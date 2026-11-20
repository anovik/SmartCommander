using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace SmartCommander.Plugins;

// Converting paths for plugin entry points that only take ANSI (system code page) strings:
// ListLoad, and ListDefaultParamStruct.DefaultIniName.
[SupportedOSPlatform("windows")]
internal static class AnsiPath
{
    private const uint CP_ACP = 0;
    private const uint CP_UTF8 = 65001;
    private const uint WC_NO_BEST_FIT_CHARS = 0x400;

    [DllImport("kernel32.dll")]
    private static extern uint GetACP();

    [DllImport("kernel32.dll")]
    private static extern int WideCharToMultiByte(uint codePage, uint flags,
        [MarshalAs(UnmanagedType.LPWStr)] string wideChars, int wideCharCount,
        byte[]? multiBytes, int multiByteCount, IntPtr defaultChar, out int usedDefaultChar);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern uint GetShortPathNameW(string longPath, char[] shortPath, uint bufferLength);

    // Encodes text in the system code page, zero-terminated. False when any character has no exact
    // equivalent there (no best-fit substitution: "č" must not silently become "c" - that names
    // a different file).
    public static bool TryEncode(string text, out byte[] zeroTerminated)
    {
        if (text.Length == 0)
        {
            zeroTerminated = [0];
            return true;
        }
        if (GetACP() == CP_UTF8)
        {
            // "Beta: Use Unicode UTF-8" system setting: everything is representable, and
            // WideCharToMultiByte rejects the usedDefaultChar check for this code page.
            zeroTerminated = Encoding.UTF8.GetBytes(text + "\0");
            return true;
        }
        int length = WideCharToMultiByte(CP_ACP, WC_NO_BEST_FIT_CHARS, text, text.Length, null, 0, IntPtr.Zero, out int usedDefault);
        if (length <= 0 || usedDefault != 0)
        {
            zeroTerminated = [];
            return false;
        }
        zeroTerminated = new byte[length + 1];
        WideCharToMultiByte(CP_ACP, WC_NO_BEST_FIT_CHARS, text, text.Length, zeroTerminated, length, IntPtr.Zero, out _);
        return true;
    }

    // The 8.3 short form of an existing path, or null when the volume has no short names for it.
    public static string? GetShortPath(string path)
    {
        var buffer = new char[1024];
        uint length = GetShortPathNameW(path, buffer, (uint)buffer.Length);
        if (length == 0 || length >= buffer.Length)
        {
            return null;
        }
        return new string(buffer, 0, (int)length);
    }

    // The path as ANSI bytes, falling back to its 8.3 short form - what Total Commander passes to
    // ANSI-only plugins. False when neither form is representable (8.3 names disabled).
    public static bool TryEncodePath(string path, out byte[] zeroTerminated)
    {
        if (TryEncode(path, out zeroTerminated))
        {
            return true;
        }
        string? shortPath = GetShortPath(path);
        return shortPath != null && TryEncode(shortPath, out zeroTerminated);
    }
}
