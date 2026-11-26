using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Serilog;

namespace SmartCommander
{
    [SupportedOSPlatform("windows")]
    public static class ShellContextMenuHelper
    {
        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("000214e4-0000-0000-c000-000000000046")]
        private interface IContextMenu
        {
            [PreserveSig]
            int QueryContextMenu(IntPtr hmenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags);
            [PreserveSig]
            int InvokeCommand(IntPtr lpici);
            [PreserveSig]
            int GetCommandString(UIntPtr idCmd, uint uType, IntPtr pwReserved, [MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszName, uint cchMax);
        }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("000214f4-0000-0000-c000-000000000046")]
        private interface IContextMenu2 : IContextMenu
        {
            [PreserveSig]
            new int QueryContextMenu(IntPtr hmenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags);
            [PreserveSig]
            new int InvokeCommand(IntPtr lpici);
            [PreserveSig]
            new int GetCommandString(UIntPtr idCmd, uint uType, IntPtr pwReserved, [MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszName, uint cchMax);
            [PreserveSig]
            int HandleMenuMsg(uint uMsg, IntPtr wParam, IntPtr lParam);
        }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("bcfce0a0-ec17-11d0-8d10-00a0c90f2719")]
        private interface IContextMenu3 : IContextMenu2
        {
            [PreserveSig]
            new int QueryContextMenu(IntPtr hmenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags);
            [PreserveSig]
            new int InvokeCommand(IntPtr lpici);
            [PreserveSig]
            new int GetCommandString(UIntPtr idCmd, uint uType, IntPtr pwReserved, [MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszName, uint cchMax);
            [PreserveSig]
            new int HandleMenuMsg(uint uMsg, IntPtr wParam, IntPtr lParam);
            [PreserveSig]
            int HandleMenuMsg2(uint uMsg, IntPtr wParam, IntPtr lParam, out IntPtr plResult);
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private struct CMINVOKECOMMANDINFO
        {
            public int cbSize;
            public uint fMask;
            public IntPtr hwnd;
            public IntPtr lpVerb;
            [MarshalAs(UnmanagedType.LPStr)]
            public string lpParameters;
            [MarshalAs(UnmanagedType.LPStr)]
            public string lpDirectory;
            public int nShow;
            public uint dwHotKey;
            public IntPtr hIcon;
        }

        private const uint CMF_NORMAL = 0x00000000;
        private const uint CMF_EXPLORER = 0x00000004;
        private const uint TPM_RETURNCMD = 0x0100;
        private const uint TPM_LEFTALIGN = 0x0000;

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr ILCreateFromPath([MarshalAs(UnmanagedType.LPWStr)] string pszPath);

        [DllImport("shell32.dll")]
        private static extern void ILFree(IntPtr pidl);

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("000214e6-0000-0000-c000-000000000046")]
        private interface IShellFolder
        {
            [PreserveSig]
            int ParseDisplayName(IntPtr hwnd, IntPtr pbc, [MarshalAs(UnmanagedType.LPWStr)] string pszDisplayName, out uint pchEaten, out IntPtr ppidl, ref uint pdwAttributes);
            [PreserveSig]
            int EnumObjects(IntPtr hwnd, uint grfFlags, out IntPtr ppenumIDList);
            [PreserveSig]
            int BindToObject(IntPtr pidl, IntPtr pbc, [In] ref Guid riid, out IntPtr ppv);
            [PreserveSig]
            int BindToStorage(IntPtr pidl, IntPtr pbc, [In] ref Guid riid, out IntPtr ppv);
            [PreserveSig]
            int CompareIDs(IntPtr lParam, IntPtr pidl1, IntPtr pidl2);
            [PreserveSig]
            int CreateViewObject(IntPtr hwndOwner, [In] ref Guid riid, out IntPtr ppv);
            [PreserveSig]
            int GetAttributesOf(uint cidl, [In, MarshalAs(UnmanagedType.LPArray)] IntPtr[] apidl, ref uint rgfInOut);
            [PreserveSig]
            int GetUIObjectOf(IntPtr hwndOwner, uint cidl, [In, MarshalAs(UnmanagedType.LPArray)] IntPtr[] apidl, [In] ref Guid riid, ref uint rgfReserved, out IntPtr ppv);
            [PreserveSig]
            int GetDisplayNameOf(IntPtr pidl, uint uFlags, out IntPtr pName);
            [PreserveSig]
            int SetNameOf(IntPtr hwnd, IntPtr pidl, [MarshalAs(UnmanagedType.LPWStr)] string pszName, uint uFlags, out IntPtr ppidlOut);
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHParseDisplayName([MarshalAs(UnmanagedType.LPWStr)] string pszName, IntPtr pbc, out IntPtr ppidl, uint sfgaoIn, out uint psfgaoOut);

