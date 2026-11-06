using System;
using System.IO;
using System.Text;

// Default Dolphin configuration written into the launcher's private user folder.
internal static class DolphinConfig
{
    public static void WriteDefaults(string userDir, bool mouseLook)
    {
        string configDir = Path.Combine(userDir, "Config");
        string gameSettingsDir = Path.Combine(userDir, "GameSettings");
        Directory.CreateDirectory(configDir);
        Directory.CreateDirectory(gameSettingsDir);

        Write(configDir, "WiimoteNew.ini", mouseLook ? WiimoteMouseLook : WiimotePointer);
        Write(configDir, "Hotkeys.ini", Hotkeys);
        Write(configDir, "GFX.ini", Gfx);
        Write(gameSettingsDir, "SJBP52.ini", GameSettings);

        // Merge into Dolphin.ini so Dolphin's own bookkeeping (analytics ID, MAC, etc.) survives a reset.
        string dolphinIni = Path.Combine(configDir, "Dolphin.ini");
        Ini main = Ini.Load(dolphinIni);
        foreach (string[] s in MainSettings)
            main.Set(s[0], s[1], s[2]);
        main.Save(dolphinIni, null);
    }

    // Enables/disables the 60 FPS Gecko code in the user game INI.
    public static void SetSixtyFps(string userDir, bool enabled)
    {
        string path = Path.Combine(Path.Combine(userDir, "GameSettings"), "SJBP52.ini");
        string text = File.Exists(path) ? File.ReadAllText(path) : GameSettings;
        if (text.IndexOf("[Gecko]", StringComparison.OrdinalIgnoreCase) < 0)
            text = GameSettings;

        const string enabledBlock = "[Gecko_Enabled]\r\n$60 FPS\r\n";
        text = text.Replace("\r\n", "\n").Replace("\n", "\r\n");
        text = text.Replace(enabledBlock, "").Replace("[Gecko_Enabled]\r\n", "");
        if (enabled)
            text = text.TrimEnd() + "\r\n" + enabledBlock;
        File.WriteAllText(path, text, new UTF8Encoding(false));
    }

    private static void Write(string dir, string name, string content)
    {
        string normalized = content.Replace("\r\n", "\n").Replace("\n", "\r\n");
        File.WriteAllText(Path.Combine(dir, name), normalized, new UTF8Encoding(false));
    }

    private static readonly string[][] MainSettings =
    {
        new[] { "Analytics", "Enabled", "False" },
        new[] { "Analytics", "PermissionAsked", "True" },
        // No GameCube pads: GoldenEye otherwise latches onto the (keyboard-mapped) GC pad
        // instead of the Wii Remote.
        new[] { "Core", "SIDevice0", "0" },
        new[] { "Core", "SIDevice1", "0" },
        new[] { "Core", "SIDevice2", "0" },
        new[] { "Core", "SIDevice3", "0" },
        new[] { "Core", "EnableCheats", "True" },
        new[] { "Core", "WiimoteContinuousScanning", "False" },
        new[] { "Core", "WiimoteEnableSpeaker", "True" },
        new[] { "Core", "CPUThread", "True" },
        new[] { "Display", "Fullscreen", "True" },
        new[] { "Display", "DisableScreenSaver", "True" },
        new[] { "Display", "RenderToMain", "False" },
        new[] { "General", "HotkeysRequireFocus", "True" },
        new[] { "Input", "BackgroundInput", "False" },
        new[] { "Interface", "ConfirmStop", "False" },
        new[] { "Interface", "LockCursor", "True" },
        new[] { "Interface", "CursorVisibility", "0" },
        new[] { "Interface", "PauseOnFocusLost", "True" },
        new[] { "Interface", "OnScreenDisplayMessages", "True" },
    };

    // Wii Remote + Nunchuk, matching GoldenEye's default "Vesper" button layout / gestures:
    //   B fire, Nunchuk Z aim down sight, C crouch, A action/sprint/lock camera, - reload,
    //   + grenade, 1 pause, 2 night vision/scoreboard, D-pad left smartphone, up firing mode,
    //   right cycle grenades, down cycle weapon, shake Nunchuk melee, hold Z + twist Nunchuk lean.
    // The pointer follows the (locked, hidden) mouse cursor 1:1.
    private const string WiimotePointer =
@"[Wiimote1]
Device = DInput/0/Keyboard Mouse
Source = 1
Buttons/A = LSHIFT | F | SPACE | RETURN
Buttons/B = `Click 0`
Buttons/1 = ESCAPE
Buttons/2 = TAB | N
Buttons/- = R
Buttons/+ = G
Buttons/Home = HOME
D-Pad/Up = B | UP
D-Pad/Down = `Axis Z+` | `Axis Z-` | DOWN | `Click 3` | `Click 4`
D-Pad/Left = X | LEFT
D-Pad/Right = H | RIGHT
IR/Up = `Cursor Y-`
IR/Down = `Cursor Y+`
IR/Left = `Cursor X-`
IR/Right = `Cursor X+`
Extension = Nunchuk
Extension/Attach MotionPlus = False
Nunchuk/Buttons/C = LCONTROL | C
Nunchuk/Buttons/Z = `Click 1` | Q | E
Nunchuk/Stick/Up = W
Nunchuk/Stick/Down = S
Nunchuk/Stick/Left = A
Nunchuk/Stick/Right = D
Nunchuk/Stick/Calibration = 100.00 141.42 100.00 141.42 100.00 141.42 100.00 141.42
Nunchuk/Tilt/Left = Q
Nunchuk/Tilt/Right = E
Nunchuk/Shake/X = V | `Click 2`
Nunchuk/Shake/Y = V | `Click 2`
Nunchuk/Shake/Z = V | `Click 2`
" + OtherWiimotes;

