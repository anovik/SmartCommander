using System;

namespace SmartCommander.ViewModels
{
    // What a file looked like at one moment; two different stamps of the same file mean
    // it was written in between.
    internal readonly record struct FileStamp(DateTime Modified, long Size);

    // Pure decisions behind F4 Edit over FTP (MainWindowViewModel.FtpEdit.cs).
    internal static class FtpEditLogic
    {
        // A save is acted on once the local copy differs from the state already dealt with
        // (handled) and looked the same on two consecutive polls - i.e. the editor has
        // finished writing it.
        internal static bool IsSettledSave(FileStamp handled, FileStamp previous, FileStamp current) =>
            current != handled && current == previous;
    }
}