        [DllImport("shell32.dll")]
        private static extern int SHBindToParent(IntPtr pidl, [In] ref Guid riid, out IntPtr ppv, out IntPtr ppidlLast);

        [DllImport("shell32.dll")]
        private static extern IntPtr ILFindLastID(IntPtr pidl);

        [DllImport("shell32.dll")]
        private static extern IntPtr ILClone(IntPtr pidl);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern int SHCreateDefaultContextMenu(ref DEFCONTEXTMENU pdcm, ref Guid riid, out IntPtr ppv);

        [StructLayout(LayoutKind.Sequential)]
        private struct DEFCONTEXTMENU
        {
            public IntPtr hwnd;
            public IntPtr pcmcb;
            public IntPtr pidlFolder;
            public IntPtr psf;
            public uint cidl;
            public IntPtr apidl;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll")]
        private static extern bool DestroyMenu(IntPtr hMenu);

        [DllImport("user32.dll")]
        private static extern uint TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y, int nReserved, IntPtr hWnd, IntPtr prcRect);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("shell32.dll")]
        private static extern int SHGetDesktopFolder(out IntPtr ppsf);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        private const int GWLP_WNDPROC = -4;
        private const uint WM_DRAWITEM = 0x002B;
        private const uint WM_MEASUREITEM = 0x002C;
        private const uint WM_INITMENUPOPUP = 0x0117;
        private const uint WM_MENUCHAR = 0x0120;

