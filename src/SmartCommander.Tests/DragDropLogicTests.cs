using SmartCommander.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace SmartCommander.Tests
{
    public class DragDropLogicTests
    {
        private static char Sep => Path.DirectorySeparatorChar;

        // A rooted local path that is valid on the current OS.
        private static string Local(params string[] parts) =>
            (OperatingSystem.IsWindows() ? "C:" + Sep : "/") + string.Join(Sep, parts);

        // ---- ResolveMove ----

        [Fact]
        public void ResolveMove_NoModifier_SameRoot_Moves()
        {
            Assert.True(DragDropLogic.ResolveMove(copyModifier: false, moveModifier: false, sameRoot: true));
        }

        [Fact]
        public void ResolveMove_NoModifier_DifferentRoot_Copies()
        {
            Assert.False(DragDropLogic.ResolveMove(copyModifier: false, moveModifier: false, sameRoot: false));
        }

        [Fact]
        public void ResolveMove_CopyModifier_SameRoot_Copies()
        {
            Assert.False(DragDropLogic.ResolveMove(copyModifier: true, moveModifier: false, sameRoot: true));
        }

        [Fact]
        public void ResolveMove_MoveModifier_DifferentRoot_Moves()
        {
            Assert.True(DragDropLogic.ResolveMove(copyModifier: false, moveModifier: true, sameRoot: false));
        }

        [Fact]
        public void ResolveMove_BothModifiers_Copies()
        {
            Assert.False(DragDropLogic.ResolveMove(copyModifier: true, moveModifier: true, sameRoot: true));
        }

        // ---- IsSameTransferRoot ----

        [Fact]
        public void SameTransferRoot_SameLocalRoot_True()
        {
            Assert.True(DragDropLogic.IsSameTransferRoot(Local("a", "file.txt"), Local("b")));
        }

        [Fact]
        public void SameTransferRoot_DifferentDrive_False()
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }
            Assert.False(DragDropLogic.IsSameTransferRoot(@"C:\a\file.txt", @"D:\b"));
        }

        [Fact]
        public void SameTransferRoot_DriveLetterCase_TrueOnWindows()
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }
            Assert.True(DragDropLogic.IsSameTransferRoot(@"c:\a\file.txt", @"C:\b"));
        }

        [Fact]
        public void SameTransferRoot_BothFtp_True()
        {
            Assert.True(DragDropLogic.IsSameTransferRoot("ftp:///pub/file.txt", "ftp:///incoming"));
        }

        [Fact]
        public void SameTransferRoot_LocalAndFtp_False()
        {
            Assert.False(DragDropLogic.IsSameTransferRoot(Local("a", "file.txt"), "ftp:///incoming"));
            Assert.False(DragDropLogic.IsSameTransferRoot("ftp:///pub/file.txt", Local("b")));
        }

        // ---- IsNoOpDrop ----

        [Fact]
        public void NoOpDrop_EmptySources_True()
        {
            Assert.True(DragDropLogic.IsNoOpDrop(new List<string>(), Local("a")));
        }

        [Fact]
        public void NoOpDrop_ItemAlreadyInDestination_True()
        {
            Assert.True(DragDropLogic.IsNoOpDrop(new[] { Local("a", "file.txt") }, Local("a")));
        }

        [Fact]
        public void NoOpDrop_DestinationTrailingSeparator_True()
        {
            Assert.True(DragDropLogic.IsNoOpDrop(new[] { Local("a", "file.txt") }, Local("a") + Sep));
        }

        [Fact]
        public void NoOpDrop_ItemInDriveRoot_DroppedOnRoot_True()
        {
            Assert.True(DragDropLogic.IsNoOpDrop(new[] { Local("file.txt") }, Local("")));
        }

        [Fact]
        public void NoOpDrop_FolderOntoItself_True()
        {
            Assert.True(DragDropLogic.IsNoOpDrop(new[] { Local("a", "folder") }, Local("a", "folder")));
        }

        [Fact]
        public void NoOpDrop_FolderIntoOwnSubfolder_True()
        {
            Assert.True(DragDropLogic.IsNoOpDrop(new[] { Local("a", "folder") }, Local("a", "folder", "sub")));
        }

        [Fact]
        public void NoOpDrop_IntoSiblingFolder_False()
        {
            Assert.False(DragDropLogic.IsNoOpDrop(new[] { Local("a", "folder") }, Local("a", "other")));
        }

        [Fact]
        public void NoOpDrop_PathPrefixButNotChild_False()
        {
            Assert.False(DragDropLogic.IsNoOpDrop(new[] { Local("a", "folder") }, Local("a", "folder2")));
        }

        [Fact]
        public void NoOpDrop_AnySourceInDestination_True()
        {
            Assert.True(DragDropLogic.IsNoOpDrop(new[] { Local("b", "x.txt"), Local("a", "y.txt") }, Local("a")));
        }

        [Fact]
        public void NoOpDrop_CaseInsensitiveOnWindows()
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }
            Assert.True(DragDropLogic.IsNoOpDrop(new[] { @"C:\Foo\file.txt" }, @"c:\foo"));
        }

        [Fact]
        public void NoOpDrop_FtpItemAlreadyInDestination_True()
        {
            Assert.True(DragDropLogic.IsNoOpDrop(new[] { "ftp:///pub/file.txt" }, "ftp:///pub/"));
        }

        [Fact]
        public void NoOpDrop_FtpItemInRoot_DroppedOnRoot_True()
        {
            Assert.True(DragDropLogic.IsNoOpDrop(new[] { "ftp:///file.txt" }, "ftp:///"));
        }

        [Fact]
        public void NoOpDrop_FtpFolderIntoOwnSubfolder_True()
        {
            Assert.True(DragDropLogic.IsNoOpDrop(new[] { "ftp:///pub" }, "ftp:///pub/sub"));
        }

        [Fact]
        public void NoOpDrop_LocalToFtp_False()
        {
            Assert.False(DragDropLogic.IsNoOpDrop(new[] { Local("pub", "file.txt") }, "ftp:///pub"));
        }

        // ---- GetDragItems ----

        private sealed class Item
        {
            public Item(string name) { Name = name; }
            public string Name { get; }
        }

        private static readonly Func<Item, bool> IsParent = i => i.Name == "..";

        [Fact]
        public void DragItems_PressedRowInSelection_DragsWholeSelection()
        {
            var a = new Item("a");
            var b = new Item("b");
            var c = new Item("c");
            var result = DragDropLogic.GetDragItems(b, new[] { a, b, c }, IsParent);
            Assert.Equal(new[] { a, b, c }, result);
        }

        [Fact]
        public void DragItems_PressedRowNotInSelection_DragsOnlyThatRow()
        {
            var a = new Item("a");
            var b = new Item("b");
            var c = new Item("c");
            var result = DragDropLogic.GetDragItems(c, new[] { a, b }, IsParent);
            Assert.Equal(new[] { c }, result);
        }

        [Fact]
        public void DragItems_ParentEntryFilteredOut()
        {
            var parent = new Item("..");
            var a = new Item("a");
            var result = DragDropLogic.GetDragItems(a, new[] { parent, a }, IsParent);
            Assert.Equal(new[] { a }, result);
        }
    }
}
