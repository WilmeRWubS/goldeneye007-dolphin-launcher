using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

// PC-style mouse look for GoldenEye 007 (Wii, PAL SJBP52): reads raw mouse deltas and writes the
// player's yaw/pitch straight into the emulated Wii RAM inside Dolphin, bypassing the game's
// stick handling (no dead zone, acceleration or turn-speed cap).
internal sealed class MouseInjector : NativeWindow, IDisposable
{
    // Game addresses (effective addresses, MEM1).
    private const uint PlayerPtr = 0x805D446C;    // global pointer to the local player object
    private const uint PlayerVtable = 0x80574784; // first word of a valid player object
    private const uint PlayerNodeOffset = 0x4;    // player -> scene node
    private const uint NodeYawOffset = 0xDC;      // node: Euler yaw (radians)
    private const uint PlayerPitchOffset = 0xC64; // player: view pitch (radians, + = down)
    private const uint MenuFlag = 0x805C95E0;     // 0 while in gameplay, non-zero in pause menus
    private const float PitchLimit = 0.7853982f;  // game clamps pitch to +-pi/4

    private const string GameId = "SJBP52";
    private const long Mem1Size = 0x1800000;

    private readonly Process _dolphin;
    private readonly double _radPerCount;
    private readonly double _adsMultiplier;
    private readonly bool _invertY;
    private readonly Timer _timer;
    private IntPtr _handle;
    private long _mem1;

    public MouseInjector(Process dolphin, double sensitivity, double adsMultiplier, bool invertY)
    {
        _dolphin = dolphin;
        // Same scale as Source/CS games: 0.022 degrees per mouse count at sensitivity 1.
        _radPerCount = sensitivity * 0.022 * Math.PI / 180.0;
        _adsMultiplier = adsMultiplier;
        _invertY = invertY;

        var cp = new CreateParams();
        cp.Parent = new IntPtr(-3); // HWND_MESSAGE
        CreateHandle(cp);

        var rid = new RAWINPUTDEVICE[1];
        rid[0].usUsagePage = 0x01;
        rid[0].usUsage = 0x02; // mouse
        rid[0].dwFlags = RIDEV_INPUTSINK;
        rid[0].hwndTarget = Handle;
        if (!RegisterRawInputDevices(rid, 1, (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICE))))
            throw new InvalidOperationException("RegisterRawInputDevices failed: " + Marshal.GetLastWin32Error());