        private delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
        private static extern IntPtr SetWindowLong32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "CallWindowProcW")]
        private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
        private static extern IntPtr GetWindowLong32(IntPtr hWnd, int nIndex);

        private static IntPtr SetWndProc(IntPtr hWnd, IntPtr wndProc)
        {
            return IntPtr.Size == 8
                ? SetWindowLongPtr64(hWnd, GWLP_WNDPROC, wndProc)
                : SetWindowLong32(hWnd, GWLP_WNDPROC, wndProc);
        }

        private static IntPtr GetWndProc(IntPtr hWnd)
        {
            return IntPtr.Size == 8
                ? GetWindowLongPtr64(hWnd, GWLP_WNDPROC)
                : GetWindowLong32(hWnd, GWLP_WNDPROC);
        }

        // Drops the runtime wrapper's references now instead of at some later GC, so shell extensions
        // loaded for the menu don't keep the selected files open after the menu has closed.
        private static void ReleaseRcw(object? comObject)
        {
            if (comObject != null && Marshal.IsComObject(comObject))
            {
                Marshal.FinalReleaseComObject(comObject);
            }
        }

        // Hooks that couldn't be removed because something subclassed the window on top of them while the
        // menu was open. They stay in the chain as pass-throughs, so they must never be collected.
        private static readonly List<WndProc> s_strandedHooks = new();

        // Shows the populated menu, and runs the picked command. While the menu is open the owner window
        // is subclassed so menu messages reach the shell: dynamic submenus (notably "New") are only filled
        // on WM_INITMENUPOPUP, and owner-drawn items need WM_MEASUREITEM / WM_DRAWITEM.
        // Returns true when a command was picked and invoked (the folder may have changed).
        private static bool TrackAndInvoke(IntPtr hwnd, IntPtr hMenu, IContextMenu menu)
        {
            var menu2 = menu as IContextMenu2;
            var menu3 = menu as IContextMenu3;

            IntPtr oldWndProc = IntPtr.Zero;
            IntPtr hookPtr = IntPtr.Zero;
            bool menuOpen = true;
            WndProc? hook = null;
            if (menu2 != null)
            {
                hook = (hWnd, msg, wParam, lParam) =>
                {
                    if (menuOpen && (msg == WM_INITMENUPOPUP || msg == WM_DRAWITEM || msg == WM_MEASUREITEM || msg == WM_MENUCHAR))
                    {
                        try
                        {
                            if (menu3 != null && menu3.HandleMenuMsg2(msg, wParam, lParam, out IntPtr result) == 0)
                            {
                                return result;
                            }
                            // Some extensions implement only the older IContextMenu2 entry point.
                            if (msg != WM_MENUCHAR && menu2.HandleMenuMsg(msg, wParam, lParam) == 0)
                            {
                                return msg == WM_INITMENUPOPUP ? IntPtr.Zero : (IntPtr)1;
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Warning(ex, "Shell context menu message {Msg} failed", msg);
                        }
                    }
                    return CallWindowProc(oldWndProc, hWnd, msg, wParam, lParam);
                };
                hookPtr = Marshal.GetFunctionPointerForDelegate(hook);
                oldWndProc = SetWndProc(hwnd, hookPtr);
            }

            uint cmdId;
            try
            {
                if (!GetCursorPos(out POINT pt))
                {
                    pt = new POINT { x = 0, y = 0 };
                }
                cmdId = TrackPopupMenu(hMenu, TPM_RETURNCMD | TPM_LEFTALIGN, pt.x, pt.y, 0, hwnd, IntPtr.Zero);
            }
            finally
            {
                menuOpen = false;
                if (oldWndProc != IntPtr.Zero)
                {
                    if (GetWndProc(hwnd) == hookPtr)
                    {
                        SetWndProc(hwnd, oldWndProc);
                    }
                    else
                    {
                        // Restoring now would cut out whoever subclassed on top of us; stay in as a pass-through.
                        Log.Warning("Window was subclassed while the shell context menu was open; leaving the hook in place");
                        s_strandedHooks.Add(hook!);
                    }
                }
                GC.KeepAlive(hook);
            }

            if (cmdId == 0 || cmdId > 0x7FFF)
            {
                return false;
            }

            CMINVOKECOMMANDINFO ici = new CMINVOKECOMMANDINFO
            {
                cbSize = Marshal.SizeOf(typeof(CMINVOKECOMMANDINFO)),
                hwnd = hwnd,
                lpVerb = (IntPtr)(long)(cmdId - 1),
                nShow = 1 // SW_SHOWNORMAL
            };
            IntPtr iciPtr = Marshal.AllocHGlobal(ici.cbSize);
            try
            {
                Marshal.StructureToPtr(ici, iciPtr, false);
                int hr3 = menu.InvokeCommand(iciPtr);
                if (hr3 < 0)
                {
                    Log.Warning("InvokeCommand failed: {HResult}", hr3);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(iciPtr);
            }
            return true;
        }

        // Both Show methods return true when a shell command was invoked, so the caller can refresh.
        public static bool ShowContextMenu(IntPtr hwnd, string[] paths)
        {
            if (paths == null || paths.Length == 0) return false;
            if (hwnd == IntPtr.Zero) return false;

            IntPtr hMenu = CreatePopupMenu();
            if (hMenu == IntPtr.Zero) return false;
            bool invoked = false;

            IntPtr[] absolutePidls = new IntPtr[paths.Length];
            IntPtr[] relativePidls = new IntPtr[paths.Length];
            IntPtr parentFolderPtr = IntPtr.Zero;
            IShellFolder? parentFolder = null;

            try
            {
                Guid guidIShellFolder = new Guid("000214e6-0000-0000-c000-000000000046");
                for (int i = 0; i < paths.Length; i++)
                {
                    // Any unresolvable path aborts the whole menu: a null PIDL in GetUIObjectOf's array can
                    // crash inside shell32, and a menu for only part of the selection would mislead (Delete).
                    uint sfgaoOut;
                    int hrP = SHParseDisplayName(paths[i], IntPtr.Zero, out absolutePidls[i], 0, out sfgaoOut);
                    if (hrP != 0)
                    {
                        Log.Warning("SHParseDisplayName failed for {Path}: {HResult}", paths[i], hrP);
                        return false;
                    }

                    IntPtr currentParentFolderPtr;
                    IntPtr relativePidl;
                    int hrB = SHBindToParent(absolutePidls[i], ref guidIShellFolder, out currentParentFolderPtr, out relativePidl);
                    if (hrB != 0)
                    {
                        Log.Warning("SHBindToParent failed for {Path}: {HResult}", paths[i], hrB);
                        return false;
                    }

                    if (parentFolderPtr == IntPtr.Zero)
                    {
                        parentFolderPtr = currentParentFolderPtr;
                    }
                    else
                    {
                        Marshal.Release(currentParentFolderPtr);
                    }
                    relativePidls[i] = ILClone(relativePidl);
                }

                parentFolder = (IShellFolder)Marshal.GetTypedObjectForIUnknown(parentFolderPtr, typeof(IShellFolder));

                Guid guidIContextMenu = new Guid("000214e4-0000-0000-c000-000000000046");
                uint rgfReserved = 0;

                int hr = parentFolder.GetUIObjectOf(hwnd, (uint)relativePidls.Length, relativePidls, ref guidIContextMenu, ref rgfReserved, out IntPtr ppv);

                if (hr == 0 && ppv != IntPtr.Zero)
                {
                    IContextMenu? menu = null;
                    try
                    {
                        menu = (IContextMenu)Marshal.GetTypedObjectForIUnknown(ppv, typeof(IContextMenu));

                        int hr2 = menu.QueryContextMenu(hMenu, 0, 1, 0x7FFF, CMF_EXPLORER);
                        if (hr2 < 0)
                        {
                            Log.Warning("QueryContextMenu failed: {HResult}", hr2);
                            return false;
                        }

                        invoked = TrackAndInvoke(hwnd, hMenu, menu);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Context menu error");
                    }
                    finally
                    {
                        ReleaseRcw(menu);
                        Marshal.Release(ppv);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "ShellContextMenuHelper error");
            }
            finally
            {
                ReleaseRcw(parentFolder);
                if (parentFolderPtr != IntPtr.Zero) Marshal.Release(parentFolderPtr);
                foreach (var pidl in absolutePidls)
                {
                    if (pidl != IntPtr.Zero) ILFree(pidl);
                }
                foreach (var pidl in relativePidls)
                {
                    if (pidl != IntPtr.Zero) ILFree(pidl);
                }
                DestroyMenu(hMenu);
            }
            return invoked;
        }

        public static bool ShowBackgroundContextMenu(IntPtr hwnd, string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath)) return false;
            if (hwnd == IntPtr.Zero) return false;

            IntPtr hMenu = CreatePopupMenu();
            if (hMenu == IntPtr.Zero) return false;
            bool invoked = false;

            IntPtr folderPidl = IntPtr.Zero;
            IntPtr folderPtr = IntPtr.Zero;
            IShellFolder? folder = null;

            try
            {
                uint sfgaoOut;
                int hrP = SHParseDisplayName(folderPath, IntPtr.Zero, out folderPidl, 0, out sfgaoOut);
                if (hrP != 0) return false;

                Guid guidIShellFolder = new Guid("000214e6-0000-0000-c000-000000000046");
                
                // Get IShellFolder for the folder itself
                IntPtr desktopFolderPtr;
                SHGetDesktopFolder(out desktopFolderPtr);
                IShellFolder desktopFolder = (IShellFolder)Marshal.GetTypedObjectForIUnknown(desktopFolderPtr, typeof(IShellFolder));

                int hrB = desktopFolder.BindToObject(folderPidl, IntPtr.Zero, ref guidIShellFolder, out folderPtr);
                ReleaseRcw(desktopFolder);
                Marshal.Release(desktopFolderPtr);

                if (hrB == 0 && folderPtr != IntPtr.Zero)
                {
                    folder = (IShellFolder)Marshal.GetTypedObjectForIUnknown(folderPtr, typeof(IShellFolder));

                    Guid guidIID_IContextMenu = new Guid("000214e4-0000-0000-c000-000000000046");

                    int hrV = folder.CreateViewObject(hwnd, ref guidIID_IContextMenu, out IntPtr ppv);

                    if (hrV == 0 && ppv != IntPtr.Zero)
                    {
                        IContextMenu? menu = null;
                        try
                        {
                            menu = (IContextMenu)Marshal.GetTypedObjectForIUnknown(ppv, typeof(IContextMenu));

                            int hr2 = menu.QueryContextMenu(hMenu, 0, 1, 0x7FFF, CMF_NORMAL | CMF_EXPLORER);
                            if (hr2 >= 0)
                            {
                                invoked = TrackAndInvoke(hwnd, hMenu, menu);
                            }
                            else
                            {
                                Log.Warning("QueryContextMenu failed: {HResult}", hr2);
                            }
                        }
                        finally
                        {
                            ReleaseRcw(menu);
                            Marshal.Release(ppv);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "ShowBackgroundContextMenu error");
            }
            finally
            {
                if (folderPidl != IntPtr.Zero) ILFree(folderPidl);
                ReleaseRcw(folder);
                if (folderPtr != IntPtr.Zero) Marshal.Release(folderPtr);
                DestroyMenu(hMenu);
            }
            return invoked;
        }
    }
}
