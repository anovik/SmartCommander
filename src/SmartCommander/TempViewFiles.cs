using Serilog;
using System;
using System.IO;

namespace SmartCommander
{
    // On-disk convention for the throwaway local copies F3/View makes of FTP files
    // (FilesPaneViewModel.ViewFtpFileAsync): one "view-<guid>" folder per view,
    // deleted when its viewer closes. CleanStale() clears any left by an abnormal exit.
    public static class TempViewFiles
    {
        public static string Root { get; } = Path.Combine(Path.GetTempPath(), "SmartCommander");

        public static string NewFolder() =>
            Path.Combine(Root, "view-" + Guid.NewGuid().ToString("N"));

        // Age-gated to one hour so a second running instance's in-use folder is safe.
        // Best-effort: a folder that can't be removed right now is skipped, not retried.
        public static void CleanStale()
        {
            try
            {
                if (!Directory.Exists(Root))
                {
                    return;
                }
                foreach (var dir in Directory.GetDirectories(Root, "view-*"))
                {
                    try
                    {
                        if (DateTime.UtcNow - Directory.GetLastWriteTimeUtc(dir) > TimeSpan.FromHours(1))
                        {
                            Directory.Delete(dir, true);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Debug(ex, "Could not delete stale temp view folder {Dir}", dir);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Temp view folder sweep failed");
            }
        }
    }
}
