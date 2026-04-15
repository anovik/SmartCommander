using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Versioning;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

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
        [Guid("000214e5-0000-0000-c000-000000000046")]
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
        [Guid("bcfce0a1-0157-11d2-9305-00a0c9034912")]
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

        public static void ShowContextMenu(IntPtr hwnd, string[] paths)
        {
            if (paths == null || paths.Length == 0) return;
            if (hwnd == IntPtr.Zero) return;

            IntPtr hMenu = CreatePopupMenu();
            if (hMenu == IntPtr.Zero) return;

            IntPtr[] absolutePidls = new IntPtr[paths.Length];
            IntPtr[] relativePidls = new IntPtr[paths.Length];
            IntPtr parentFolderPtr = IntPtr.Zero;

            try
            {
                Guid guidIShellFolder = new Guid("000214e6-0000-0000-c000-000000000046");
                for (int i = 0; i < paths.Length; i++)
                {
                    uint sfgaoOut;
                    int hrP = SHParseDisplayName(paths[i], IntPtr.Zero, out absolutePidls[i], 0, out sfgaoOut);
                    if (hrP != 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"[DEBUG_LOG] SHParseDisplayName failed for {paths[i]}: {hrP}");
                        continue;
                    }

                    IntPtr currentParentFolderPtr;
                    IntPtr relativePidl;
                    int hrB = SHBindToParent(absolutePidls[i], ref guidIShellFolder, out currentParentFolderPtr, out relativePidl);
                    if (hrB == 0)
                    {
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
                }

                if (parentFolderPtr == IntPtr.Zero) return;

                IShellFolder parentFolder = (IShellFolder)Marshal.GetTypedObjectForIUnknown(parentFolderPtr, typeof(IShellFolder));
                
                Guid guidIContextMenu = new Guid("000214e4-0000-0000-c000-000000000046");
                uint rgfReserved = 0;
                
                int hr = parentFolder.GetUIObjectOf(hwnd, (uint)relativePidls.Length, relativePidls, ref guidIContextMenu, ref rgfReserved, out IntPtr ppv);

                if (hr == 0 && ppv != IntPtr.Zero)
                {
                    try
                    {
                        IContextMenu menu = (IContextMenu)Marshal.GetTypedObjectForIUnknown(ppv, typeof(IContextMenu));

                        int hr2 = menu.QueryContextMenu(hMenu, 0, 1, 0x7FFF, CMF_EXPLORER);
                        if (hr2 < 0)
                        {
                            System.Diagnostics.Debug.WriteLine($"[DEBUG_LOG] QueryContextMenu failed: {hr2}");
                            return;
                        }

                        POINT pt;
                        if (!GetCursorPos(out pt))
                        {
                            pt = new POINT { x = 0, y = 0 };
                        }

                        uint cmdId = TrackPopupMenu(hMenu, TPM_RETURNCMD | TPM_LEFTALIGN, pt.x, pt.y, 0, hwnd, IntPtr.Zero);

                        if (cmdId > 0 && cmdId <= 0x7FFF)
                        {
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
                                    System.Diagnostics.Debug.WriteLine($"[DEBUG_LOG] InvokeCommand failed: {hr3}");
                                }
                            }
                            finally
                            {
                                Marshal.FreeHGlobal(iciPtr);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[DEBUG_LOG] Context menu error: {ex}");
                    }
                    finally
                    {
                        Marshal.Release(ppv);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG_LOG] ShellContextMenuHelper error: {ex}");
            }
            finally
            {
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
        }

        public static void ShowBackgroundContextMenu(IntPtr hwnd, string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath)) return;
            if (hwnd == IntPtr.Zero) return;

            IntPtr hMenu = CreatePopupMenu();
            if (hMenu == IntPtr.Zero) return;

            IntPtr folderPidl = IntPtr.Zero;
            IntPtr folderPtr = IntPtr.Zero;

            try
            {
                uint sfgaoOut;
                int hrP = SHParseDisplayName(folderPath, IntPtr.Zero, out folderPidl, 0, out sfgaoOut);
                if (hrP != 0) return;

                Guid guidIShellFolder = new Guid("000214e6-0000-0000-c000-000000000046");
                
                // Get IShellFolder for the folder itself
                IntPtr desktopFolderPtr;
                SHGetDesktopFolder(out desktopFolderPtr);
                IShellFolder desktopFolder = (IShellFolder)Marshal.GetTypedObjectForIUnknown(desktopFolderPtr, typeof(IShellFolder));
                
                int hrB = desktopFolder.BindToObject(folderPidl, IntPtr.Zero, ref guidIShellFolder, out folderPtr);
                Marshal.Release(desktopFolderPtr);

                if (hrB == 0 && folderPtr != IntPtr.Zero)
                {
                    IShellFolder folder = (IShellFolder)Marshal.GetTypedObjectForIUnknown(folderPtr, typeof(IShellFolder));
                    
                    Guid guidIContextMenu = new Guid("000214e4-0000-0000-c000-000000000046");
                    Guid guidIID_IContextMenu = new Guid("000214e4-0000-0000-c000-000000000046");
                    
                    int hrV = folder.CreateViewObject(hwnd, ref guidIID_IContextMenu, out IntPtr ppv);

                    if (hrV == 0 && ppv != IntPtr.Zero)
                    {
                        try
                        {
                            IContextMenu menu = (IContextMenu)Marshal.GetTypedObjectForIUnknown(ppv, typeof(IContextMenu));

                            int hr2 = menu.QueryContextMenu(hMenu, 0, 1, 0x7FFF, CMF_NORMAL | CMF_EXPLORER);
                            if (hr2 >= 0)
                            {
                                POINT pt;
                                if (!GetCursorPos(out pt))
                                {
                                    pt = new POINT { x = 0, y = 0 };
                                }

                                uint cmdId = TrackPopupMenu(hMenu, TPM_RETURNCMD | TPM_LEFTALIGN, pt.x, pt.y, 0, hwnd, IntPtr.Zero);

                                if (cmdId > 0 && cmdId <= 0x7FFF)
                                {
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
                                        menu.InvokeCommand(iciPtr);
                                    }
                                    finally
                                    {
                                        Marshal.FreeHGlobal(iciPtr);
                                    }
                                }
                            }
                        }
                        finally
                        {
                            Marshal.Release(ppv);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG_LOG] ShowBackgroundContextMenu error: {ex}");
            }
            finally
            {
                if (folderPidl != IntPtr.Zero) ILFree(folderPidl);
                if (folderPtr != IntPtr.Zero) Marshal.Release(folderPtr);
                DestroyMenu(hMenu);
            }
        }
    }
}
