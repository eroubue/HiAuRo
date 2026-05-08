using System.Runtime.InteropServices;

namespace Browsingway;

internal enum WindowLongType
{
	GWL_WNDPROC = -4
}

internal enum WindowsMessage
{
	WM_KEYDOWN = 0x0100,
	WM_KEYUP = 0x0101,
	WM_CHAR = 0x0102,
	WM_SYSKEYDOWN = 0x0104,
	WM_SYSKEYUP = 0x0105,
	WM_SYSCHAR = 0x0106,

	WM_LBUTTONDOWN = 0x0201
}

internal class NativeMethods
{
	[DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
	public static extern IntPtr GetWindowLongPtr(IntPtr hWnd, WindowLongType nIndex);

	[DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
	public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, WindowLongType nIndex, IntPtr dwNewLong);

	[DllImport("user32.dll", EntryPoint = "CallWindowProcW")]
	public static extern long CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, ulong wParam, long lParam);
}