        _timer = new Timer();
        _timer.Interval = 500;
        _timer.Tick += OnTick;
        _timer.Start();
    }

    // Runs a message loop until Dolphin exits.
    public static void Run(Process dolphin, double sensitivity, double adsMultiplier, bool invertY)
    {
        using (new MouseInjector(dolphin, sensitivity, adsMultiplier, invertY))
            Application.Run();
    }

    private void OnTick(object sender, EventArgs e)
    {
        if (_dolphin.HasExited)
        {
            Application.ExitThread();
            return;
        }
        if (_handle == IntPtr.Zero)
            _handle = OpenProcess(PROCESS_VM_READ | PROCESS_VM_WRITE | PROCESS_VM_OPERATION | PROCESS_QUERY_INFORMATION,
                                  false, _dolphin.Id);
        if (_handle != IntPtr.Zero && (_mem1 == 0 || !GameIdMatches(_mem1)))
            _mem1 = FindMem1();
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_INPUT)
            OnRawInput(m.LParam);
        base.WndProc(ref m);
    }

    private readonly byte[] _rawBuffer = new byte[128];

    private void OnRawInput(IntPtr hRawInput)
    {
        uint size = (uint)_rawBuffer.Length;
        uint headerSize = (uint)Marshal.SizeOf(typeof(RAWINPUTHEADER));
        if (GetRawInputData(hRawInput, RID_INPUT, _rawBuffer, ref size, headerSize) == unchecked((uint)-1))
            return;
        if (BitConverter.ToUInt32(_rawBuffer, 0) != RIM_TYPEMOUSE)
            return;

        int o = (int)headerSize;
        ushort flags = BitConverter.ToUInt16(_rawBuffer, o);
        if ((flags & MOUSE_MOVE_ABSOLUTE) != 0)
            return;
        int dx = BitConverter.ToInt32(_rawBuffer, o + 12);
        int dy = BitConverter.ToInt32(_rawBuffer, o + 16);
        if (dx != 0 || dy != 0)
            Apply(dx, dy);
    }

    private void Apply(int dx, int dy)
    {
        if (_mem1 == 0 || !DolphinHasFocus())
            return;
        uint menu;
        if (!ReadU32(MenuFlag, out menu) || menu != 0)
            return;

        uint player, vtable, node;
        if (!ReadU32(PlayerPtr, out player) || !IsMem1(player))
            return;
        if (!ReadU32(player, out vtable) || vtable != PlayerVtable)
            return;
        if (!ReadU32(player + PlayerNodeOffset, out node) || !IsMem1(node))
            return;

        float yaw, pitch;
        if (!ReadF32(node + NodeYawOffset, out yaw) || !ReadF32(player + PlayerPitchOffset, out pitch))
            return;
        if (float.IsNaN(yaw) || float.IsNaN(pitch))
            return;

        double scale = _radPerCount;
        if ((GetAsyncKeyState(VK_RBUTTON) & 0x8000) != 0)
            scale *= _adsMultiplier;

        double newYaw = yaw + dx * scale;
        if (newYaw > Math.PI) newYaw -= 2 * Math.PI;
        else if (newYaw < -Math.PI) newYaw += 2 * Math.PI;
        double newPitch = pitch + (_invertY ? -dy : dy) * scale;
        newPitch = Math.Max(-PitchLimit, Math.Min(PitchLimit, newPitch));

        if (dx != 0)
            WriteF32(node + NodeYawOffset, (float)newYaw);
        if (dy != 0)
            WriteF32(player + PlayerPitchOffset, (float)newPitch);
    }

    private bool DolphinHasFocus()
    {
        uint pid;
        GetWindowThreadProcessId(GetForegroundWindow(), out pid);
        return pid == (uint)_dolphin.Id;
    }

    private static bool IsMem1(uint ea)
    {
        return ea >= 0x80000000 && ea < 0x80000000 + Mem1Size;
    }

    private long FindMem1()
    {
        long addr = 0;
        var mbi = new MEMORY_BASIC_INFORMATION();
        uint mbiSize = (uint)Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION));
        while (VirtualQueryEx(_handle, new IntPtr(addr), out mbi, mbiSize) != IntPtr.Zero)
        {
            long baseAddr = mbi.BaseAddress.ToInt64();
            long regionSize = mbi.RegionSize.ToInt64();
            if (mbi.State == MEM_COMMIT && mbi.Type == MEM_MAPPED && regionSize >= Mem1Size && GameIdMatches(baseAddr))
                return baseAddr;
            addr = baseAddr + regionSize;
            if (regionSize <= 0 || addr >= 0x7FFFFFFFFFFF)
                break;
        }
        return 0;
    }

    private readonly byte[] _idBuffer = new byte[6];

    private bool GameIdMatches(long hostAddr)
    {
        IntPtr read;
        return ReadProcessMemory(_handle, new IntPtr(hostAddr), _idBuffer, 6, out read) &&
               Encoding.ASCII.GetString(_idBuffer) == GameId;
    }

    private readonly byte[] _word = new byte[4];

    private bool ReadU32(uint ea, out uint value)
    {
        IntPtr read;
        value = 0;
        if (!ReadProcessMemory(_handle, new IntPtr(_mem1 + (ea & 0x3FFFFFFF)), _word, 4, out read))
            return false;
        value = (uint)(_word[0] << 24 | _word[1] << 16 | _word[2] << 8 | _word[3]);
        return true;
    }

    private bool ReadF32(uint ea, out float value)
    {
        uint raw;
        value = 0;
        if (!ReadU32(ea, out raw))
            return false;
        value = BitConverter.ToSingle(BitConverter.GetBytes(raw), 0);
        return true;
    }

    private void WriteF32(uint ea, float value)
    {
        byte[] b = BitConverter.GetBytes(value);
        Array.Reverse(b);
        IntPtr written;
        WriteProcessMemory(_handle, new IntPtr(_mem1 + (ea & 0x3FFFFFFF)), b, 4, out written);
    }

    public void Dispose()
    {
        _timer.Dispose();
        if (_handle != IntPtr.Zero)
            CloseHandle(_handle);
        DestroyHandle();
    }

    private const int WM_INPUT = 0x00FF;
    private const uint RID_INPUT = 0x10000003;
    private const uint RIM_TYPEMOUSE = 0;
    private const uint RIDEV_INPUTSINK = 0x00000100;
    private const ushort MOUSE_MOVE_ABSOLUTE = 0x01;
    private const int VK_RBUTTON = 0x02;
    private const uint PROCESS_VM_OPERATION = 0x0008;
    private const uint PROCESS_VM_READ = 0x0010;
    private const uint PROCESS_VM_WRITE = 0x0020;
    private const uint PROCESS_QUERY_INFORMATION = 0x0400;
    private const uint MEM_COMMIT = 0x1000;
    private const uint MEM_MAPPED = 0x40000;

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUTDEVICE
    {
        public ushort usUsagePage;
        public ushort usUsage;
        public uint dwFlags;
        public IntPtr hwndTarget;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUTHEADER
    {
        public uint dwType;
        public uint dwSize;
        public IntPtr hDevice;
        public IntPtr wParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORY_BASIC_INFORMATION
    {
        public IntPtr BaseAddress;
        public IntPtr AllocationBase;
        public uint AllocationProtect;
        public IntPtr RegionSize;
        public uint State;
        public uint Protect;
        public uint Type;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] devices, uint count, uint size);

    [DllImport("user32.dll")]
    private static extern uint GetRawInputData(IntPtr hRawInput, uint command, byte[] data, ref uint size, uint headerSize);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vk);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, bool inherit, int pid);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll")]
    private static extern IntPtr VirtualQueryEx(IntPtr process, IntPtr address, out MEMORY_BASIC_INFORMATION info, uint length);

    [DllImport("kernel32.dll")]
    private static extern bool ReadProcessMemory(IntPtr process, IntPtr address, byte[] buffer, int size, out IntPtr read);

    [DllImport("kernel32.dll")]
    private static extern bool WriteProcessMemory(IntPtr process, IntPtr address, byte[] buffer, int size, out IntPtr written);
}
