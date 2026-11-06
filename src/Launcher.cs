// GoldenEye 007 (Wii, PAL) one-click launcher for Dolphin.
// Boots the game directly in fullscreen with a keyboard + mouse setup.
// Built twice: the default build emulates the Wii Remote pointer; the MOUSELOOK build emulates a
// Classic Controller and runs MouseInjector, which writes raw mouse movement straight into the
// player's yaw/pitch in game memory for PC-style aiming.
//
// Usage:
//   "GoldenEye 007.exe"               start the game
//   "GoldenEye 007.exe" --configure   open Dolphin's UI using the launcher's profile (tweak controls/graphics)
//   "GoldenEye 007.exe" --reset       restore the launcher's default Dolphin configuration, then start

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Management;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

#if MOUSELOOK
[assembly: AssemblyTitle("GoldenEye 007 (Mouse Look)")]
#else
[assembly: AssemblyTitle("GoldenEye 007")]
#endif
[assembly: AssemblyProduct("GoldenEye 007 (Dolphin launcher)")]
[assembly: AssemblyDescription("Boots GoldenEye 007 (Wii) in Dolphin with keyboard and mouse controls")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

internal static class Program
{
    private const string AppName = "GoldenEye 007";
    private const string DefaultGamePath =
        @"C:\Users\Willy\Documents\007\GoldenEye 007 (Europe) (En,Fr,De,Es,It).wbfs";
#if MOUSELOOK
    private static readonly bool MouseLook = true;
    private const string UserDirName = "GoldenEye007-MouseLook-User";
#else
    private static readonly bool MouseLook = false;
    private const string UserDirName = "GoldenEye007-User";
#endif
    // Shared by both builds; used to detect an already running instance of either.
    private const string UserDirPrefix = "GoldenEye007-";

    // Bump when the generated Dolphin config changes so existing installs get the new defaults.
    private const int ConfigVersion = 3;

    [STAThread]
    private static int Main(string[] args)
    {
        Application.EnableVisualStyles();

        bool configure = HasArg(args, "--configure");
        bool reset = HasArg(args, "--reset");

        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string dolphinExe = Path.Combine(baseDir, "Dolphin.exe");
        if (!File.Exists(dolphinExe))
        {
            Error("Dolphin.exe was not found next to this launcher:\n" + baseDir +
                  "\n\nKeep \"" + AppName + ".exe\" inside the Dolphin folder.");
            return 1;
        }

        if (IsAlreadyRunning())
        {
            MessageBox.Show(AppName + " is already running.", AppName,
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 0;
        }

        try
        {
            return Run(baseDir, dolphinExe, configure, reset);
        }
        catch (Exception ex)
        {
            Error("Failed to start the game:\n\n" + ex.Message);
            return 1;
        }
    }

    private static int Run(string baseDir, string dolphinExe, bool configure, bool reset)
    {
        string settingsPath = Path.Combine(baseDir, "GoldenEye007.ini");
        string userDir = Path.Combine(baseDir, UserDirName);
        string configDir = Path.Combine(userDir, "Config");

        Ini launcherIni = Ini.Load(settingsPath);
        string gamePath = launcherIni.Get("Launcher", "GamePath", DefaultGamePath);
        string nandPath = launcherIni.Get("Launcher", "NANDPath", DefaultNandPath());

        if (!configure && !File.Exists(gamePath))
        {
            gamePath = AskForGame(gamePath);
            if (gamePath == null)
                return 1;
        }

        bool sixtyFps = GetBool(launcherIni, "SixtyFPS", true);
        double sensitivity = GetDouble(launcherIni, "MouseSensitivity", 2.0);
        double adsMultiplier = GetDouble(launcherIni, "MouseAdsMultiplier", 0.6);
        bool invertY = GetBool(launcherIni, "MouseInvertY", false);

        launcherIni.Set("Launcher", "GamePath", gamePath);
        launcherIni.Set("Launcher", "NANDPath", nandPath);
        launcherIni.Set("Launcher", "SixtyFPS", sixtyFps ? "True" : "False");
        launcherIni.Set("Launcher", "MouseSensitivity", Num(sensitivity));
        launcherIni.Set("Launcher", "MouseAdsMultiplier", Num(adsMultiplier));
        launcherIni.Set("Launcher", "MouseInvertY", invertY ? "True" : "False");
        foreach (string old in new[] { "MouseLookSensitivity", "MouseLookSmoothing", "MouseLookDeadzone" })
            launcherIni.Remove("Launcher", old);
        launcherIni.Save(settingsPath, LauncherIniHeader);

        Directory.CreateDirectory(configDir);
        string versionFile = Path.Combine(userDir, "launcher-config-version.txt");
        int installedVersion = 0;
        if (File.Exists(versionFile))
            int.TryParse(File.ReadAllText(versionFile).Trim(), out installedVersion);

        if (reset || installedVersion != ConfigVersion)
        {
            DolphinConfig.WriteDefaults(userDir, MouseLook);
            File.WriteAllText(versionFile, ConfigVersion.ToString());
        }

        DolphinConfig.SetSixtyFps(userDir, sixtyFps);

        // Settings enforced on every launch; everything else stays tweakable via --configure.
        string dolphinIni = Path.Combine(configDir, "Dolphin.ini");
        Ini main = Ini.Load(dolphinIni);
        if (!string.IsNullOrEmpty(nandPath))
            main.Set("General", "NANDRootPath", nandPath.Replace('\\', '/'));
        main.Set("Analytics", "PermissionAsked", "True");
        main.Set("Interface", "ConfirmStop", "False");
        main.Set("Interface", "LockCursor", "True");
        main.Set("Interface", "CursorVisibility", "0");
        main.Save(dolphinIni, null);

        var psi = new ProcessStartInfo(dolphinExe);
        psi.WorkingDirectory = baseDir;
        psi.UseShellExecute = false;
        psi.Arguments = configure
            ? "-u " + Quote(userDir)
            : "-u " + Quote(userDir) + " -b -e " + Quote(gamePath);
        Process dolphin = Process.Start(psi);
        if (MouseLook && !configure && dolphin != null)
            MouseInjector.Run(dolphin, sensitivity, adsMultiplier, invertY);
        return 0;
    }

    private const string LauncherIniHeader =
        "; GoldenEye 007 launcher settings (shared by both exes)\r\n" +
        "; GamePath = disc image to boot\r\n" +
        "; NANDPath = Wii save-data folder (defaults to your normal Dolphin install so saves are shared)\r\n" +
        "; SixtyFPS = True/False - 60 FPS code (may get you stuck in Archives vents; Tank timer becomes impossible)\r\n" +
        "; MouseSensitivity = Mouse Look exe: same scale as CS/Source games (0.022 deg per mouse count x value)\r\n" +
        "; MouseAdsMultiplier = sensitivity multiplier while holding right mouse (aim down sights)\r\n" +
        "; MouseInvertY = True/False\r\n" +
        "; Run the exe with --configure to change controls/graphics in Dolphin's UI,\r\n" +
        "; or --reset to restore the launcher's default keyboard/mouse setup.\r\n";

    private static bool GetBool(Ini ini, string key, bool fallback)
    {
        string v = ini.Get("Launcher", key, fallback ? "True" : "False");
        return string.Equals(v, "True", StringComparison.OrdinalIgnoreCase) || v == "1";
    }

    private static double GetDouble(Ini ini, string key, double fallback)
    {
        double v;
        string s = ini.Get("Launcher", key, "").Replace(',', '.');
        return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v) && v >= 0 ? v : fallback;
    }

    private static string Num(double v)
    {
        return v.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string DefaultNandPath()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string nand = Path.Combine(appData, @"Dolphin Emulator\Wii");
        return Directory.Exists(nand) ? nand : "";
    }

    private static bool IsAlreadyRunning()
    {
        try
        {
            using (var searcher = new ManagementObjectSearcher(
                "SELECT CommandLine FROM Win32_Process WHERE Name = 'Dolphin.exe'"))
            {
                foreach (ManagementBaseObject p in searcher.Get())
                {
                    var cmd = p["CommandLine"] as string;
                    if (cmd != null && cmd.IndexOf(UserDirPrefix, StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }
            }
        }
        catch (Exception)
        {
            // WMI unavailable; just launch.
        }
        return false;
    }

    private static string AskForGame(string missingPath)
    {
        MessageBox.Show("The game image was not found:\n" + missingPath +
                        "\n\nPlease select your GoldenEye 007 disc image.",
                        AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
        using (var dlg = new OpenFileDialog())
        {
            dlg.Title = "Select GoldenEye 007 (Wii) disc image";
            dlg.Filter = "Wii disc images (*.wbfs;*.iso;*.rvz;*.wia;*.gcz;*.ciso)|*.wbfs;*.iso;*.rvz;*.wia;*.gcz;*.ciso|All files (*.*)|*.*";
            string dir = Path.GetDirectoryName(missingPath);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                dlg.InitialDirectory = dir;
            return dlg.ShowDialog() == DialogResult.OK ? dlg.FileName : null;
        }
    }

    private static bool HasArg(string[] args, string name)
    {
        foreach (string a in args)
            if (string.Equals(a, name, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    private static string Quote(string s)
    {
        return "\"" + s.TrimEnd('\\') + "\"";
    }

    private static void Error(string message)
    {
        MessageBox.Show(message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}

// Minimal order-preserving INI reader/writer compatible with Dolphin's format ("Key = Value").
internal sealed class Ini
{
    private readonly List<KeyValuePair<string, List<string>>> _sections =
        new List<KeyValuePair<string, List<string>>>();

    public static Ini Load(string path)
    {
        var ini = new Ini();
        if (!File.Exists(path))
            return ini;
        List<string> current = null;
        foreach (string raw in File.ReadAllLines(path))
        {
            string line = raw.Trim();
            if (line.StartsWith("[") && line.EndsWith("]"))
            {
                current = ini.Section(line.Substring(1, line.Length - 2), true);
                continue;
            }
            if (current != null && line.Length > 0 && !line.StartsWith(";"))
                current.Add(raw);
        }
        return ini;
    }

    public string Get(string section, string key, string fallback)
    {
        List<string> lines = Section(section, false);
        if (lines != null)
        {
            int i = Find(lines, key);
            if (i >= 0)
            {
                string line = lines[i];
                return line.Substring(line.IndexOf('=') + 1).Trim();
            }
        }
        return fallback;
    }

    public void Set(string section, string key, string value)
    {
        List<string> lines = Section(section, true);
        string entry = key + " = " + value;
        int i = Find(lines, key);
        if (i >= 0)
            lines[i] = entry;
        else
            lines.Add(entry);
    }

    public void Remove(string section, string key)
    {
        List<string> lines = Section(section, false);
        if (lines == null)
            return;
        int i = Find(lines, key);
        if (i >= 0)
            lines.RemoveAt(i);
    }

    public void Save(string path, string header)
    {
        var sb = new StringBuilder();
        if (header != null)
            sb.Append(header);
        foreach (var s in _sections)
        {
            sb.Append('[').Append(s.Key).Append("]\r\n");
            foreach (string l in s.Value)
                sb.Append(l).Append("\r\n");
        }
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
    }

    private List<string> Section(string name, bool create)
    {
        foreach (var s in _sections)
            if (string.Equals(s.Key, name, StringComparison.OrdinalIgnoreCase))
                return s.Value;
        if (!create)
            return null;
        var lines = new List<string>();
        _sections.Add(new KeyValuePair<string, List<string>>(name, lines));
        return lines;
    }

    private static int Find(List<string> lines, string key)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            int eq = lines[i].IndexOf('=');
            if (eq > 0 && string.Equals(lines[i].Substring(0, eq).Trim(), key, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }
}
