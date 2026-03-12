using System;
using System.IO;
using System.Runtime.InteropServices;

namespace ClawCage.WinUI.Services.Tools.Helper
{
    internal static class FolderPickerHelper
    {
        private const uint FOS_PICKFOLDERS = 0x00000020;
        private const uint FOS_FORCEFILESYSTEM = 0x00000040;
        private const int SIGDN_FILESYSPATH = unchecked((int)0x80058000);

        [DllImport("shell32", CharSet = CharSet.Unicode, PreserveSig = false)]
        private static extern void SHCreateItemFromParsingName(
            string pszPath, nint pbc, in Guid riid, out IShellItem ppv);

        [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"),
         InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItem
        {
            void BindToHandler(nint pbc, in Guid bhid, in Guid riid, out nint ppv);
            void GetParent(out IShellItem ppsi);
            void GetDisplayName(int sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
            void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
            void Compare(IShellItem psi, uint hint, out int piOrder);
        }

        // Flat vtable: IUnknown → IModalWindow → IFileDialog → IFileOpenDialog
        [ComImport, Guid("D57C7288-D4AD-4768-BE02-9D969532D960"),
         InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IFileOpenDialog
        {
            [PreserveSig] int Show(nint hwndOwner);
            void SetFileTypes(uint cFileTypes, nint rgFilterSpec);
            void SetFileTypeIndex(uint iFileType);
            void GetFileTypeIndex(out uint piFileType);
            void Advise(nint pfde, out uint pdwCookie);
            void Unadvise(uint dwCookie);
            void SetOptions(uint fos);
            void GetOptions(out uint pfos);
            void SetDefaultFolder(IShellItem psi);
            void SetFolder(IShellItem psi);
            void GetFolder(out IShellItem ppsi);
            void GetCurrentSelection(out IShellItem ppsi);
            void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);
            void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
            void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string pszText);
            void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
            void GetResult(out IShellItem ppsi);
            void AddPlace(IShellItem psi, int fdap);
            void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
            void Close(int hr);
            void SetClientGuid(in Guid guid);
            void ClearClientData();
            void SetFilter(nint pFilter);
            void GetResults(out nint ppenum);
            void GetSelectedItems(out nint ppsai);
        }

        [ComImport, Guid("DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7")]
        private class FileOpenDialogRCW { }

        /// <summary>
        /// Opens a modern shell folder-picker dialog.
        /// If <paramref name="startPath"/> is a valid directory the dialog opens there.
        /// Returns the chosen path, or <c>null</c> if the user cancelled.
        /// </summary>
        internal static string? PickFolder(nint ownerHwnd, string? startPath = null)
        {
            var dialog = (IFileOpenDialog)new FileOpenDialogRCW();
            try
            {
                dialog.GetOptions(out uint opts);
                dialog.SetOptions(opts | FOS_PICKFOLDERS | FOS_FORCEFILESYSTEM);
                dialog.SetTitle("选择路径");
                dialog.SetOkButtonLabel("选择此文件夹");

                if (!string.IsNullOrEmpty(startPath) && Directory.Exists(startPath))
                {
                    try
                    {
                        SHCreateItemFromParsingName(startPath, 0,
                            typeof(IShellItem).GUID, out var si);
                        dialog.SetFolder(si);
                    }
                    catch { /* non-critical; dialog opens at default location */ }
                }

                int hr = dialog.Show(ownerHwnd);
                if (hr != 0) return null; // user cancelled (0x800704C7) or other non-S_OK

                dialog.GetResult(out var result);
                result.GetDisplayName(SIGDN_FILESYSPATH, out string path);
                return path;
            }
            finally
            {
                Marshal.ReleaseComObject(dialog);
            }
        }
    }
}
