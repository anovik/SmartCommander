using SmartCommander.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SmartCommander.ViewModels
{
    // Pure, string-only decisions behind drag-and-drop. Kept synchronous and I/O-free because
    // DragOver calls them on every pointer move to pick the cursor effect.
    internal static class DragDropLogic
    {
        private static StringComparison LocalComparison =>
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        // Same local volume, or both on the (single, app-wide) FTP connection. Decides whether
        // an unmodified drag defaults to Move (same root) or Copy.
        internal static bool IsSameTransferRoot(string sourcePath, string destDirectory)
        {
            bool sourceIsFtp = RemotePath.IsFtp(sourcePath);
            if (sourceIsFtp != RemotePath.IsFtp(destDirectory))
            {
                return false;
            }
            if (sourceIsFtp)
            {
                return true;
            }
            return string.Equals(Path.GetPathRoot(sourcePath), Path.GetPathRoot(destDirectory), LocalComparison);
        }

        // copyModifier wins over moveModifier when both are held: a copy never loses data.
        internal static bool ResolveMove(bool copyModifier, bool moveModifier, bool sameRoot)
        {
            if (copyModifier)
            {
                return false;
            }
            if (moveModifier)
            {
                return true;
            }
            return sameRoot;
        }

        // A drop that could only fail validation is refused up front (no-drop cursor, no dialog):
        // an item already in the destination directory, a folder dropped onto itself, or a folder
        // dropped into its own subtree.
        internal static bool IsNoOpDrop(IReadOnlyList<string> sourcePaths, string destDirectory)
        {
            if (sourcePaths.Count == 0)
            {
                return true;
            }
            var dest = Normalize(destDirectory);
            var comparison = RemotePath.IsFtp(destDirectory) ? StringComparison.Ordinal : LocalComparison;
            char separator = RemotePath.IsFtp(destDirectory) ? '/' : Path.DirectorySeparatorChar;
            foreach (var source in sourcePaths)
            {
                if (RemotePath.IsFtp(source) != RemotePath.IsFtp(destDirectory))
                {
                    continue;
                }
                var src = Normalize(source);
                var parent = GetParent(src);
                if (parent != null && string.Equals(parent, dest, comparison))
                {
                    return true;
                }
                if (string.Equals(src, dest, comparison) || dest.StartsWith(src + separator, comparison))
                {
                    return true;
                }
            }
            return false;
        }

        // D6: dragging a row that was part of the selection drags the whole selection; dragging
        // any other row drags just that row. ".." is never part of a drag.
        internal static List<T> GetDragItems<T>(T pressedItem, IReadOnlyList<T> selectionAtPress, Func<T, bool> isParentEntry)
            where T : class
        {
            var items = selectionAtPress.Contains(pressedItem) ? selectionAtPress.ToList() : new List<T> { pressedItem };
            return items.Where(i => !isParentEntry(i)).ToList();
        }

        private static string Normalize(string path)
        {
            if (RemotePath.IsFtp(path))
            {
                return RemotePath.Combine(RemotePath.GetRemotePart(path).TrimEnd('/'));
            }
            var trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            // Keep a bare root ("C:\" or "/") intact - trimming it would change its meaning.
            var root = Path.GetPathRoot(path);
            return !string.IsNullOrEmpty(root) && trimmed.Length < root.Length ? root : trimmed;
        }

        private static string? GetParent(string normalizedPath)
        {
            if (RemotePath.IsFtp(normalizedPath))
            {
                var remote = RemotePath.GetRemotePart(normalizedPath);
                int slash = remote.LastIndexOf('/');
                return slash < 0 || remote == "/" ? null : RemotePath.Combine(remote.Substring(0, slash));
            }
            var parent = Path.GetDirectoryName(normalizedPath);
            return parent == null ? null : Normalize(parent);
        }
    }
}
