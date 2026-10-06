using System.Runtime.InteropServices;
using System.Text;

namespace WorkspaceManager;

internal static class Native
{
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] public struct Placement { public int Length, Flags, ShowCommand; public POINT Minimum, Maximum; public RECT Normal; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] public struct DisplayDevice
    {
        public int Size;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Name;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Description;
        public int Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Id;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Key;
    }
    // MIB_IF_ROW2, Windows x64 ABI. Full buffer retained so GetIfEntry2 can populate every member.
    [StructLayout(LayoutKind.Explicit, Size = 1352)] public struct InterfaceRow
    {
        [FieldOffset(8)] public uint Index;
        [FieldOffset(1128)] public uint Type;
        [FieldOffset(1132)] public uint Tunnel;
        [FieldOffset(1156)] public byte Flags;
        [FieldOffset(1160)] public uint OperStatus;
    }
    [DllImport("iphlpapi.dll")] public static extern uint GetIfEntry2(ref InterfaceRow row);
    public delegate bool EnumCallback(nint window, nint parameter);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumCallback callback, nint parameter);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] public static extern bool IsWindow(nint window);
    [DllImport("user32.dll")] public static extern nint GetWindow(nint window, uint command);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] public static extern nint GetWindowLongPtr(nint window, int index);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(nint window, StringBuilder text, int count);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(nint window, StringBuilder text, int count);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(nint window, out uint process);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool GetWindowPlacement(nint window, ref Placement placement);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool GetWindowRect(nint window, out RECT rect);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool SetWindowPos(nint window, nint insertAfter, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool SetWindowPlacement(nint window, in Placement placement);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(nint window, int attribute, out int value, int size);
    [DllImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")] public static extern int DwmGetWindowAttributeRect(nint window, int attribute, out RECT value, int size);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern bool EnumDisplayDevices(string device, uint index, ref DisplayDevice data, uint flags);
    [DllImport("user32.dll")] public static extern nint MonitorFromPoint(POINT point, uint flags);
    [DllImport("shcore.dll")] public static extern int GetDpiForMonitor(nint monitor, int type, out uint x, out uint y);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool RegisterHotKey(nint window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(nint window, int id);
    [DllImport("user32.dll")] public static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(nint window);
    [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int key);
    [StructLayout(LayoutKind.Sequential)] public struct Keyboard { public ushort VirtualKey, Scan; public uint Flags, Time; public nuint Extra; }
    [StructLayout(LayoutKind.Explicit, Size = 40)] public struct Input { [FieldOffset(0)] public uint Type; [FieldOffset(8)] public Keyboard Keyboard; }
    [DllImport("user32.dll", SetLastError = true)] public static extern uint SendInput(uint count, Input[] inputs, int size);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] public struct Credential
    {
        public uint Flags, Type;
        public string Target;
        public string? Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME Written;
        public uint BlobSize;
        public nint Blob;
        public uint Persist, AttributeCount;
        public nint Attributes;
        public string? Alias;
        public string Username;
    }
    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)] public static extern bool CredWrite(ref Credential credential, uint flags);
    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)] public static extern bool CredRead(string target, uint type, uint flags, out nint credential);
    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)] public static extern bool CredDelete(string target, uint type, uint flags);
    [DllImport("advapi32.dll")] public static extern void CredFree(nint credential);
}
