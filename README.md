# GoldenEye 007 (Wii) – Dolphin one-click launcher

A small Windows launcher (`GoldenEye 007.exe`) that boots **GoldenEye 007 (Wii, PAL – SJBP52)** straight into
fullscreen in [Dolphin](https://dolphin-emu.org/), with keyboard and mouse controls already set up.
You don't need to touch Dolphin's UI.

## What it does
- Starts Dolphin in batch mode (`-b -e <game>`). It uses its own Dolphin user folder (`GoldenEye007-User`) so your
  normal Dolphin settings aren't changed.
- It shares the Wii save data (NAND) with your normal Dolphin install, so your progress carries over.
- It writes a tuned config:
  - Emulated **Wii Remote + Nunchuk**. The hidden mouse cursor, locked to the window, drives the IR pointer.
  - GameCube pads are disabled. Otherwise the game latches onto the keyboard-mapped GC pad instead of the Wii Remote.
  - 3× internal resolution, 4× MSAA, borderless fullscreen, VSync, async ubershaders.
  - `EFBScaledCopy = False`. This fixes white sheets of fog that flash over the top half of the screen at >1× IR.
  - Optional **60 FPS** Gecko code, on by default.

## Controls (Wii Remote "Vesper" layout)
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

Dolphin hotkeys: **Shift+Esc** stop, **Alt+Enter** fullscreen toggle, **F5 / F9** save / load state, **F10** pause
emulation, **F12** screenshot.

## Install
1. Build it (see below) or download `GoldenEye 007.exe` from the Releases page.
2. Put `GoldenEye 007.exe` **in the Dolphin folder**, next to `Dolphin.exe`.
3. Run it. If the game image isn't found, you're asked to pick it. The choice is saved in `GoldenEye007.ini`.

### Options
- `GoldenEye 007.exe --configure` opens Dolphin's normal UI on the launcher's profile so you can tweak graphics or
  controls.
- `GoldenEye 007.exe --reset` restores the launcher's default config, then starts the game.
- `GoldenEye007.ini` (next to the exe):
  - `GamePath`: the disc image to boot.
  - `NANDPath`: where Wii saves are stored.
  - `SixtyFPS`: `True` / `False`. The 60 FPS code can get you stuck in the vents on *St. Petersburg: Archives*, and
    it makes the *Tank* mission's time limit impossible to meet.

## Build
Requires only Windows (it uses the .NET Framework 4 `csc.exe` that ships with it).

```powershell
powershell -ExecutionPolicy Bypass -File .\src\build.ps1            # -> .\bin\GoldenEye 007.exe
powershell -ExecutionPolicy Bypass -File .\src\build.ps1 -OutDir C:\path\to\Dolphin-x64
```

If you have a GoldenEye save in Dolphin, the build script extracts its save icon and uses it as the exe icon.

No game files, Dolphin binaries or BIOS/NAND data are included in this repository.
