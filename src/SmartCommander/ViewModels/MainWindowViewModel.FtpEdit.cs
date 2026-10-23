using Avalonia.Controls;
using MsBox.Avalonia.Enums;
using Serilog;
using SmartCommander.Assets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SmartCommander.ViewModels
{
    // F4 Edit over FTP. The editor needs a local file, so the remote file is downloaded to
    // a private temp folder and that copy is opened. The editor process can't be relied on
    // to tell when editing is over (Windows 11 Notepad hands the file to an already-running
    // instance, a Linux terminal emulator may fork), so the copy is polled instead and every
    // save that has settled is offered for upload. A session therefore has no natural end:
    // it lives until the FTP connection or the app goes away.
    public partial class MainWindowViewModel
    {
        private sealed class FtpEditSession
        {
            public FtpEditSession(string ftpPath, string ftpFolder, string tempDir)
            {
                FtpPath = ftpPath;
                FtpFolder = ftpFolder;
                TempDir = tempDir;
                TempFile = Path.Combine(tempDir, Path.GetFileName(ftpPath));
            }

            public string FtpPath { get; }
            // The pane directory the file was opened from; panes showing it are refreshed
            // after an upload.
            public string FtpFolder { get; }
            public string TempDir { get; }
            public string TempFile { get; }

            // The remote file as last downloaded or uploaded. A different stamp at upload
            // time means it was changed on the server meanwhile.
            public FileStamp Remote { get; set; }
            // The state of the local copy that needs no (further) upload prompt: as
            // downloaded, as uploaded, or as declined. Reset to default by a failed upload
            // so that save is offered again.
            public FileStamp Handled { get; set; }
            // The local copy as of the previous poll.
            public FileStamp Previous { get; set; }
        }

        private static readonly TimeSpan FtpEditPollInterval = TimeSpan.FromSeconds(1);

        // Same cap as F3 View over FTP (FilesPaneViewModel.ViewFtpFileAsync).
        private const long FtpEditMaxSize = 128 * 1024 * 1024;

        // UI-thread-only, like ActiveOperations.
        private readonly List<FtpEditSession> _ftpEditSessions = new();
        private CancellationTokenSource? _ftpEditWatchCts;
        // Released to run a poll now instead of at the end of the interval.
        private readonly SemaphoreSlim _ftpEditWake = new(0, 1);
        // Held for the whole of a download, a poll (prompts and upload included) and the
        // ending of all sessions, so the three never interleave: F4 and Disconnect/Exit
        // queue behind whatever is in flight, the poll just skips a turn.
        private readonly SemaphoreSlim _ftpEditGate = new(1, 1);

        internal bool HasFtpEditSessions => _ftpEditSessions.Count > 0;

        // Returns the local copy to open in the editor, or null when there is nothing to
        // open (the reason was already shown). Never throws.
        internal async Task<string?> BeginFtpEditAsync(string ftpPath, string ftpFolder)
        {
            await _ftpEditGate.WaitAsync();
            try
            {
                var existing = _ftpEditSessions.FirstOrDefault(s => s.FtpPath == ftpPath);
                if (existing != null)
                {
                    // F4 on a file that already has a session reopens its local copy as it
                    // is. It is deliberately not refreshed from the server: an editor may
                    // still hold the old text, and refreshing would also move the conflict
                    // baseline (Remote) past a change that editor never saw.
                    if (await _fs.FileExistsAsync(existing.TempFile))
                    {
                        return existing.TempFile;
                    }
                    _ftpEditSessions.Remove(existing);
                    await DeleteFtpEditFolderAsync(existing.TempDir);
                }
                return await DownloadForFtpEditAsync(ftpPath, ftpFolder);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to open FTP file {FtpPath} for editing", ftpPath);
                await ShowMessageBoxAsync(DescribeException(ex), ButtonEnum.Ok);
                return null;
            }
            finally
            {
                _ftpEditGate.Release();
            }
        }

        // Starts a new session. Returns null when the file is over the size cap.
        private async Task<string?> DownloadForFtpEditAsync(string ftpPath, string ftpFolder)
        {
            // Taken before the download, so a server-side change racing it shows up as a
            // conflict at upload time rather than going unnoticed.
            var remote = await ReadStampAsync(ftpPath);
            if (remote.Size > FtpEditMaxSize)
            {
                await ShowMessageBoxAsync(Resources.TooLargeSize, ButtonEnum.Ok);
                return null;
            }

            var session = new FtpEditSession(ftpPath, ftpFolder, TempViewFiles.NewEditFolder());
            bool started = false;
            try
            {
                await _fs.CreateDirectoryAsync(session.TempDir);
                await _fs.CopyFileAsync(ftpPath, session.TempFile, false, true, null, 0, 0, CancellationToken.None);
                var local = await ReadStampAsync(session.TempFile);

                // A negative size means the server has no SIZE support, so the cap can only
                // be checked on the downloaded copy.
                if (remote.Size < 0 && local.Size > FtpEditMaxSize)
                {
                    await ShowMessageBoxAsync(Resources.TooLargeSize, ButtonEnum.Ok);
                    return null;
                }

                session.Remote = remote;
                session.Handled = session.Previous = local;
                _ftpEditSessions.Add(session);
                StartFtpEditWatch();
                started = true;
                return session.TempFile;
            }
            finally
            {
                if (!started)
                {
                    await DeleteFtpEditFolderAsync(session.TempDir);
                }
            }
        }

        // Off the UI thread: the local provider's metadata calls are synchronous, and the
        // poll makes them every second.
        private Task<FileStamp> ReadStampAsync(string path) =>
            Task.Run(async () => new FileStamp(await _fs.GetModifiedTimeAsync(path), await _fs.GetFileSizeAsync(path)));

        private void StartFtpEditWatch()
        {
            if (_ftpEditWatchCts != null)
            {
                return;
            }
            _ftpEditWatchCts = new CancellationTokenSource();
            _ = WatchFtpEditsAsync(_ftpEditWatchCts.Token);
        }

        private void StopFtpEditWatch()
        {
            _ftpEditWatchCts?.Cancel();
            _ftpEditWatchCts?.Dispose();
            _ftpEditWatchCts = null;
        }

        // Called on the UI thread when the main window is activated.
        internal void WakeFtpEditWatch()
        {
            if (_ftpEditWatchCts != null && _ftpEditWake.CurrentCount == 0)
            {
                _ftpEditWake.Release();
            }
        }

        // Started from BeginFtpEditAsync on the UI thread, so every continuation (and every
        // touch of _ftpEditSessions) stays on it.
        private async Task WatchFtpEditsAsync(CancellationToken ct)
        {
            while (true)
            {
                try
                {
                    await _ftpEditWake.WaitAsync(FtpEditPollInterval, ct);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                if (!_ftpEditGate.Wait(0))
                {
                    continue;
                }

                try
                {
                    // The copies are tracked on every poll, but a settled save is only
                    // offered while the main window is the active one. That keeps the upload
                    // prompt from stacking on another dialog (and taking the Enter/Esc meant
                    // for it), from being shown over a window hidden in the tray, and from
                    // popping up while the user is still typing in the editor. Because the
                    // save has already settled by then, it is offered the moment the user
                    // comes back (the Activated event wakes this loop).
                    bool canPrompt = GetTopLevel() is Window { IsActive: true };
                    foreach (var session in _ftpEditSessions.ToList())
                    {
                        await CheckFtpEditAsync(session, canPrompt);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "FTP edit watch failed");
                }
                finally
                {
                    _ftpEditGate.Release();
                }
            }
        }

        private async Task CheckFtpEditAsync(FtpEditSession session, bool canPrompt)
        {
            FileStamp current;
            try
            {
                current = await ReadStampAsync(session.TempFile);
            }
            catch (Exception)
            {
                // Usually a save caught midway (some editors replace the file rather than
                // rewrite it); the next poll looks again. A copy whose folder is gone -
                // deleted by hand, or swept by another instance - ends the session.
                if (!await _fs.DirectoryExistsAsync(session.TempDir))
                {
                    _ftpEditSessions.Remove(session);
                }
                return;
            }

            bool settled = FtpEditLogic.IsSettledSave(session.Handled, session.Previous, current);
            session.Previous = current;
            // A settled save left unoffered stays settled (Handled is untouched), so the
            // first poll that may prompt picks it up.
            if (settled && canPrompt)
            {
                await OfferFtpEditUploadAsync(session, current);
            }
        }

        private async Task OfferFtpEditUploadAsync(FtpEditSession session, FileStamp saved)
        {
            // Whatever the answer, this save is dealt with: declining must not re-prompt.
            session.Handled = saved;

            var name = Path.GetFileName(session.FtpPath);
            var answer = await ShowMessageBoxAsync(string.Format(Resources.FtpEditUploadPrompt, name), ButtonEnum.YesNo);
            if (answer != ButtonResult.Yes)
            {
                return;
            }

            try
            {
                if (await ReadStampAsync(session.FtpPath) != session.Remote)
                {
                    answer = await ShowMessageBoxAsync(string.Format(Resources.FtpEditConflictPrompt, name),
                        ButtonEnum.YesNo, ButtonResult.No);
                    if (answer != ButtonResult.Yes)
                    {
                        return;
                    }
                }

                // Re-read: a save made while the prompts were open goes up with this upload
                // and must not be offered a second time.
                session.Handled = session.Previous = await ReadStampAsync(session.TempFile);
                await _fs.CopyFileAsync(session.TempFile, session.FtpPath, false, true, null, 0, 0, CancellationToken.None);
                session.Remote = await ReadStampAsync(session.FtpPath);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to upload edited FTP file {FtpPath}", session.FtpPath);
                // Not dealt with after all. The save is offered again - by the next poll, or
                // by the loop in EndFtpEditSessionsAsync - which doubles as the retry; No
                // there gives it up.
                session.Handled = default;
                await ShowMessageBoxAsync(string.Format(Resources.FtpEditUploadFailed, name, DescribeException(ex)), ButtonEnum.Ok);
                return;
            }

            foreach (var pane in new[] { LeftFileViewModel, RightFileViewModel })
            {
                if (pane.CurrentDirectory == session.FtpFolder)
                {
                    pane.Update();
                }
            }
        }

        // Called while the FTP connection is still up, before Disconnect or app exit: a save
        // that hasn't been offered yet (or whose upload failed) gets its prompt while there
        // is a server to upload to, then the local copies are deleted. Needs a visible main
        // window to prompt over.
        internal async Task EndFtpEditSessionsAsync()
        {
            await _ftpEditGate.WaitAsync();
            try
            {
                StopFtpEditWatch();
                foreach (var session in _ftpEditSessions.ToList())
                {
                    try
                    {
                        FileStamp current;
                        while ((current = await ReadStampAsync(session.TempFile)) != session.Handled)
                        {
                            await OfferFtpEditUploadAsync(session, current);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Debug(ex, "Could not read FTP edit copy {TempFile}", session.TempFile);
                    }
                    await DeleteFtpEditFolderAsync(session.TempDir);
                }
                _ftpEditSessions.Clear();
            }
            finally
            {
                _ftpEditGate.Release();
            }
        }

        // Last resort for a close that never went through EndFtpEditSessionsAsync (e.g. an
        // OS shutdown): nothing can be prompted or uploaded any more, so the sessions are
        // dropped and their temp folders handed to the caller to delete.
        internal IReadOnlyList<string> EndFtpEditSessionsOnExit()
        {
            StopFtpEditWatch();
            var folders = _ftpEditSessions.Select(s => s.TempDir).ToList();
            _ftpEditSessions.Clear();
            return folders;
        }

        private async Task DeleteFtpEditFolderAsync(string tempDir)
        {
            try
            {
                await _fs.DeleteDirectoryAsync(tempDir);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to delete temp edit folder {TempDir}", tempDir);
            }
        }
    }
}