    // Classic Controller (dual analog), matching GoldenEye's default "Goldfinger Pro" layout:
    //   ZR fire, ZL aim down sight, L melee, R grenade, a crouch, b action/sprint, Y reload,
    //   X cycle grenades, D-pad up firing mode, left/right cycle weapon, down smartphone,
    //   - night vision/scoreboard, + pause. Camera look is written directly to game memory by\r
    //   MouseInjector; the right stick is only a keyboard fallback (IJKL) for vehicles/turrets.
    private const string WiimoteMouseLook =
@"[Wiimote1]
Device = DInput/0/Keyboard Mouse
Source = 1
Buttons/Home = HOME
Extension = Classic
Extension/Attach MotionPlus = False
Classic/Buttons/A = LCONTROL | C | RETURN
Classic/Buttons/B = LSHIFT | F | SPACE | BACK
Classic/Buttons/X = H
Classic/Buttons/Y = R
Classic/Buttons/ZL = `Click 1`
Classic/Buttons/ZR = `Click 0`
Classic/Buttons/- = TAB | N
Classic/Buttons/+ = ESCAPE
Classic/Buttons/Home = HOME
Classic/Triggers/L = V | `Click 2`
Classic/Triggers/R = G
Classic/Triggers/L-Analog = V | `Click 2`
Classic/Triggers/R-Analog = G
Classic/D-Pad/Up = B | UP
Classic/D-Pad/Down = X | DOWN
Classic/D-Pad/Left = `Axis Z-` | `Click 3` | `1` | LEFT
Classic/D-Pad/Right = `Axis Z+` | `Click 4` | `2` | RIGHT
Classic/Left Stick/Up = W
Classic/Left Stick/Down = S
Classic/Left Stick/Left = A
Classic/Left Stick/Right = D
Classic/Left Stick/Calibration = 100.00 141.42 100.00 141.42 100.00 141.42 100.00 141.42
Classic/Right Stick/Up = I
Classic/Right Stick/Down = K
Classic/Right Stick/Left = J
Classic/Right Stick/Right = L
Classic/Right Stick/Calibration = 100.00 141.42 100.00 141.42 100.00 141.42 100.00 141.42
" + OtherWiimotes;

    private const string OtherWiimotes =
@"[Wiimote2]
Device = DInput/0/Keyboard Mouse
Source = 0
[Wiimote3]
Device = DInput/0/Keyboard Mouse
Source = 0
[Wiimote4]
Device = DInput/0/Keyboard Mouse
Source = 0
[BalanceBoard]
Device = DInput/0/Keyboard Mouse
Source = 0
";

    // Esc is the in-game pause, so Dolphin's default Esc=Stop moves to Shift+Esc,
    // and Tab (scoreboard) no longer toggles the speed limit.
    private const string Hotkeys =
@"[Hotkeys]
Device = DInput/0/Keyboard Mouse
General/Toggle Pause = F10
General/Stop = @(Shift+ESCAPE)
General/Toggle Fullscreen = @(Alt+RETURN)
General/Take Screenshot = F12
Load State/Load State Slot 1 = F9
Save State/Save State Slot 1 = F5
";

    // EFBScaledCopy must stay off: with scaled EFB copies the game's fog/haze effect
    // intermittently renders as a white sheet over the upper screen at >1x resolution.
    private const string Gfx =
@"[Hardware]
VSync = True
[Settings]
InternalResolution = 3
MSAA = 4
SSAA = False
AspectRatio = 0
BorderlessFullscreen = True
ShaderCompilationMode = 2
WaitForShadersBeforeStarting = True
ShowFPS = False
[Enhancements]
MaxAnisotropy = 4
ForceTrueColor = True
[Hacks]
EFBScaledCopy = False
";

    // 60 FPS code (PAL) from the Dolphin wiki. Known side effects: possible stuck in the vents
    // on St. Petersburg: Archives, and the Tank mission timer becomes impossible.
    private const string GameSettings =
@"[Gecko]
$60 FPS
046E9ECC 0000003C
[Gecko_Enabled]
$60 FPS
";
}
