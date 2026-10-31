using System;
using System.Runtime.InteropServices;
using System.Text;

namespace FnShare.ShellExtension
{
    // IShellExtInit，GUID 为 COM 标准值
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214e8-0000-0000-c000-000000000046")]
    public interface IShellExtInit
    {
        void Initialize(IntPtr pidlFolder, IDataObject pDataObj, IntPtr hKeyProgID);
    }

    // IDataObject（OLE），仅实现本扩展用到的 GetData 子集；
    // 其余方法声明齐全以保持 vtable 布局正确，但本扩展不调用它们。
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("0000010e-0000-0000-C000-000000000046")]
    public interface IDataObject
    {
        void GetData(ref FORMATETC format, out STGMEDIUM medium);
        void GetDataHere(ref FORMATETC format, ref STGMEDIUM medium);
        [PreserveSig]
        int QueryGetData(ref FORMATETC format);
        [PreserveSig]
        int GetCanonicalFormatEtc(ref FORMATETC formatIn, out FORMATETC formatOut);
        void SetData(ref FORMATETC format, ref STGMEDIUM medium, bool release);
        [PreserveSig]
        int EnumFormatEtc(uint direction, out IntPtr enumFormat);
        [PreserveSig]
        int DAdvise(ref FORMATETC format, uint advf, IntPtr advSink, out uint connection);
        void DUnadvise(uint connection);
        [PreserveSig]
        int EnumDAdvise(out IntPtr enumAdvise);
    }

    // IContextMenu，GUID 为 COM 标准值
    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214e4-0000-0000-c000-000000000046")]
    public interface IContextMenu
    {
        [PreserveSig]
        int QueryContextMenu(IntPtr hmenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags);
        [PreserveSig]
        int InvokeCommand(IntPtr pici);
        [PreserveSig]
        int GetCommandString(UIntPtr idCmd, uint uType, IntPtr pReserved, IntPtr pszName, uint cchMax);
    }

    // FORMATETC：cfFormat 用 int，与原生 CLIPFORMAT（UINT）布局一致
    [StructLayout(LayoutKind.Sequential)]
    public struct FORMATETC
    {
        public int cfFormat;
        public IntPtr ptd;
        public uint dwAspect;
        public int lindex;
        public uint tymed;
    }

    // STGMEDIUM：tymed + 联合体（HGLOBAL）+ pUnkForRelease
    [StructLayout(LayoutKind.Sequential)]
    public struct STGMEDIUM
    {
        public uint tymed;
        public IntPtr unionmember;
        public IntPtr pUnkForRelease;
    }

    // CMINVOKECOMMANDINFO（经典结构；Explorer 也可能传 EX 结构，
    // 但前段字段一致，只读前段是安全的）
    [StructLayout(LayoutKind.Sequential)]
    public struct CMINVOKECOMMANDINFO
    {
        public uint cbSize;
        public uint fMask;
        public IntPtr hwnd;
        public IntPtr lpVerb;
        public IntPtr lpParameters;
        public IntPtr lpDirectory;
        public int nShow;
        public uint dwHotKey;
        public IntPtr hIcon;
    }

    internal static class NativeMethods
    {
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        internal static extern uint DragQueryFile(IntPtr hDrop, uint iFile, StringBuilder lpszFile, int cch);

        [DllImport("ole32.dll")]
        internal static extern void ReleaseStgMedium(ref STGMEDIUM medium);

        [DllImport("user32.dll")]
        internal static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern bool AppendMenu(IntPtr hMenu, uint uFlags, UIntPtr uIDNewItem, string lpNewItem);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern bool InsertMenu(IntPtr hMenu, uint uPosition, uint uFlags, UIntPtr uIDNewItem, string lpNewItem);

        [DllImport("user32.dll")]
        internal static extern bool DestroyMenu(IntPtr hMenu);

        [DllImport("user32.dll")]
        internal static extern bool RemoveMenu(IntPtr hMenu, uint uPosition, uint uFlags);

        // 给已有菜单项配位图：uFlags 取 MF_BYCOMMAND（按命令 id）或 MF_BYPOSITION（按位置）。
        [DllImport("user32.dll")]
        internal static extern bool SetMenuItemBitmaps(IntPtr hMenu, uint uPosition, uint uFlags, IntPtr hBitmapUnchecked, IntPtr hBitmapChecked);

        [DllImport("gdi32.dll")]
        internal static extern bool DeleteObject(IntPtr hObject);
    }
}
