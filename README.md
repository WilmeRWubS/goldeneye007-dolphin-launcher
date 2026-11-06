# GoldenEye 007 (Wii) – Dolphin one-click launcher

Small Windows launchers that boot **GoldenEye 007 (Wii, PAL – SJBP52)** straight into
fullscreen in [Dolphin](https://dolphin-emu.org/), with keyboard and mouse controls already set up.
You don't need to touch Dolphin's UI.

There are two exes:

| Exe | Aiming |
|---|---|
| `GoldenEye 007.exe` | Wii Remote pointer. The mouse moves an on-screen crosshair, and the camera turns near the edges (Wii-like). |
| `GoldenEye 007 (Mouse Look).exe` | **PC-style mouse look.** Raw mouse movement is written straight into the player's yaw/pitch in game memory: 1:1, with no stick dead zone, acceleration or turn-speed cap. |

## What it does
- Starts Dolphin in batch mode (`-b -e <game>`). It uses its own Dolphin user folder (`GoldenEye007-User`, or
  `GoldenEye007-MouseLook-User` for Mouse Look) so your normal Dolphin settings aren't changed.
- It shares the Wii save data (NAND) with your normal Dolphin install, so your progress carries over.
- It writes a tuned config:
  - Pointer exe: an emulated **Wii Remote + Nunchuk**. The hidden mouse cursor, locked to the window, drives the IR
    pointer.
  - Mouse Look exe: an emulated **Classic Controller**. The mouse isn't mapped in Dolphin; it's injected (see below).
  - GameCube pads are disabled. Otherwise the game latches onto the keyboard-mapped GC pad instead of the Wii Remote.
  - 3× internal resolution, 4× MSAA, borderless fullscreen, VSync, async ubershaders.
  - `EFBScaledCopy = False`. This fixes white sheets of fog that flash over the top half of the screen at >1× IR.
  - Optional **60 FPS** Gecko code, on by default.

### How Mouse Look works
The Mouse Look exe keeps running next to Dolphin until Dolphin exits. It:
1. Finds Dolphin's emulated MEM1 (the `MEM_MAPPED` region that starts with game ID `SJBP52`).
2. Reads mouse deltas with Raw Input (`RIDEV_INPUTSINK`).
3. On every mouse event, while Dolphin is the foreground window, updates the player's view angles in place:

| What | Address (PAL SJBP52) |
|---|---|
| Local player object | `[0x805D446C]`, validated by vtable `0x80574784` |
| Yaw (radians) | `[player + 0x4] + 0xDC` |
| Pitch (radians, + = down, clamped to ±π/4) | `player + 0xC64` |
| Pause / menu flag (non-zero → injection paused) | `0x805C95E0` |

Vehicle sections (e.g. the Tank) don't use the on-foot player object, so mouse look is inactive there. Use I J K L instead.

## Controls: `GoldenEye 007.exe` (Wii Remote "Vesper" layout)
| Action | Key / mouse |
|---|---|
| Aim / turn | Mouse (pointer) |
| Fire | Left click |
| Aim down sights | Right click (also Q / E) |
| Lean | Hold Q / E (tilts the Nunchuk) |
| Move | WASD |
| Action / sprint / lock camera | Shift, F, Space, Enter |
| Crouch | Ctrl, C |
| Melee | Middle click, V |
| Reload | R |
| Grenade | G |
| Cycle weapon | Mouse wheel, Down arrow |
| Firing mode | B, Up arrow |
| Smartphone | X, Left arrow |
| Cycle grenades | H, Right arrow |
| Night vision / scoreboard | Tab, N |
| Pause | Esc |

## Controls: `GoldenEye 007 (Mouse Look).exe` (Classic Controller "Goldfinger Pro" layout)
| Action | Key / mouse |
|---|---|
| Look | Mouse (direct, 1:1) |
| Fire | Left click |
| Aim down sights | Right click (lowers sensitivity, see `MouseAdsMultiplier`) |
| Move | WASD |
| Action / sprint | Shift, F, Space |
| Crouch | Ctrl, C (Enter also works in menus) |
| Melee | Middle click, V |
| Reload | R |
| Grenade / cycle grenades | G / H |
| Cycle weapon | Mouse wheel, 1 / 2, Left / Right arrow |
| Firing mode / smartphone | B / X |
| Night vision / scoreboard | Tab, N |
| Pause | Esc |
| Vehicle / turret aim (fallback) | I J K L |

In the game's Control Settings, turn **ADS Snap** off so aiming down sights doesn't pull the camera onto targets.

Dolphin hotkeys: **Shift+Esc** stop, **Alt+Enter** fullscreen toggle, **F5 / F9** save / load state, **F10** pause
emulation, **F12** screenshot.

## Install
1. Build them (see below) or download the exes from the Releases page.
2. Put the exe(s) **in the Dolphin folder**, next to `Dolphin.exe`.
3. Run it. If the game image isn't found, you're asked to pick it. The choice is saved in `GoldenEye007.ini`.

### Options
- `--configure` opens Dolphin's normal UI on the launcher's profile so you can tweak graphics or
  controls.
- `--reset` restores the launcher's default config, then starts the game.
- `GoldenEye007.ini` (next to the exe):
  - `GamePath`: the disc image to boot.
  - `NANDPath`: where Wii saves are stored.
  - `SixtyFPS`: `True` / `False`. The 60 FPS code can get you stuck in the vents on *St. Petersburg: Archives*, and
    it makes the *Tank* mission's time limit impossible to meet.
  - `MouseSensitivity` (Mouse Look): same scale as CS/Source games (0.022° per mouse count × value). Default `2`.
  - `MouseAdsMultiplier` (Mouse Look): sensitivity multiplier while right mouse is held. Default `0.6`.
  - `MouseInvertY` (Mouse Look): `True` / `False`.

## Build
Requires only Windows (it uses the .NET Framework 4 `csc.exe` that ships with it).

```powershell
powershell -ExecutionPolicy Bypass -File .\src\build.ps1            # -> .\bin\GoldenEye 007.exe and .\bin\GoldenEye 007 (Mouse Look).exe
powershell -ExecutionPolicy Bypass -File .\src\build.ps1 -OutDir C:\path\to\Dolphin-x64
```

If you have a GoldenEye save in Dolphin, the build script extracts its save icon and uses it as the exe icon.

Only the PAL release (`SJBP52`) is supported by Mouse Look; the memory addresses differ on other regions.

No game files, Dolphin binaries or BIOS/NAND data are included in this repository.
