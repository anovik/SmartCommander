using Serilog;
using System;
using System.IO;

namespace SmartCommander
{
    // On-disk convention for the throwaway local copies made of FTP files: one
    // "view-<guid>" folder per F3 view (FilesPaneViewModel.ViewFtpFileAsync), deleted
    // when its viewer closes, and one "edit-<guid>" folder per F4 edit session
    // (MainWindowViewModel.FtpEdit.cs), deleted when the session ends.
    // CleanStale() clears any left by an abnormal exit.
    public static class TempViewFiles
    {
        public static string Root { get; } = Path.Combine(Path.GetTempPath(), "SmartCommander");

        public static string NewFolder() =>
            Path.Combine(Root, "view-" + Guid.NewGuid().ToString("N"));

        public static string NewEditFolder() =>
            Path.Combine(Root, "edit-" + Guid.NewGuid().ToString("N"));

        // Age-gated so a second running instance's in-use folder is safe: one hour for a
        // view, a day for an edit session, which can stay open far longer (and saving the
        // file in place doesn't refresh its folder's timestamp).
        public static void CleanStale()
        {
            CleanStale("view-*", TimeSpan.FromHours(1));
            CleanStale("edit-*", TimeSpan.FromDays(1));
        }

        // Best-effort: a folder that can't be removed right now is skipped, not retried.
        private static void CleanStale(string pattern, TimeSpan maxAge)
        {
            try
            {
                if (!Directory.Exists(Root))
                {
                    return;
                }
                foreach (var dir in Directory.GetDirectories(Root, pattern))
                {
                    TryDelete(dir, maxAge);
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Temp folder sweep failed for {Pattern}", pattern);
            }
        }

        // Also called directly (maxAge zero) for the edit folders still open at app exit.
        public static void TryDelete(string dir, TimeSpan maxAge = default)
        {
            try
            {
                if (DateTime.UtcNow - Directory.GetLastWriteTimeUtc(dir) > maxAge)
                {
                    Directory.Delete(dir, true);
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Could not delete temp folder {Dir}", dir);
            }
        }
    }
}
