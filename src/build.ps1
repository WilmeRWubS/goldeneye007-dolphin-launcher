# Builds "GoldenEye 007.exe" with the .NET Framework C# compiler (ships with Windows).
# Usage: .\build.ps1 [-OutDir <folder>]   (default: ..\bin)
param([string]$OutDir)
$ErrorActionPreference = 'Stop'
$src = $PSScriptRoot
$out = if ($OutDir) { $OutDir } else { Join-Path (Split-Path $src -Parent) 'bin' }
New-Item -ItemType Directory $out -Force | Out-Null
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$icon = Join-Path $src 'GoldenEye007.ico'

if (-not (Test-Path $icon)) {
    # Build an icon from the 48x48 save icon in the game's Wii save banner (RGB5A3 tiles).
    $banner = Join-Path $env:APPDATA 'Dolphin Emulator\Wii\title\00010000\534a4250\data\banner.bin'
    if (Test-Path $banner) {
        Add-Type -AssemblyName System.Drawing
        $b = [IO.File]::ReadAllBytes($banner)
        $bmp = New-Object Drawing.Bitmap 48, 48
        $off = 0x60A0
        for ($ty = 0; $ty -lt 48; $ty += 4) { for ($tx = 0; $tx -lt 48; $tx += 4) {
            for ($y = 0; $y -lt 4; $y++) { for ($x = 0; $x -lt 4; $x++) {
                $v = ([int]$b[$off] -shl 8) -bor [int]$b[$off + 1]; $off += 2
                if ($v -band 0x8000) {
                    $a = 255; $r = (($v -shr 10) -band 31) * 255 / 31; $g = (($v -shr 5) -band 31) * 255 / 31; $bl = ($v -band 31) * 255 / 31
                } else {
                    $a = (($v -shr 12) -band 7) * 255 / 7; $r = (($v -shr 8) -band 15) * 17; $g = (($v -shr 4) -band 15) * 17; $bl = ($v -band 15) * 17
                }
                $bmp.SetPixel($tx + $x, $ty + $y, [Drawing.Color]::FromArgb([int]$a, [int]$r, [int]$g, [int]$bl))
            } }
        } }
        $big = New-Object Drawing.Bitmap 256, 256
        $gfx = [Drawing.Graphics]::FromImage($big)
        $gfx.InterpolationMode = 'HighQualityBicubic'; $gfx.PixelOffsetMode = 'Half'
        $gfx.DrawImage($bmp, 0, 0, 256, 256); $gfx.Dispose()
        $ms = New-Object IO.MemoryStream; $big.Save($ms, [Drawing.Imaging.ImageFormat]::Png); $png = $ms.ToArray()
        $fs = [IO.File]::Create($icon); $w = New-Object IO.BinaryWriter $fs
        $w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]1)
        $w.Write([byte]0); $w.Write([byte]0); $w.Write([byte]0); $w.Write([byte]0)
        $w.Write([uint16]1); $w.Write([uint16]32); $w.Write([uint32]$png.Length); $w.Write([uint32]22)
        $w.Write($png); $w.Close()
        $bmp.Dispose(); $big.Dispose()
    }
}

$iconArg = @()
if (Test-Path $icon) { $iconArg = @("/win32icon:$icon") }

$sources = @((Join-Path $src 'Launcher.cs'), (Join-Path $src 'DolphinConfig.cs'))
$common = @('/nologo', '/optimize+', '/target:winexe', '/platform:anycpu',
    '/r:System.Windows.Forms.dll', '/r:System.Drawing.dll', '/r:System.Management.dll') + $iconArg

$exe = Join-Path $out 'GoldenEye 007.exe'
& $csc @common "/out:$exe" @sources
if ($LASTEXITCODE -ne 0) { throw "csc failed with exit code $LASTEXITCODE" }
"Built $exe"