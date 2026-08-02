using System;

namespace SmartCommander.Services
{
    // Parses/formats the "ftp://" path scheme. No connection name is embedded in the path
    // itself, since at most one FTP connection is ever active app-wide — there is never
    // ambiguity about which connection a "ftp://" path refers to.
    public static class RemotePath
    {
        public const string Scheme = "ftp://";

        public static bool IsFtp(string path) =>
            path.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase);

        // The path portion after "ftp://", always starting with '/'. Throws if not an ftp:// path.
        public static string GetRemotePart(string path)
        {
            if (!IsFtp(path))
            {
                throw new ArgumentException($"Not an ftp:// path: {path}", nameof(path));
            }
            var remainder = path.Substring(Scheme.Length);
            return remainder.StartsWith('/') ? remainder : "/" + remainder;
        }

        public static string Combine(string remotePart) =>
            Scheme + (remotePart.StartsWith('/') ? remotePart : "/" + remotePart);

        // Scheme-aware replacement for Path.Combine(basePath, name): a plain Path.Combine would
        // join an "ftp://" base with the OS-native separator (backslash on Windows), producing a
        // remote path FluentFTP can't resolve. Falls through to Path.Combine for local paths.
        public static string CombineChild(string basePath, string name) =>
            IsFtp(basePath)
                ? Combine(GetRemotePart(basePath).TrimEnd('/') + "/" + name)
                : System.IO.Path.Combine(basePath, name);
    }
}
