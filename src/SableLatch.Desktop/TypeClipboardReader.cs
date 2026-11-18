using System.Runtime.InteropServices;

namespace SableLatch.Desktop;

internal static class TypeClipboardReader
{
    public static string Read()
    {
        if (!OpenClipboard(IntPtr.Zero)) throw new InvalidOperationException("The clipboard is busy. Try again.");
        try
        {
            var data = GetClipboardData(13);
            if (data == IntPtr.Zero) throw new InvalidOperationException("Copy some plain text before using Type clipboard.");
            var size = GlobalSize(data).ToUInt64();
            if (size is < 2 or > 200002 || size % 2 != 0) throw new InvalidOperationException("Type supports up to 100,000 characters at a time.");
            var pointer = GlobalLock(data);
            if (pointer == IntPtr.Zero) throw new InvalidOperationException("The clipboard text is unavailable. Try copying it again.");
            try
            {
                var text = Marshal.PtrToStringUni(pointer, (int)size / 2)!; var end = text.IndexOf('\0');
                if (end < 0) throw new InvalidOperationException("The clipboard text is invalid. Try copying it again.");
                return text[..end];
            }
            finally { GlobalUnlock(data); }
        }
        finally { CloseClipboard(); }
    }
    [DllImport("user32")] private static extern bool OpenClipboard(IntPtr window);
    [DllImport("user32")] private static extern bool CloseClipboard();
    [DllImport("user32")] private static extern IntPtr GetClipboardData(uint format);
    [DllImport("kernel32")] private static extern UIntPtr GlobalSize(IntPtr memory);
    [DllImport("kernel32")] private static extern IntPtr GlobalLock(IntPtr memory);
    [DllImport("kernel32")] private static extern bool GlobalUnlock(IntPtr memory);
}
