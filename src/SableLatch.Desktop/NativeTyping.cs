using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace SableLatch.Desktop;

// Exists only during an explicit Type operation. No resident hook, polling worker or text log.
internal sealed class NativeTyping : IDisposable
{
    private readonly CancellationTokenSource stop = new();
    private readonly HookCallback mouseCallback, keyCallback;
    private IntPtr mouseHook, keyHook;
    private IntPtr foregroundHook, expectedTarget;
    private readonly ForegroundCallback foregroundCallback;
    private bool cursorsChanged, disposed;
    private readonly TaskCompletionSource<IntPtr> selection = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public CancellationToken Token => stop.Token;
    public NativeTyping()
    {
        mouseCallback = Mouse; keyCallback = Keyboard;
        foregroundCallback = (_, _, window, _, _, _, _) => { if (expectedTarget != IntPtr.Zero && window != expectedTarget) Cancel(); };
        keyHook = SetWindowsHookEx(13, keyCallback, GetModuleHandle(null), 0);
        if (keyHook == IntPtr.Zero) { stop.Dispose(); throw new Win32Exception(); }
    }
    public Task<IntPtr> SelectTarget()
    {
        mouseHook = SetWindowsHookEx(14, mouseCallback, GetModuleHandle(null), 0);
        if (mouseHook == IntPtr.Zero) throw new Win32Exception();
        cursorsChanged = true;
        foreach (var id in new uint[] { 32512, 32513, 32649 })
        {
            var cursor = CopyIcon(LoadCursor(IntPtr.Zero, new IntPtr(32515)));
            if (cursor == IntPtr.Zero || !SetSystemCursor(cursor, id)) { if (cursor != IntPtr.Zero) DestroyCursor(cursor); throw new Win32Exception(); }
        }
        return selection.Task.WaitAsync(Token);
    }
    private IntPtr Mouse(int code, IntPtr message, IntPtr data)
    {
        if (code >= 0 && message.ToInt32() == 0x0202)
        {
            var point = Marshal.PtrToStructure<MouseData>(data).Point;
            var root = GetAncestor(WindowFromPoint(point), 2);
            EndSelection(); selection.TrySetResult(root);
        }
        return CallNextHookEx(IntPtr.Zero, code, message, data);
    }
    private IntPtr Keyboard(int code, IntPtr message, IntPtr data)
    {
#if DEBUG
        if (code >= 0) ObserveKeyboard?.Invoke(Marshal.ReadInt32(data), Marshal.ReadInt32(data, 4), (uint)Marshal.ReadInt32(data, 8));
#endif
        if (code >= 0 && Marshal.ReadInt32(data) == 27)
        {
            if (message.ToInt32() is 0x0100 or 0x0104) Cancel();
            return new IntPtr(1);
        }
        return CallNextHookEx(IntPtr.Zero, code, message, data);
    }
#if DEBUG
    internal Action<int, int, uint>? ObserveKeyboard;
#endif
    public void Cancel() { if (!disposed) { stop.Cancel(); EndSelection(); } }
    private void EndSelection()
    {
        if (mouseHook != IntPtr.Zero) { UnhookWindowsHookEx(mouseHook); mouseHook = IntPtr.Zero; }
        if (cursorsChanged) { SystemParametersInfo(0x0057, 0, IntPtr.Zero, 0); cursorsChanged = false; }
    }
    public async Task TypeTo(IntPtr target, string text, TypeClipboardOptions options)
    {
        expectedTarget = target;
        foregroundHook = SetWinEventHook(3, 3, IntPtr.Zero, foregroundCallback, 0, 0, 0);
        if (foregroundHook == IntPtr.Zero) throw new Win32Exception();
        // Let the selected click reach Windows before fixing the destination identity.
        await Task.Delay(60, Token);
        if (target == IntPtr.Zero) throw new InvalidOperationException("Choose an application window.");
        GetWindowThreadProcessId(target, out var process);
        bool Matches() => GetForegroundWindow() == target && GetWindowThreadProcessId(target, out var current) != 0 && current == process;
        if (!Matches()) throw new InvalidOperationException("The chosen window did not receive focus. Try again.");
        var layout = GetKeyboardLayout(GetWindowThreadProcessId(target, out _));
        // Validate compatibility before sending anything; do not silently switch modes mid-password.
        if (options.InputMode == "keyboard") foreach (var unit in TypeClipboardText.Units(text)) if (unit is not ("\n" or "\t") && (unit.Length != 1 || VkKeyScanEx(unit[0], layout) == -1)) throw new InvalidOperationException("This text cannot be typed with the destination keyboard layout. Choose Unicode text in Type settings.");
        await TypeClipboardRunner.Run(text, options, Matches, unit => Send(unit, options.InputMode, layout), Token);
    }
    private static void Send(string unit, string mode, IntPtr layout)
    {
        // Physical modifiers can turn otherwise ordinary text into shortcuts.
        if (new[] { 16, 17, 18, 91, 92 }.Any(k => (GetAsyncKeyState(k) & 0x8000) != 0)) throw new InvalidOperationException("Typing stopped. Release Shift, Ctrl, Alt and Windows keys, then try again.");
        var inputs = new List<Input>();
        void Key(ushort key, bool up)
        {
            var scan = MapVirtualKeyEx(key, 4, layout);
            // Supply BOTH VK and scan code. Remote-control clients often inspect VK events,
            // whereas browser consoles may use the physical scan code. Do not set SCANCODE.
            inputs.Add(new Input { Type = 1, Data = new InputData { Keyboard = new KeyboardInput { VirtualKey = key, Scan = (ushort)(scan & 255), Flags = (up ? 2u : 0) | ((scan & 0xFF00) == 0xE000 ? 1u : 0) } } });
        }
        if (unit is "\n" or "\t") { Key(unit == "\n" ? (ushort)13 : (ushort)9, false); Key(unit == "\n" ? (ushort)13 : (ushort)9, true); }
        else if (mode == "unicode" || mode == "auto" && (unit.Length != 1 || VkKeyScanEx(unit[0], layout) == -1))
        {
            foreach (var ch in unit) foreach (var up in new[] { false, true }) inputs.Add(new Input { Type = 1, Data = new InputData { Keyboard = new KeyboardInput { Scan = ch, Flags = 4u | (up ? 2u : 0) } } });
        }
        else
        {
            var mapped = VkKeyScanEx(unit[0], layout); var modifiers = mapped >> 8;
            var held = new List<ushort>();
            if ((modifiers & 1) != 0) held.Add(0xA0); // Left Shift
            if ((modifiers & 6) == 6) held.Add(0xA5); // AltGr: right Alt with extended scan code
            else { if ((modifiers & 2) != 0) held.Add(0xA2); if ((modifiers & 4) != 0) held.Add(0xA5); }
            foreach (var key in held) Key(key, false);
            Key((ushort)(mapped & 255), false); Key((ushort)(mapped & 255), true);
            foreach (var key in held.AsEnumerable().Reverse()) Key(key, true);
        }
        var sent = SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<Input>());
        if (sent != inputs.Count)
        {
            // Best-effort release for synthetic modifiers after a partial OS acceptance.
            var releases = inputs.Where(i => (i.Data.Keyboard.Flags & 2) != 0).ToArray();
            if (sent > 0) SendInput((uint)releases.Length, releases, Marshal.SizeOf<Input>());
            throw new InvalidOperationException("Windows blocked typing. The destination may require higher privileges.");
        }
    }
    public void Dispose() { if (disposed) return; Cancel(); if (keyHook != IntPtr.Zero) UnhookWindowsHookEx(keyHook); if (foregroundHook != IntPtr.Zero) UnhookWinEvent(foregroundHook); keyHook = foregroundHook = IntPtr.Zero; stop.Dispose(); disposed = true; }
    private delegate void ForegroundCallback(IntPtr hook, uint eventId, IntPtr window, int objectId, int childId, uint thread, uint time);
    [DllImport("user32")] private static extern IntPtr SetWinEventHook(uint min, uint max, IntPtr module, ForegroundCallback callback, uint process, uint thread, uint flags);
    [DllImport("user32")] private static extern bool UnhookWinEvent(IntPtr hook);
    private delegate IntPtr HookCallback(int code, IntPtr message, IntPtr data);
    [StructLayout(LayoutKind.Sequential)] private struct PointNative { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct MouseData { public PointNative Point; public uint Data, Flags, Time; public UIntPtr Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct Input { public uint Type; public InputData Data; }
    [StructLayout(LayoutKind.Explicit)] private struct InputData { [FieldOffset(0)] public KeyboardInput Keyboard; [FieldOffset(0)] public MouseInput Mouse; }
    [StructLayout(LayoutKind.Sequential)] private struct KeyboardInput { public ushort VirtualKey, Scan; public uint Flags, Time; public UIntPtr Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct MouseInput { public int X, Y; public uint Data, Flags, Time; public UIntPtr Extra; }
    [DllImport("user32", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int id, HookCallback callback, IntPtr module, uint thread);
    [DllImport("user32")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
    [DllImport("kernel32", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string? name);
    [DllImport("user32")] private static extern IntPtr WindowFromPoint(PointNative point);
    [DllImport("user32")] private static extern IntPtr GetAncestor(IntPtr window, uint flags);
    [DllImport("user32")] internal static extern IntPtr GetForegroundWindow();
    [DllImport("user32")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    [DllImport("user32")] private static extern IntPtr GetKeyboardLayout(uint thread);
    [DllImport("user32", CharSet = CharSet.Unicode)] private static extern short VkKeyScanEx(char character, IntPtr layout);
    [DllImport("user32", CharSet = CharSet.Unicode)] private static extern uint MapVirtualKeyEx(uint key, uint mode, IntPtr layout);
    [DllImport("user32")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32", SetLastError = true)] private static extern uint SendInput(uint count, Input[] inputs, int size);
    [DllImport("user32")] private static extern IntPtr LoadCursor(IntPtr instance, IntPtr name);
    [DllImport("user32")] private static extern IntPtr CopyIcon(IntPtr cursor);
    [DllImport("user32")] private static extern bool SetSystemCursor(IntPtr cursor, uint id);
    [DllImport("user32")] private static extern bool DestroyCursor(IntPtr cursor);
    [DllImport("user32")] private static extern bool SystemParametersInfo(uint action, uint parameter, IntPtr data, uint flags);
}
