using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

internal static class Native
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct State
    {
        public uint Packet;
        public ushort Buttons;
        public byte LeftTrigger, RightTrigger;
        public short LX, LY, RX, RY;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int X, Y, Data;
        public uint Flags, Time;
        public UIntPtr Extra;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort Key, Scan;
        public uint Flags, Time;
        public UIntPtr Extra;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputData
    {
        [FieldOffset(0)] public MouseInput Mouse;
        [FieldOffset(0)] public KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputData Data;
    }

    [DllImport("xinput1_4.dll")]
    internal static extern uint XInputGetState(uint index, out State state);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, Input[] inputs, int size);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool SetProcessDpiAwarenessContext(IntPtr context);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetShellWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetDesktopWindow();

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowStyle(IntPtr window, int index);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr window, out RectangleEdges bounds);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr window, uint attribute, out RectangleEdges bounds, int size);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr window, StringBuilder name, int capacity);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetWindowTextW")]
    private static extern int GetWindowText(IntPtr window, StringBuilder text, int capacity);

    [DllImport("user32.dll")]
    private static extern bool IsZoomed(IntPtr window);

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr window, out RectangleEdges bounds);

    [DllImport("user32.dll")]
    private static extern bool ClientToScreen(IntPtr window, ref PointStruct point);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);

    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    private delegate bool EnumWindowsProc(IntPtr window, IntPtr param);

    [DllImport("user32.dll")]
    private static extern bool EnumChildWindows(IntPtr parent, EnumWindowsProc callback, IntPtr param);

    [DllImport("shell32.dll")]
    private static extern int SHQueryUserNotificationState(out int state);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, bool inherit, uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool QueryFullProcessImageName(IntPtr process, uint flags, StringBuilder path, ref uint size);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "Module32FirstW", SetLastError = true)]
    private static extern bool Module32First(IntPtr snapshot, ref ModuleEntry entry);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "Module32NextW", SetLastError = true)]
    private static extern bool Module32Next(IntPtr snapshot, ref ModuleEntry entry);

    [StructLayout(LayoutKind.Sequential)]
    private struct RectangleEdges
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PointStruct
    {
        public int X, Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public RectangleEdges Monitor;
        public RectangleEdges Work;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ModuleEntry
    {
        public uint Size;
        public uint ModuleId;
        public uint ProcessId;
        public uint GlobalUsage;
        public uint ProcessUsage;
        public IntPtr BaseAddress;
        public uint BaseSize;
        public IntPtr ModuleHandle;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string ModuleName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string ExePath;
    }

    internal static string ClassOf(IntPtr window)
    {
        if (window == IntPtr.Zero) return "";
        var name = new StringBuilder(128);
        return GetClassName(window, name, name.Capacity) > 0 ? name.ToString() : "";
    }

    internal static string TitleOf(IntPtr window)
    {
        if (window == IntPtr.Zero) return "";
        var text = new StringBuilder(256);
        return GetWindowText(window, text, text.Capacity) > 0 ? text.ToString() : "";
    }

    internal static uint ProcessIdOf(IntPtr window)
    {
        uint id = 0;
        if (window != IntPtr.Zero) GetWindowThreadProcessId(window, out id);
        return id;
    }

    // Resolves UWP / Microsoft Store apps hosted inside ApplicationFrameHost to the real app process.
    internal static uint RealProcessIdOf(IntPtr window)
    {
        uint id = ProcessIdOf(window);
        if (id == 0 || ClassOf(window) != "ApplicationFrameWindow") return id;
        uint childPid = 0;
        EnumChildWindows(window, delegate(IntPtr child, IntPtr param) {
            uint pid = ProcessIdOf(child);
            if (pid == 0 || pid == id) return true;
            if (ClassOf(child) == "Windows.UI.Core.CoreWindow")
            {
                childPid = pid;
                return false;
            }
            if (childPid == 0) childPid = pid;
            return true;
        }, IntPtr.Zero);
        return childPid != 0 ? childPid : id;
    }

    // Full executable path using PROCESS_QUERY_LIMITED_INFORMATION (works even on elevated/anti-cheat processes).
    internal static string ProcessPathOf(uint processId)
    {
        if (processId == 0) return "";
        const uint QueryLimited = 0x1000;
        IntPtr handle = OpenProcess(QueryLimited, false, processId);
        if (handle == IntPtr.Zero) return "";
        try
        {
            var path = new StringBuilder(1024);
            uint size = (uint)path.Capacity;
            return QueryFullProcessImageName(handle, 0, path, ref size) ? path.ToString() : "";
        }
        finally { CloseHandle(handle); }
    }

    // Lower-case process name without ".exe", or "" if it can't be read.
    internal static string ProcessNameOf(uint processId, out string exePath)
    {
        exePath = ProcessPathOf(processId);
        if (exePath.Length != 0)
            return Path.GetFileNameWithoutExtension(exePath).ToLowerInvariant();
        return ProcessNameOf(processId);
    }

    internal static string ProcessNameOf(uint processId)
    {
        if (processId == 0) return "";
        string path = ProcessPathOf(processId);
        if (path.Length != 0)
            return Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
        try
        {
            using (var process = Process.GetProcessById((int)processId))
                return process.ProcessName.ToLowerInvariant();
        }
        catch (ArgumentException) { return ""; }
        catch (InvalidOperationException) { return ""; }
        catch (System.ComponentModel.Win32Exception) { return ""; }
    }

    // Returns loaded module file names (lower-case) for a process, or null if access is denied.
    internal static List<string> ModulesOf(uint processId)
    {
        if (processId == 0) return null;
        const uint SnapModule = 0x00000008, SnapModule32 = 0x00000010;
        const int ErrorBadLength = 24;
        IntPtr invalid = new IntPtr(-1);
        IntPtr snapshot = invalid;
        for (int attempt = 0; attempt < 3; attempt++)
        {
            snapshot = CreateToolhelp32Snapshot(SnapModule | SnapModule32, processId);
            if (snapshot != invalid) break;
            if (Marshal.GetLastWin32Error() != ErrorBadLength) return null;
        }
        if (snapshot == invalid) return null;
        try
        {
            var entry = new ModuleEntry { Size = (uint)Marshal.SizeOf(typeof(ModuleEntry)) };
            if (!Module32First(snapshot, ref entry)) return null;
            var modules = new List<string>(64);
            do
            {
                if (!string.IsNullOrEmpty(entry.ModuleName))
                    modules.Add(entry.ModuleName.ToLowerInvariant());
            }
            while (Module32Next(snapshot, ref entry));
            return modules;
        }
        finally { CloseHandle(snapshot); }
    }

    internal static bool IsDirect3DFullscreen()
    {
        const int QunsRunningD3dFullScreen = 3;
        int state;
        return SHQueryUserNotificationState(out state) == 0 && state == QunsRunningD3dFullScreen;
    }

    // Shell surfaces (taskbar, tray overflow, Start/search) that shouldn't count as "the app you're using".
    internal static bool IsShellSurface(IntPtr window)
    {
        switch (ClassOf(window))
        {
            case "Shell_TrayWnd":
            case "Shell_SecondaryTrayWnd":
            case "NotifyIconOverflowWindow":
            case "TopLevelWindowForOverflowXamlIsland":
            case "Windows.UI.Core.CoreWindow":
                return true;
            default:
                return false;
        }
    }

    internal static bool IsDesktopWindow(IntPtr window)
    {
        if (window == IntPtr.Zero || window == GetShellWindow() || window == GetDesktopWindow())
            return true;
        string className = ClassOf(window);
        return className == "WorkerW" || className == "Progman";
    }

    internal static bool IsFullscreenWindow(IntPtr window)
    {
        const int StyleIndex = -16;
        const int Caption = 0x00C00000;
        const uint ExtendedFrameBounds = 9;
        const uint MonitorDefaultToNearest = 2;
        if (IsDesktopWindow(window) || IsShellSurface(window))
            return false;
        if (IsDirect3DFullscreen())
            return true;

        IntPtr monitorHandle = MonitorFromWindow(window, MonitorDefaultToNearest);
        if (monitorHandle == IntPtr.Zero) return false;
        var info = new MonitorInfo { Size = Marshal.SizeOf(typeof(MonitorInfo)) };
        if (!GetMonitorInfo(monitorHandle, ref info)) return false;
        var monitor = Rectangle.FromLTRB(info.Monitor.Left, info.Monitor.Top, info.Monitor.Right, info.Monitor.Bottom);
        var work = Rectangle.FromLTRB(info.Work.Left, info.Work.Top, info.Work.Right, info.Work.Bottom);

        RectangleEdges frame;
        if (DwmGetWindowAttribute(window, ExtendedFrameBounds, out frame, 16) != 0 &&
            !GetWindowRect(window, out frame))
            return false;
        var frameRect = Rectangle.FromLTRB(frame.Left, frame.Top, frame.Right, frame.Bottom);

        bool hasCaption = (GetWindowStyle(window, StyleIndex) & Caption) == Caption;
        if (!hasCaption && CoversMonitor(frameRect, monitor))
            return true;

        // Many borderless games (Unity, Unreal, DirectX, SDL) keep WS_CAPTION set while removing the titlebar
        // or extending the frame slightly offscreen. Check whether their client area actually covers the monitor,
        // while ignoring normal maximized desktop windows when the taskbar is auto-hidden.
        RectangleEdges client;
        if (!GetClientRect(window, out client) || client.Right <= client.Left || client.Bottom <= client.Top)
            return false;
        var topLeft = new PointStruct { X = client.Left, Y = client.Top };
        var bottomRight = new PointStruct { X = client.Right, Y = client.Bottom };
        if (!ClientToScreen(window, ref topLeft) || !ClientToScreen(window, ref bottomRight))
            return false;
        var clientRect = Rectangle.FromLTRB(topLeft.X, topLeft.Y, bottomRight.X, bottomRight.Y);
        if (!CoversMonitor(clientRect, monitor))
            return false;
        return !IsZoomed(window) || !CoversMonitor(work, monitor);
    }

    internal static bool CoversMonitor(Rectangle window, Rectangle monitor)
    {
        // Allow small DPI/compositor rounding inside (up to 2px) and borderless frame overhang outside (up to 24px),
        // while rejecting windows that span across multiple monitors.
        const int inset = 2, overhang = 24;
        return window.Left <= monitor.Left + inset && window.Left >= monitor.Left - overhang &&
            window.Top <= monitor.Top + inset && window.Top >= monitor.Top - overhang &&
            window.Right >= monitor.Right - inset && window.Right <= monitor.Right + overhang &&
            window.Bottom >= monitor.Bottom - inset && window.Bottom <= monitor.Bottom + overhang;
    }

    private static readonly int InputSize = Marshal.SizeOf(typeof(Input));

    internal static bool Mouse(uint flags, int data = 0)
    {
        var input = new Input();
        input.Data.Mouse.Flags = flags;
        input.Data.Mouse.Data = data;
        return SendInput(1, new[] { input }, InputSize) == 1;
    }

    internal static bool Key(ushort value, bool unicode)
    {
        var down = new Input { Type = 1 };
        down.Data.Keyboard = unicode
            ? new KeyboardInput { Scan = value, Flags = 4 }
            : new KeyboardInput { Key = value };
        var up = down;
        up.Data.Keyboard.Flags |= 2;
        uint sent = SendInput(2, new[] { down, up }, InputSize);
        if (sent == 1)
            return SendInput(1, new[] { up }, InputSize) == 1;
        return sent == 2;
    }

    internal static bool LayoutValid
    {
        get
        {
            return Marshal.SizeOf(typeof(State)) == 16 &&
                InputSize == (IntPtr.Size == 8 ? 40 : 28) &&
                Marshal.SizeOf(typeof(MonitorInfo)) == 40 &&
                Marshal.SizeOf(typeof(ModuleEntry)) == (IntPtr.Size == 8 ? 1080 : 1064);
        }
    }
}

// Heuristics for detecting PC games even when they run windowed or borderless.
internal static class GameDetector
{
    private static readonly HashSet<string> KnownLaunchers = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
        "steam", "steamwebhelper", "epicgameslauncher", "epicwebhelper", "galaxyclient", "galaxyclient helper",
        "goggalaxy", "eadesktop", "eabackgroundservice", "origin", "upc", "ubisoftconnect", "battle.net",
        "riotclientservices", "riotclientux", "riotclientuxrender", "playnite.desktopapp", "launchbox",
        "heroic", "itch", "amazongames", "discord", "obs64", "obs32", "wallpaper32", "wallpaper64",
        "losslessscaling", "borderlessgaming", "sharex", "blender", "aseprite"
    };

    private static readonly string[] GamePathMarkers = {
        @"\steamapps\common\", @"\gog games\", @"\gog galaxy\games\", @"\xboxgames\", @"\modifiablewindowsapps\",
        @"\ea games\", @"\origin games\", @"\ubisoft game launcher\games\", @"\amazon games\library\",
        @"\itch\apps\", @"\heroic\games\"
    };

    private static readonly HashSet<string> BrowserHostDlls = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
        "chrome_elf.dll", "msedge_elf.dll", "libcef.dll", "webview2loader.dll",
        "embeddedbrowserwebview.dll", "chrome.dll", "msedge.dll", "xul.dll"
    };

    private static readonly HashSet<string> GameOnlyDlls = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
        "steam_api.dll", "steam_api64.dll", "gameoverlayrenderer.dll", "gameoverlayrenderer64.dll",
        "eossdk-win64-shipping.dll", "eossdk-win32-shipping.dll", "eosovh-win64-shipping.dll", "eosovh-win32-shipping.dll",
        "unityplayer.dll", "nvngx_dlss.dll", "sl.interposer.dll", "libxess.dll",
        "bink2w64.dll", "bink2w32.dll", "binkw64.dll", "binkw32.dll",
        "easyanticheat_eos.dll", "easyanticheat_x64.dll", "easyanticheat_x86.dll", "beclient_x64.dll", "beclient.dll",
        "physx3_x64.dll", "physx3_x86.dll", "physx_64.dll", "physxcommon_64.dll",
        "fmodstudio.dll", "fmod.dll", "fmod64.dll", "fmodex.dll", "fmodex64.dll",
        "galaxy.dll", "galaxy64.dll"
    };

    private static readonly HashSet<string> ControllerDlls = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
        "xinput1_4.dll", "xinput1_3.dll", "xinput9_1_0.dll", "xinputuap.dll",
        "dinput8.dll", "dinput.dll", "gameinput.dll", "gameinputredist.dll",
        "windows.gaming.input.dll", "sdl2.dll", "sdl3.dll"
    };

    private static readonly HashSet<string> GraphicsDlls = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
        "d3d9.dll", "d3d10.dll", "d3d10_1.dll", "d3d11.dll", "d3d12.dll", "d3d12core.dll",
        "vulkan-1.dll", "opengl32.dll", "ddraw.dll"
    };

    private static readonly string WindowsDir =
        (Environment.GetFolderPath(Environment.SpecialFolder.Windows) ?? "").TrimEnd('\\').ToLowerInvariant();

    internal static bool IsKnownLauncher(string processName)
    {
        return !string.IsNullOrEmpty(processName) && KnownLaunchers.Contains(processName);
    }

    internal static bool IsSystemExecutable(string exePath)
    {
        if (string.IsNullOrEmpty(exePath) || WindowsDir.Length == 0) return false;
        string normalized = exePath.Replace('/', '\\').ToLowerInvariant();
        if (!normalized.StartsWith(WindowsDir + @"\", StringComparison.Ordinal)) return false;
        string relative = normalized.Substring(WindowsDir.Length + 1);
        return relative == "explorer.exe" ||
            relative.StartsWith(@"system32\", StringComparison.Ordinal) ||
            relative.StartsWith(@"syswow64\", StringComparison.Ordinal) ||
            relative.StartsWith(@"systemapps\", StringComparison.Ordinal) ||
            relative.StartsWith(@"immersivecontrolpanel\", StringComparison.Ordinal);
    }

    internal static bool IsGamePath(string exePath, string processName)
    {
        if (IsKnownLauncher(processName) || IsSystemExecutable(exePath)) return false;
        if (!string.IsNullOrEmpty(processName))
        {
            string lowerName = processName.ToLowerInvariant();
            if (lowerName.EndsWith("-win64-shipping", StringComparison.Ordinal) ||
                lowerName.EndsWith("-win32-shipping", StringComparison.Ordinal) ||
                lowerName.EndsWith("-wingdk-shipping", StringComparison.Ordinal))
                return true;
        }
        if (string.IsNullOrEmpty(exePath)) return false;
        string path = exePath.Replace('/', '\\').ToLowerInvariant();
        foreach (string marker in GamePathMarkers)
            if (path.IndexOf(marker, StringComparison.Ordinal) >= 0) return true;
        if (path.IndexOf(@"\epic games\", StringComparison.Ordinal) >= 0 &&
            path.IndexOf(@"\epic games\launcher\", StringComparison.Ordinal) < 0)
            return true;
        if (path.IndexOf(@"\riot games\", StringComparison.Ordinal) >= 0 &&
            path.IndexOf(@"\riot games\riot client\", StringComparison.Ordinal) < 0)
            return true;
        return false;
    }

    internal static bool IsGameModules(IEnumerable<string> modules, string processName)
    {
        if (modules == null || IsKnownLauncher(processName)) return false;
        bool isBrowserHost = false, hasController = false, hasGraphics = false;
        foreach (string raw in modules)
        {
            if (string.IsNullOrEmpty(raw)) continue;
            string name = raw.ToLowerInvariant();
            if (GameOnlyDlls.Contains(name)) return true;
            if (BrowserHostDlls.Contains(name)) isBrowserHost = true;
            else if (ControllerDlls.Contains(name)) hasController = true;
            else if (GraphicsDlls.Contains(name)) hasGraphics = true;
        }
        return !isBrowserHost && hasController && hasGraphics;
    }

    internal static bool IsSteamBigPicture(IntPtr window, string processName)
    {
        if (processName != "steamwebhelper" && processName != "steam") return false;
        return string.Equals(Native.TitleOf(window), "Steam Big Picture Mode", StringComparison.OrdinalIgnoreCase);
    }
}

internal enum AppMode { Auto, On, Off }

internal enum RuleSource { None, User, List, Default }

// Process-name rules. "name" matches exactly; "name*" matches any process starting with "name".
internal sealed class RuleSet
{
    private readonly Dictionary<string, AppMode> exact = new Dictionary<string, AppMode>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AppMode> prefixes = new Dictionary<string, AppMode>(StringComparer.OrdinalIgnoreCase);

    internal static string Normalize(string name)
    {
        name = (name ?? "").Trim().ToLowerInvariant();
        if (name.EndsWith(".exe", StringComparison.Ordinal)) name = name.Substring(0, name.Length - 4);
        return name;
    }

    internal void Clear()
    {
        exact.Clear();
        prefixes.Clear();
    }

    // Overwrites any existing rule for the pattern. AppMode.Auto removes it.
    internal void Set(string pattern, AppMode mode)
    {
        Dictionary<string, AppMode> table;
        string key = Key(pattern, out table);
        if (key.Length == 0) return;
        if (mode == AppMode.Auto) table.Remove(key);
        else table[key] = mode;
    }

    // Adds a rule from a shared list. When lists disagree, Off wins so games never get stray clicks.
    internal void Merge(string pattern, AppMode mode)
    {
        Dictionary<string, AppMode> table;
        string key = Key(pattern, out table);
        if (key.Length == 0 || mode == AppMode.Auto) return;
        AppMode existing;
        if (table.TryGetValue(key, out existing) && existing == AppMode.Off) return;
        table[key] = mode;
    }

    private string Key(string pattern, out Dictionary<string, AppMode> table)
    {
        string key = Normalize(pattern);
        table = exact;
        if (key.EndsWith("*", StringComparison.Ordinal))
        {
            key = key.TrimEnd('*');
            table = prefixes;
        }
        return key;
    }

    internal AppMode Match(string app)
    {
        if (string.IsNullOrEmpty(app)) return AppMode.Auto;
        AppMode mode;
        if (exact.TryGetValue(app, out mode)) return mode;
        mode = AppMode.Auto;
        int best = 0;
        foreach (var rule in prefixes)
        {
            if (!app.StartsWith(rule.Key, StringComparison.OrdinalIgnoreCase)) continue;
            if (rule.Key.Length > best || (rule.Key.Length == best && rule.Value == AppMode.Off))
            {
                best = rule.Key.Length;
                mode = rule.Value;
            }
        }
        return mode;
    }

    internal AppMode MatchExact(string app)
    {
        AppMode mode;
        return exact.TryGetValue(Normalize(app), out mode) ? mode : AppMode.Auto;
    }

    internal List<KeyValuePair<string, AppMode>> Entries()
    {
        var entries = new List<KeyValuePair<string, AppMode>>(exact);
        foreach (var rule in prefixes)
            entries.Add(new KeyValuePair<string, AppMode>(rule.Key + "*", rule.Value));
        entries.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
        return entries;
    }
}

// Settings live in %APPDATA%\PadDesk\settings.ini. Other programs (like a game frontend) can drop
// rule lists into %APPDATA%\PadDesk\lists\*.txt; PadDesk picks up changes within a couple of seconds.
internal sealed class Settings
{
    // Apps where PadDesk should always drive the mouse, even fullscreen (browsers, video players, the desktop).
    private static readonly string[] DefaultOn = {
        "chrome", "msedge", "firefox", "brave", "opera", "vivaldi", "librewolf", "waterfox", "floorp", "zen",
        "chromium", "thorium", "arc", "explorer", "searchhost", "shellexperiencehost", "startmenuexperiencehost",
        "vlc", "mpv", "mpc-hc*", "mpc-be*", "potplayer*", "wmplayer", "microsoft.media.player", "spotify",
        "jellyfinmediaplayer", "plex", "stremio*"
    };

    // Apps that read the controller themselves (emulators, controller-driven frontends, game streaming).
    private static readonly string[] DefaultOff = {
        "spine", "retroarch", "pcsx2*", "dolphin*", "cemu*", "duckstation*", "ppsspp*", "rpcs3*", "xemu*", "xenia*",
        "citra*", "lime3ds*", "azahar*", "ryujinx*", "yuzu*", "eden*", "suyu*", "sudachi*", "citron*", "melonds*",
        "mgba*", "desmume*", "flycast*", "redream", "mame*", "snes9x*", "bsnes*", "mesen*", "vita3k*", "shadps4*",
        "ares", "emuhawk", "project64*", "rmg", "simple64*", "mednafen", "blastem", "scummvm", "dosbox*", "cxbx*",
        "vpinball*", "visualboyadvance*", "vba-m", "kodi", "playnite.fullscreenapp", "es-de", "emulationstation",
        "bigbox", "pegasus-fe", "retrobat", "attract", "moonlight", "chiaki*", "greenlight", "xbplay"
    };

    internal readonly string Folder;
    internal readonly RuleSet User = new RuleSet();
    internal readonly RuleSet Lists = new RuleSet();
    internal readonly RuleSet Defaults = new RuleSet();
    internal double Speed = 1;
    internal bool AutoGames = true;
    internal bool AutoFullscreen = true;
    private string listSignature;

    internal Settings(string folder)
    {
        Folder = folder;
        foreach (string app in DefaultOn) Defaults.Set(app, AppMode.On);
        foreach (string app in DefaultOff) Defaults.Set(app, AppMode.Off);
    }

    internal static string DefaultFolder
    {
        get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PadDesk"); }
    }

    internal string FilePath { get { return Path.Combine(Folder, "settings.ini"); } }
    internal string ListsFolder { get { return Path.Combine(Folder, "lists"); } }

    internal static bool TryParseMode(string text, out AppMode mode)
    {
        switch ((text ?? "").Trim().ToLowerInvariant())
        {
            case "on": mode = AppMode.On; return true;
            case "off": mode = AppMode.Off; return true;
            case "auto": mode = AppMode.Auto; return true;
            default: mode = AppMode.Auto; return false;
        }
    }

    internal AppMode Resolve(string app, out RuleSource source)
    {
        AppMode mode = User.Match(app);
        if (mode != AppMode.Auto) { source = RuleSource.User; return mode; }
        return ResolveWithoutUser(app, out source);
    }

    internal AppMode ResolveWithoutUser(string app, out RuleSource source)
    {
        AppMode mode = Lists.Match(app);
        if (mode != AppMode.Auto) { source = RuleSource.List; return mode; }
        mode = Defaults.Match(app);
        if (mode != AppMode.Auto) { source = RuleSource.Default; return mode; }
        source = RuleSource.None;
        return AppMode.Auto;
    }

    internal void Load()
    {
        User.Clear();
        string[] lines;
        try
        {
            if (!File.Exists(FilePath)) return;
            lines = File.ReadAllLines(FilePath);
        }
        catch (IOException) { return; }
        catch (UnauthorizedAccessException) { return; }

        bool apps = false;
        foreach (string raw in lines)
        {
            string line = raw.Trim();
            if (line.Length == 0 || line[0] == '#' || line[0] == ';') continue;
            if (line[0] == '[')
            {
                apps = string.Equals(line, "[apps]", StringComparison.OrdinalIgnoreCase);
                continue;
            }
            int equals = line.IndexOf('=');
            if (equals <= 0) continue;
            string key = line.Substring(0, equals).Trim();
            string value = line.Substring(equals + 1).Trim().ToLowerInvariant();
            AppMode mode;
            double number;
            if (apps)
            {
                if (TryParseMode(value, out mode)) User.Set(key, mode);
            }
            else if (string.Equals(key, "speed", StringComparison.OrdinalIgnoreCase))
            {
                if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number) &&
                    number >= 0.2 && number <= 4)
                    Speed = number;
            }
            else if (string.Equals(key, "pauseInGames", StringComparison.OrdinalIgnoreCase))
            {
                AutoGames = value != "false" && value != "off" && value != "0";
            }
            else if (string.Equals(key, "pauseInFullscreen", StringComparison.OrdinalIgnoreCase))
            {
                AutoFullscreen = value != "false" && value != "off" && value != "0";
            }
        }
    }

    internal bool Save()
    {
        var text = new StringBuilder();
        text.AppendLine("# PadDesk settings. The tray menu updates this file; if you edit it by hand, close PadDesk first.");
        text.AppendLine("#");
        text.AppendLine("# Per-app rules use the program's process name without .exe (shown in the tray menu).");
        text.AppendLine("# A trailing * matches every name that starts with it, e.g. pcsx2* = off.");
        text.AppendLine("# Other programs can add shared rules as .txt files in the \"lists\" folder next to this file,");
        text.AppendLine("# one rule per line:  off myemulator   /   on mybrowser");
        text.AppendLine("# Rules here beat the lists, and the lists beat PadDesk's built-in defaults.");
        text.AppendLine();
        text.AppendLine("speed=" + Speed.ToString(CultureInfo.InvariantCulture));
        text.AppendLine("pauseInGames=" + (AutoGames ? "true" : "false"));
        text.AppendLine("pauseInFullscreen=" + (AutoFullscreen ? "true" : "false"));
        text.AppendLine();
        text.AppendLine("[apps]");
        foreach (var rule in User.Entries())
            text.AppendLine(rule.Key + "=" + (rule.Value == AppMode.On ? "on" : "off"));

        string temporary = FilePath + ".tmp";
        try
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(temporary, text.ToString());
            if (!File.Exists(FilePath))
            {
                File.Move(temporary, FilePath);
                return true;
            }
            try { File.Replace(temporary, FilePath, null); }
            catch (IOException)
            {
                // Some drives (FAT, network shares) can't do an atomic replace.
                File.Copy(temporary, FilePath, true);
                File.Delete(temporary);
            }
            return true;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }

    // Reloads lists\*.txt if any file was added, removed or changed. Returns true if the rules changed.
    internal bool RefreshLists()
    {
        string[] files = new string[0];
        try
        {
            if (Directory.Exists(ListsFolder)) files = Directory.GetFiles(ListsFolder, "*.txt");
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);

        var signature = new StringBuilder();
        foreach (string file in files)
        {
            signature.Append(file).Append('|');
            try { signature.Append(File.GetLastWriteTimeUtc(file).Ticks); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            signature.Append(';');
        }
        if (signature.ToString() == listSignature) return false;
        listSignature = signature.ToString();

        Lists.Clear();
        foreach (string file in files)
        {
            string[] lines;
            try { lines = File.ReadAllLines(file); }
            catch (IOException) { continue; }
            catch (UnauthorizedAccessException) { continue; }
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                int space = line.IndexOfAny(new[] { ' ', '\t' });
                if (space <= 0) continue;
                AppMode mode;
                if (TryParseMode(line.Substring(0, space), out mode))
                    Lists.Merge(line.Substring(space + 1), mode);
            }
        }
        return true;
    }
}

// Small click-through banner at the top of the screen, e.g. "PadDesk on in chrome". Never takes focus.
internal sealed class Toast : Form
{
    private readonly System.Windows.Forms.Timer hideTimer = new System.Windows.Forms.Timer { Interval = 2200 };
    private string message = "";
    private Font font;

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);

    internal Toast()
    {
        Text = "PadDesk";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        DoubleBuffered = true;
        BackColor = Color.FromArgb(20, 23, 34);
        ForeColor = Color.White;
        hideTimer.Tick += delegate { HideToast(); };
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x08000000 | 0x80 | 0x8 | 0x20; // NOACTIVATE | TOOLWINDOW | TOPMOST | TRANSPARENT
            return cp;
        }
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == 0x21) { message.Result = new IntPtr(3); return; }   // WM_MOUSEACTIVATE -> no activate
        if (message.Msg == 0x84) { message.Result = new IntPtr(-1); return; }  // WM_NCHITTEST -> click through
        base.WndProc(ref message);
    }

    internal void ShowMessage(string text, Rectangle screen)
    {
        message = text;
        float pixels = Math.Max(14, screen.Height / 48f);
        if (font != null) font.Dispose();
        font = new Font("Segoe UI Semibold", pixels, GraphicsUnit.Pixel);
        Size textSize = TextRenderer.MeasureText(text, font, Size.Empty, TextFormatFlags.NoPrefix);
        int padX = (int)(pixels * 1.3f), padY = (int)(pixels * 0.7f);
        int width = Math.Min(screen.Width - 40, textSize.Width + padX * 2);
        int height = textSize.Height + padY * 2;
        int x = screen.Left + (screen.Width - width) / 2;
        int y = screen.Top + screen.Height / 14;

        const uint NoActivate = 0x0010, ShowWindow = 0x0040;
        SetWindowPos(Handle, new IntPtr(-1), x, y, width, height, NoActivate | ShowWindow);
        using (var path = Rounded(new Rectangle(0, 0, width, height), Math.Min(height, 28)))
        {
            var old = Region;
            Region = new Region(path);
            if (old != null) old.Dispose();
        }
        Invalidate();
        hideTimer.Stop();
        hideTimer.Start();
    }

    internal void HideToast()
    {
        hideTimer.Stop();
        if (!IsHandleCreated) return;
        const uint KeepBounds = 0x0003, KeepZOrder = 0x0004, NoActivate = 0x0010, HideWindow = 0x0080;
        SetWindowPos(Handle, IntPtr.Zero, 0, 0, 0, 0, KeepBounds | KeepZOrder | NoActivate | HideWindow);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (font == null) return;
        var bounds = new Rectangle(0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using (var path = Rounded(bounds, Math.Min(bounds.Height, 28)))
        using (var border = new Pen(Color.FromArgb(188, 174, 255), 2))
            e.Graphics.DrawPath(border, path);
        TextRenderer.DrawText(e.Graphics, message, font, bounds, ForeColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix |
            TextFormatFlags.EndEllipsis);
    }

    private static System.Drawing.Drawing2D.GraphicsPath Rounded(Rectangle box, int diameter)
    {
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        diameter = Math.Max(2, Math.Min(diameter, Math.Min(box.Width, box.Height)));
        path.AddArc(box.Left, box.Top, diameter, diameter, 180, 90);
        path.AddArc(box.Right - diameter, box.Top, diameter, diameter, 270, 90);
        path.AddArc(box.Right - diameter, box.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(box.Left, box.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            hideTimer.Dispose();
            if (font != null) font.Dispose();
        }
        base.Dispose(disposing);
    }
}

internal sealed class Keyboard : Form
{
    private readonly string[][] lower = {
        Row("1234567890-="), Row("qwertyuiop[]"), Row("asdfghjkl;'"),
        Row("zxcvbnm,./"), new[] { "Shift", "Tab", "Space", "Backspace", "Enter", "Close" }
    };
    private readonly string[][] upper = {
        Row("!@#$%^&*()_+"), Row("QWERTYUIOP{}"), Row("ASDFGHJKL:\""),
        Row("ZXCVBNM<>?"), new[] { "Shift", "Tab", "Space", "Backspace", "Enter", "Close" }
    };
    private readonly Rectangle[][] keys;
    private readonly Func<ushort, bool, bool> sendKey;
    private static readonly int[] ActionWidths = { 14, 9, 34, 17, 14, 12 };
    private readonly System.Text.StringBuilder preview = new System.Text.StringBuilder();
    private Rectangle workArea;
    private IntPtr target;
    private bool dockTop, hidePreview, inputBlocked;

    internal string PreviewText { get { return preview.ToString(); } }


    private int row, column;
    private bool shift;

    internal Keyboard(Func<ushort, bool, bool> sendKey)
    {
        this.sendKey = sendKey;
        Text = "PadDesk keyboard";
        FormBorderStyle = FormBorderStyle.None;


        StartPosition = FormStartPosition.Manual;
        MaximizeBox = false;
        MinimizeBox = false;
        ResizeRedraw = true;
        DoubleBuffered = true;
        BackColor = Color.FromArgb(20, 23, 34);
        ForeColor = Color.White;
        keys = Array.ConvertAll(lower, labels => new Rectangle[labels.Length]);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        LayoutKeys();
        var labels = shift ? upper : lower;
        int width = ClientSize.Width, height = ClientSize.Height;
        float scale = Math.Min(width / 920f, height / 392f);
        var accent = Color.FromArgb(188, 174, 255);
        var muted = Color.FromArgb(182, 190, 211);
        using (var border = new Pen(Color.FromArgb(77, 80, 105)))
        using (var strip = new SolidBrush(Color.FromArgb(32, 36, 53)))
        using (var selected = new SolidBrush(accent))
        using (var normal = new SolidBrush(Color.FromArgb(42, 47, 65)))
        using (var action = new SolidBrush(Color.FromArgb(54, 60, 82)))
        using (var letters = new Font("Segoe UI Semibold", Math.Max(12, 23 * scale), GraphicsUnit.Pixel))
        using (var small = new Font("Segoe UI", Math.Max(9, 15 * scale), GraphicsUnit.Pixel))
        {
            e.Graphics.DrawRectangle(border, 0, 0, width - 1, height - 1);
            var previewBox = new Rectangle(14, 12, width - 28, height * 17 / 100);
            FillTile(e.Graphics, strip, previewBox);
            DrawLabel(e.Graphics, "TYPING PREVIEW", small,
                new Rectangle(previewBox.X + 10, previewBox.Y + 2, previewBox.Width - 20, previewBox.Height / 3),
                accent, TextFormatFlags.Left);
            var line = new Rectangle(previewBox.X + 10, previewBox.Y + previewBox.Height / 3,
                previewBox.Width - 20, previewBox.Height * 2 / 3);
            string text = PreviewText;
            if (hidePreview && text.Length != 0) text = new string('•', text.Length);
            if (text.Length == 0)
                text = hidePreview ? "Preview hidden" : "Text you type here will appear as you go";
            else
            {
                int first = 0;
                while (first < text.Length && TextRenderer.MeasureText(e.Graphics, text.Substring(first),
                    letters, Size.Empty, TextFormatFlags.NoPrefix).Width > line.Width - 20) first++;
                if (first != 0) text = "…" + text.Substring(first);
            }
            DrawLabel(e.Graphics, text, letters, line, preview.Length == 0 ? muted : ForeColor, TextFormatFlags.Left);

            for (int r = 0; r < keys.Length; r++)
            for (int c = 0; c < keys[r].Length; c++)
            {
                bool active = r == row && c == column;
                bool actionKey = r == keys.Length - 1;
                Rectangle bounds = keys[r][c];
                FillTile(e.Graphics, active ? selected : actionKey ? action : normal, bounds);
                Color ink = active ? Color.FromArgb(24, 19, 47) : ForeColor;
                string label = labels[r][c];
                DrawLabel(e.Graphics, shift && label == "Shift" ? "SHIFT ON" : label,
                    actionKey ? small : letters, bounds, ink, TextFormatFlags.HorizontalCenter);
            }

            var footer = new Rectangle(14, height * 89 / 100, width - 28, height * 10 / 100);
            if (inputBlocked)
            {
                DrawLabel(e.Graphics, "Windows blocked the keystroke. Check the app you’re typing into.",
                    small, footer, Color.FromArgb(255, 191, 152), TextFormatFlags.Left);
            }
            else
            {
                string[] hints = { "D-pad  Move", "A  Type", "B  Delete", "X  Space", "Y  Close",
                    "LB  Shift", "RB  Dock", hidePreview ? "RS  Show text" : "RS  Hide text" };
                Color[] colors = { muted, Color.PaleGreen, Color.LightSalmon, Color.LightSkyBlue,
                    Color.Khaki, muted, muted, muted };
                for (int i = 0; i < hints.Length; i++)
                {
                    int left = footer.X + footer.Width * i / hints.Length;
                    int right = footer.X + footer.Width * (i + 1) / hints.Length;
                    DrawLabel(e.Graphics, hints[i], small, new Rectangle(left, footer.Y, right - left, footer.Height),
                        colors[i], TextFormatFlags.HorizontalCenter);
                }
            }
        }
    }

    private static void DrawLabel(Graphics graphics, string text, Font font, Rectangle bounds, Color color,
        TextFormatFlags align)
    {
        TextRenderer.DrawText(graphics, text, font, bounds, color,
            align | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
    }

    private static void FillTile(Graphics graphics, Brush brush, Rectangle box)
    {
        const int diameter = 12;
        if (box.Width < diameter || box.Height < diameter)
        {
            graphics.FillRectangle(brush, box);
            return;
        }
        using (var path = new System.Drawing.Drawing2D.GraphicsPath())
        {
            path.AddArc(box.Left, box.Top, diameter, diameter, 180, 90);
            path.AddArc(box.Right - diameter, box.Top, diameter, diameter, 270, 90);
            path.AddArc(box.Right - diameter, box.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(box.Left, box.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            graphics.FillPath(brush, path);
        }
    }

    private void LayoutKeys()
    {
        int width = ClientSize.Width - 28;
        int top = ClientSize.Height * 22 / 100;
        int height = ClientSize.Height * 66 / 100;
        for (int r = 0; r < keys.Length; r++)
        {
            bool actions = r == keys.Length - 1;
            int units = actions ? 100 : 12;
            int span = actions ? width : width * keys[r].Length / units;
            int left = 14 + (width - span) / 2;
            int offset = 0;
            for (int c = 0; c < keys[r].Length; c++)
            {
                int x = left + width * offset / units;
                offset += actions ? ActionWidths[c] : 1;
                int y = top + height * r / keys.Length;
                keys[r][c] = new Rectangle(x, y, Math.Max(1, left + width * offset / units - x - 5),
                    Math.Max(1, top + height * (r + 1) / keys.Length - y - 5));
            }
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left) return;
        for (int r = 0; r < keys.Length; r++)
        for (int c = 0; c < keys[r].Length; c++)
        {
            if (!keys[r][c].Contains(e.Location)) continue;
            row = r;
            column = c;
            ActivateKey();
            return;
        }
    }

    private static string[] Row(string letters)
    {
        return Array.ConvertAll(letters.ToCharArray(), c => c.ToString());
    }

    protected override CreateParams CreateParams
    {
        get { var cp = base.CreateParams; cp.ExStyle |= 0x08000080; // WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW 
            return cp;
        }
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == 0x21)
        {
            message.Result = new IntPtr(3);
            return;
        }
        base.WndProc(ref message);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            CloseOverlay();
        }
        base.OnFormClosing(e);
    }

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);

    private const uint KeepBounds = 0x0003, NoActivate = 0x0010, KeepZOrder = 0x0004;
    private const uint ShowWindow = 0x0040, HideWindow = 0x0080;

    internal bool IsOpen { get; private set; }

    internal void CloseOverlay()
    {
        if (!IsOpen) return;
        SetWindowPos(Handle, IntPtr.Zero, 0, 0, 0, 0, KeepBounds | KeepZOrder | NoActivate | HideWindow);
        IsOpen = false;
        preview.Clear();
        target = IntPtr.Zero;
    }

    internal void Open()
    {
        Point pointer = Cursor.Position;
        workArea = Screen.FromPoint(pointer).WorkingArea;
        Size = new Size(Math.Min(workArea.Width - 24, Math.Max(640, workArea.Width * 58 / 100)),
            Math.Min(workArea.Height - 24, Math.Max(300, workArea.Height * 40 / 100)));
        dockTop = false;
        if (new Rectangle(DockLocation(), Size).Contains(pointer)) dockTop = true;
        Location = DockLocation();
        shift = false;
        inputBlocked = false;
        preview.Clear();
        target = Native.GetForegroundWindow();
        row = 1;
        column = 0;
        LayoutKeys();
        Invalidate();
        IntPtr window = Handle;
        IsOpen = SetWindowPos(window, new IntPtr(-1), 0, 0, 0, 0, KeepBounds | NoActivate | ShowWindow);
    }

    private Point DockLocation()
    {
        return new Point(workArea.Left + (workArea.Width - Width) / 2,
            dockTop ? workArea.Top + 12 : workArea.Bottom - Height - 12);
    }

    internal void FlipDock()
    {
        dockTop = !dockTop;
        Point point = DockLocation();
        SetWindowPos(Handle, IntPtr.Zero, point.X, point.Y, 0, 0, 0x0001 | KeepZOrder | NoActivate);
    }

    internal void ToggleShift()
    {
        shift = !shift;
        Invalidate();
    }

    internal void TogglePreview()
    {
        hidePreview = !hidePreview;
        Invalidate();
    }

    internal void UpdateTarget()
    {
        IntPtr foreground = Native.GetForegroundWindow();
        if (foreground == target) return;
        target = foreground;
        preview.Clear();
        Invalidate();
    }

    internal void MoveSelection(ushort direction)
    {
        if (direction == 1 || direction == 2)
        {
            int newRow = (row + (direction == 1 ? -1 : 1) + keys.Length) % keys.Length;
            column = Math.Min(keys[newRow].Length - 1,
                (int)((column + 0.5) * keys[newRow].Length / keys[row].Length));
            row = newRow;
        }
        else
        {
            int length = keys[row].Length;
            column = (column + (direction == 4 ? -1 : 1) + length) % length;
        }
        RefreshKeys();
    }

    internal void ActivateKey()
    {
        string key = (shift ? upper : lower)[row][column];
        switch (key)
        {
            case "Close": CloseOverlay(); break;
            case "Shift": ToggleShift(); break;
            case "Space": Type(0x20); break;
            case "Backspace": Type(8); break;
            case "Enter": Type(13); break;
            case "Tab": Type(9); break;
            default:
                Type(key[0], true);
                shift = false;
                break;
        }
        RefreshKeys();
    }

    internal void Type(ushort key, bool unicode = false)
    {
        UpdateTarget();
        inputBlocked = !sendKey(key, unicode);
        if (!inputBlocked)
        {
            if (key == 8 && !unicode && preview.Length > 0) preview.Length--;
            else if (key == 9 && !unicode) preview.Clear();
            else if (unicode || key == 32 || key == 13)
            {
                if (preview.Length >= 256) preview.Remove(0, 1);
                preview.Append(key == 13 && !unicode ? '↵' : (char)key);
            }
        }
        Invalidate();
    }

    private void RefreshKeys() { Invalidate(); }
}

internal sealed class Desktop : ApplicationContext
{
    private const ushort A = 0x1000, B = 0x2000, X = 0x4000, Y = 0x8000;
    private const ushort DpadDown = 0x02, RightStick = 0x80, LB = 0x100, RB = 0x200;
    private const ushort ToggleChord = DpadDown | RB;
    private const long ChordHold = 1000;
    private readonly Keyboard keyboard = new Keyboard(Native.Key);
    private readonly Toast toast = new Toast();
    private readonly Settings settings;
    private readonly NotifyIcon tray;
    private readonly ContextMenuStrip menu = new ContextMenuStrip();
    private readonly ToolStripMenuItem status = new ToolStripMenuItem { Enabled = false };
    private sealed class ProcessProbe
    {
        public bool IsGame;
        public int Checks;
        public long NextCheck;
    }

    private readonly ToolStripMenuItem appMenu, appAuto, appOn, appOff;
    private readonly ToolStripMenuItem pause, pauseInGames, pauseInFullscreen;
    private readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer { Interval = 8 };
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly uint ownProcess = (uint)Process.GetCurrentProcess().Id;
    private readonly Dictionary<uint, ProcessProbe> moduleCache = new Dictionary<uint, ProcessProbe>();
    private int controller = -1;
    private long now, lastTick, nextScan, nextRepeat, nextAppCheck, nextListCheck, chordStart;
    private ushort previous, repeating;
    private bool paused, appPaused, ready, chordDown, chordFired, autoHintShown;
    private IntPtr lastForeground;
    private uint foregroundPid;
    private string foregroundApp = "", foregroundPath = "", currentApp = "", currentAppAutoReason = "", menuApp = "", appPauseReason = "";

    private bool InputPaused { get { return paused || appPaused; } }
    private double carryX, carryY, wheel, wheelX;

    // A = left, B = right, X = middle. Each button goes down on press and up on release, so all three can drag.
    private static readonly ushort[] ClickButtons = { A, B, X };
    private static readonly uint[] DownFlags = { 0x0002, 0x0008, 0x0020 };
    private static readonly uint[] UpFlags = { 0x0004, 0x0010, 0x0040 };
    private readonly bool[] held = new bool[3];

    internal Desktop()
    {
        settings = new Settings(Settings.DefaultFolder);
        settings.Load();
        settings.RefreshLists();

        menu.Items.Add(status);
        appMenu = new ToolStripMenuItem("This app");
        appAuto = new ToolStripMenuItem("Automatic", null, delegate { SetAppRule(menuApp, AppMode.Auto, false); });
        appOn = new ToolStripMenuItem("Always on", null, delegate { SetAppRule(menuApp, AppMode.On, false); });
        appOff = new ToolStripMenuItem("Always off", null, delegate { SetAppRule(menuApp, AppMode.Off, false); });
        appMenu.DropDownItems.Add(appAuto);
        appMenu.DropDownItems.Add(appOn);
        appMenu.DropDownItems.Add(appOff);
        menu.Items.Add(appMenu);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Controls", null, delegate {
            MessageBox.Show(
                "Left stick: pointer\nLeft trigger: precision (pull further for slower)\nA: click, hold to drag\nB: right-click, hold to right-drag\n" +
                "X: middle-click, hold to middle-drag\nRight stick: scroll up, down, left and right\nY: keyboard\n\n" +
                "Hold D-pad Down + RB for a second: turn PadDesk on or off for the app you're in. PadDesk remembers it.\n" +
                "PadDesk automatically turns off in Spine, emulators, PC games (windowed or fullscreen) and Steam Big Picture, " +
                "and stays on in browsers and video players. Change any app from this tray menu.\n\n" +
                "On the keyboard: D-pad selects, A types, B backspaces, X inserts a space, Y closes. LB shifts, RB docks the keyboard " +
                "on the other edge, and pressing the right stick hides or shows the typing preview.",
                "PadDesk controls", MessageBoxButtons.OK, MessageBoxIcon.Information);
        });
        pause = new ToolStripMenuItem("Pause everywhere", null, delegate { TogglePause(); });
        menu.Items.Add(pause);
        pauseInGames = new ToolStripMenuItem("Turn off in detected games")
        {
            Checked = settings.AutoGames,
            CheckOnClick = true
        };
        pauseInGames.CheckedChanged += delegate {
            settings.AutoGames = pauseInGames.Checked;
            settings.Save();
            UpdateAppPause(true);
        };
        menu.Items.Add(pauseInGames);
        pauseInFullscreen = new ToolStripMenuItem("Turn off in unknown fullscreen apps")
        {
            Checked = settings.AutoFullscreen,
            CheckOnClick = true
        };
        pauseInFullscreen.CheckedChanged += delegate {
            settings.AutoFullscreen = pauseInFullscreen.Checked;
            settings.Save();
            UpdateAppPause(true);
        };
        menu.Items.Add(pauseInFullscreen);
        var speeds = new ToolStripMenuItem("Pointer speed");
        foreach (double value in new[] { 0.6, 1.0, 1.5 })
        {
            var item = new ToolStripMenuItem(value == 0.6 ? "Slow" : value == 1.0 ? "Normal" : "Fast");
            item.Checked = Math.Abs(value - settings.Speed) < 0.001;
            item.Click += delegate {
                settings.Speed = value;
                settings.Save();
                foreach (ToolStripMenuItem sibling in speeds.DropDownItems) sibling.Checked = sibling == item;
            };
            speeds.DropDownItems.Add(item);
        }
        menu.Items.Add(speeds);
        menu.Items.Add("Open settings folder", null, delegate { OpenSettingsFolder(); });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, delegate { ExitThread(); });
        menu.Opening += delegate { UpdateAppPause(true); RefreshAppMenu(); };
        tray = new NotifyIcon { Icon = SystemIcons.Application, ContextMenuStrip = menu };
        UpdateStatus();
        tray.Visible = true;
        tray.ShowBalloonTip(4500, "PadDesk is running",
            "Press Y for the keyboard. Hold D-pad Down + RB to turn PadDesk on or off for the current app. Right-click the tray icon for more.",
            ToolTipIcon.Info);
        timer.Tick += Poll;
        timer.Start();
    }

    private void UpdateStatus()
    {
        status.Text = paused ? "Paused everywhere"
            : controller < 0 ? "Waiting for controller"
            : appPaused ? appPauseReason
            : "Controller connected";
        string tip = "PadDesk · " + status.Text;
        tray.Text = tip.Length > 63 ? tip.Substring(0, 62) + "…" : tip; // NotifyIcon.Text throws past 63 chars
        pause.Text = paused ? "Resume" : "Pause everywhere";
    }

    private void RefreshAppMenu()
    {
        menuApp = currentApp;
        appMenu.Visible = menuApp.Length != 0;
        if (!appMenu.Visible) return;
        appMenu.Text = "This app: " + menuApp;
        RuleSource source;
        AppMode fallback = settings.ResolveWithoutUser(menuApp, out source);
        string automatic = fallback == AppMode.On ? "on"
            : fallback == AppMode.Off ? "off"
            : currentAppAutoReason.Length != 0 ? "off — " + currentAppAutoReason
            : settings.AutoGames && settings.AutoFullscreen ? "on, off in games & fullscreen"
            : settings.AutoGames ? "on, off in games"
            : settings.AutoFullscreen ? "on, off when fullscreen" : "on";
        appAuto.Text = "Automatic (" + automatic + ")";
        AppMode chosen = settings.User.MatchExact(menuApp);
        appAuto.Checked = chosen == AppMode.Auto;
        appOn.Checked = chosen == AppMode.On;
        appOff.Checked = chosen == AppMode.Off;
    }

    private void OpenSettingsFolder()
    {
        try
        {
            Directory.CreateDirectory(settings.ListsFolder);
            if (!File.Exists(settings.FilePath)) settings.Save();
            Process.Start("explorer.exe", "\"" + settings.Folder + "\"");
        }
        catch (Exception error)
        {
            MessageBox.Show("Couldn't open " + settings.Folder + "\n\n" + error.Message, "PadDesk");
        }
    }

    private void SetAppRule(string app, AppMode mode, bool announce)
    {
        if (app.Length == 0) return;
        settings.User.Set(app, mode);
        settings.Save();
        UpdateAppPause(true);
        if (announce) ShowToast((appPaused ? "PadDesk off in " : "PadDesk on in ") + app);
    }

    private void ShowToast(string text)
    {
        IntPtr window = Native.GetForegroundWindow();
        Rectangle screen = window == IntPtr.Zero ? Screen.PrimaryScreen.Bounds : Screen.FromHandle(window).Bounds;
        toast.ShowMessage(text, screen);
    }

    private void TogglePause()
    {
        paused = !paused;
        ResetInput();
        UpdateStatus();
    }

    // Held D-pad Down + RB: resume if paused everywhere, otherwise flip PadDesk for the focused app and remember it.
    private void ChordAction()
    {
        if (paused)
        {
            TogglePause();
            ShowToast("PadDesk resumed");
            return;
        }
        UpdateAppPause(true);
        if (foregroundApp.Length == 0) return;
        SetAppRule(foregroundApp, appPaused ? AppMode.On : AppMode.Off, true);
    }

    private bool IsDetectedGameByModules(uint processId, string app, string exePath)
    {
        if (processId == 0 || GameDetector.IsKnownLauncher(app) || GameDetector.IsSystemExecutable(exePath))
            return false;
        ProcessProbe probe;
        if (moduleCache.TryGetValue(processId, out probe))
        {
            if (probe.IsGame) return true;
            if (probe.Checks >= 3 || now < probe.NextCheck) return false;
        }
        else
        {
            if (moduleCache.Count >= 64) moduleCache.Clear();
            probe = new ProcessProbe();
            moduleCache[processId] = probe;
        }
        probe.Checks++;
        // Re-check twice over the next few seconds in case a game loads XInput/Steam overlay after opening its window.
        probe.NextCheck = now + (probe.Checks == 1 ? 1500 : 3500);
        probe.IsGame = GameDetector.IsGameModules(Native.ModulesOf(processId), app);
        return probe.IsGame;
    }

    private void UpdateAppPause(bool force)
    {
        IntPtr foreground = Native.GetForegroundWindow();
        bool newWindow = foreground != lastForeground;
        if (!force && !newWindow && now < nextAppCheck) return;
        nextAppCheck = now + 150;
        if (newWindow || force || foregroundApp == "applicationframehost")
        {
            lastForeground = foreground;
            uint process = Native.RealProcessIdOf(foreground);
            foregroundPid = process;
            // PadDesk's own windows (menus, dialogs) never switch it off.
            if (process == ownProcess)
            {
                foregroundApp = "";
                foregroundPath = "";
            }
            else
            {
                foregroundApp = Native.ProcessNameOf(process, out foregroundPath);
                if (foregroundApp.Length != 0 && !Native.IsShellSurface(foreground)) currentApp = foregroundApp;
            }
        }

        bool off = false, guessed = false;
        string reason = "", autoTag = "";
        if (foregroundApp.Length != 0)
        {
            RuleSource source;
            AppMode mode = settings.Resolve(foregroundApp, out source);
            if (mode == AppMode.Off)
            {
                off = true;
                reason = "Off in " + foregroundApp;
            }
            else if (mode == AppMode.Auto)
            {
                if (settings.AutoGames && GameDetector.IsSteamBigPicture(foreground, foregroundApp))
                {
                    off = guessed = true;
                    reason = "Off in Steam Big Picture";
                    autoTag = "Big Picture";
                }
                else if (settings.AutoGames && GameDetector.IsGamePath(foregroundPath, foregroundApp))
                {
                    off = guessed = true;
                    reason = "Off in game " + foregroundApp;
                    autoTag = "game detected";
                }
                else if (settings.AutoGames && IsDetectedGameByModules(foregroundPid, foregroundApp, foregroundPath))
                {
                    off = guessed = true;
                    reason = "Off in game " + foregroundApp;
                    autoTag = "game detected";
                }
                else if (settings.AutoFullscreen && Native.IsFullscreenWindow(foreground))
                {
                    off = guessed = true;
                    reason = "Off in fullscreen app " + foregroundApp;
                    autoTag = "fullscreen";
                }
            }
        }
        else if (foregroundPid != 0 && foregroundPid != ownProcess && settings.AutoFullscreen &&
            Native.IsFullscreenWindow(foreground))
        {
            off = guessed = true;
            reason = "Off in fullscreen app";
        }
        if (foregroundApp.Length != 0 && foregroundApp == currentApp && !Native.IsShellSurface(foreground))
            currentAppAutoReason = autoTag;

        bool changed = off != appPaused;
        bool reasonChanged = reason != appPauseReason;
        appPauseReason = reason;
        if (changed)
        {
            appPaused = off;
            ResetInput();
        }
        if (changed || reasonChanged) UpdateStatus();
        if (changed && guessed && !autoHintShown)
        {
            autoHintShown = true;
            ShowToast("PadDesk is off in " + (foregroundApp.Length != 0 ? foregroundApp : "this app") +
                ". Hold D-pad Down + RB to keep it on here.");
        }
    }

    private void ResetInput()
    {
        ReleaseDrag();
        keyboard.CloseOverlay();
        ready = false;
        previous = repeating = 0;
        carryX = carryY = wheel = wheelX = 0;
    }

    private void ReleaseDrag()
    {
        for (int i = 0; i < held.Length; i++)
            if (held[i] && Native.Mouse(UpFlags[i])) held[i] = false;
    }

    // Holding the left trigger slows the pointer and scrolling, down to 30% at a full pull.
    internal static double PrecisionFactor(byte trigger)
    {
        const int threshold = 30; // XINPUT_GAMEPAD_TRIGGER_THRESHOLD
        if (trigger <= threshold) return 1;
        return 1 - 0.7 * (trigger - threshold) / (255.0 - threshold);
    }

    private static int Scroll(ref double carry, double amount, double dt)
    {
        if (amount == 0) { carry = 0; return 0; }
        carry += amount * 12 * dt;
        int steps = (int)carry;
        carry -= steps;
        return steps;
    }

    internal static void Stick(short x, short y, double deadzone, out double horizontal, out double vertical)
    {
        double magnitude = Math.Sqrt((double)x * x + (double)y * y);
        if (magnitude <= deadzone)
        {
            horizontal = vertical = 0;
            return;
        }
        double scaled = (Math.Min(magnitude, 32767) - deadzone) / (32767 - deadzone);
        double response = scaled * scaled / magnitude;
        horizontal = x * response;
        vertical = y * response;
    }

    private void Poll(object sender, EventArgs args)
    {
        now = clock.ElapsedMilliseconds;
        double dt = Math.Min(now - lastTick, 40) / 1000.0;
        lastTick = now;
        if (now >= nextListCheck)
        {
            nextListCheck = now + 2000;
            if (settings.RefreshLists()) UpdateAppPause(true);
        }
        UpdateAppPause(false);

        Native.State state;
        if (controller < 0)
        {
            ReleaseDrag();
            if (now < nextScan) return;
            nextScan = now + 800;
            for (uint index = 0; index < 4; index++)
            {
                if (Native.XInputGetState(index, out state) != 0) continue;
                controller = (int)index;
                chordDown = chordFired = true; // a chord already held while connecting doesn't count
                UpdateAppPause(true);
                UpdateStatus();
                break;
            }
            return;
        }
        if (Native.XInputGetState((uint)controller, out state) != 0)
        {
            controller = -1;
            ResetInput();
            UpdateStatus();
            return;
        }

        ushort buttons = state.Buttons;
        bool chord = (buttons & ToggleChord) == ToggleChord;
        if (chord && !chordDown)
        {
            chordStart = now;
            chordFired = false;
        }
        chordDown = chord;
        // Requiring a hold keeps brief presses in games from flipping PadDesk.
        if (chord && !chordFired && now - chordStart >= ChordHold)
        {
            chordFired = true;
            ChordAction();
        }

        double x, y, scrollX, scrollY;
        Stick(state.LX, state.LY, 8000, out x, out y);
        Stick(state.RX, state.RY, 9500, out scrollX, out scrollY);
        if (!ready || InputPaused)
        {
            ReleaseDrag();
            ready = buttons == 0 && x == 0 && y == 0 && scrollX == 0 && scrollY == 0;
            previous = buttons;
            return;
        }

        ushort pressed = (ushort)(buttons & ~previous);
        previous = buttons;
        if ((pressed & Y) != 0)
        {
            ReleaseDrag();
            if (keyboard.IsOpen) keyboard.CloseOverlay(); else keyboard.Open();
            carryX = carryY = wheel = wheelX = 0;
            repeating = 0;
            ready = false;
            return;
        }
        if (keyboard.IsOpen)
        {
            ReleaseDrag();
            HandleKeyboard(buttons, pressed);
            return;
        }

        double precision = PrecisionFactor(state.LeftTrigger);
        MovePointer(x * precision, y * precision, dt);
        for (int i = 0; i < ClickButtons.Length; i++)
        {
            if (held[i] && (buttons & ClickButtons[i]) == 0 && Native.Mouse(UpFlags[i])) held[i] = false;
            if ((pressed & ClickButtons[i]) != 0 && !held[i]) held[i] = Native.Mouse(DownFlags[i]);
        }

        // Scroll along the stick's main direction only, so scrolling down a page doesn't drift sideways.
        if (Math.Abs(scrollX) < Math.Abs(scrollY) * 0.5) scrollX = 0;
        else if (Math.Abs(scrollY) < Math.Abs(scrollX) * 0.5) scrollY = 0;
        int vertical = Scroll(ref wheel, scrollY * precision, dt);
        if (vertical != 0) Native.Mouse(0x0800, vertical * 120);    // MOUSEEVENTF_WHEEL, positive = up
        int horizontal = Scroll(ref wheelX, scrollX * precision, dt);
        if (horizontal != 0) Native.Mouse(0x1000, horizontal * 120); // MOUSEEVENTF_HWHEEL, positive = right
    }

    private void MovePointer(double x, double y, double dt)
    {
        carryX = x == 0 ? 0 : carryX + x * dt * 1500 * settings.Speed;
        carryY = y == 0 ? 0 : carryY - y * dt * 1500 * settings.Speed;
        int dx = (int)carryX, dy = (int)carryY;
        carryX -= dx;
        carryY -= dy;
        if (dx == 0 && dy == 0) return;
        Point position = Cursor.Position;
        Cursor.Position = new Point(position.X + dx, position.Y + dy);
    }

    private void HandleKeyboard(ushort buttons, ushort pressed)
    {
        keyboard.UpdateTarget();
        if ((pressed & LB) != 0) keyboard.ToggleShift();
        if ((pressed & RB) != 0) keyboard.FlipDock();
        if ((pressed & RightStick) != 0) keyboard.TogglePreview();

        ushort held = (ushort)(buttons & (15 | B));
        // Repeat a single held direction or Backspace. Ignore opposing directions.
        if ((held & (held - 1)) != 0) held = 0;
        if (held != repeating)
        {
            repeating = held;
            nextRepeat = now + 330;
            if (held != 0) RepeatKey(held);
        }
        else if (held != 0 && now >= nextRepeat)
        {
            nextRepeat = now + 90;
            RepeatKey(held);
        }

        if ((pressed & A) != 0)
        {
            keyboard.ActivateKey();
            if (!keyboard.IsOpen) ready = false;
        }
        if (keyboard.IsOpen && (pressed & X) != 0) keyboard.Type(32);
    }

    private void RepeatKey(ushort key)
    {
        if (key == B) keyboard.Type(8);
        else keyboard.MoveSelection(key);
    }

    protected override void ExitThreadCore()
    {
        timer.Stop();
        ReleaseDrag();
        tray.Dispose();
        keyboard.Dispose();
        toast.Dispose();
        menu.Dispose();
        timer.Dispose();
        base.ExitThreadCore();
    }
}

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        Native.SetProcessDpiAwarenessContext(new IntPtr(-4));
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        if (args.Length == 1 && args[0] == "--check")
            return Check();
        using (var mutex = new Mutex(false, @"Local\PadDesk"))
        {
            bool acquired;
            try { acquired = mutex.WaitOne(0); }
            catch (AbandonedMutexException) { acquired = true; }
            if (!acquired)
            {
                MessageBox.Show("PadDesk is already running. Look for its icon in the notification area.", "PadDesk");
                return 0;
            }
            try
            {
                using (var desktop = new Desktop()) Application.Run(desktop);
            }
            finally { mutex.ReleaseMutex(); }
        }
        return 0;
    }

    // Rule resolution, list merging and settings round-trip. Touches only a temp folder.
    internal static int CheckRules()
    {
        string folder = Path.Combine(Path.GetTempPath(), "PadDeskCheck-" + Guid.NewGuid().ToString("N"));
        try
        {
            RuleSource source;
            var settings = new Settings(folder);
            settings.Load();
            settings.RefreshLists();
            if (settings.Resolve("chrome", out source) != AppMode.On || source != RuleSource.Default) return 10;
            if (settings.Resolve("duckstation-qt-x64-releaseltcg", out source) != AppMode.Off) return 11;
            if (settings.Resolve("spine", out source) != AppMode.Off || source != RuleSource.Default) return 28;
            if (settings.Resolve("notepad", out source) != AppMode.Auto || source != RuleSource.None) return 12;
            if (settings.Resolve("", out source) != AppMode.Auto) return 13;

            Directory.CreateDirectory(settings.ListsFolder);
            File.WriteAllLines(Path.Combine(settings.ListsFolder, "frontend.txt"),
                new[] { "# rules from a frontend", "off MyFrontend.exe", "on chrome", "on mygame", "off fancyemu*", "junk" });
            File.WriteAllLines(Path.Combine(settings.ListsFolder, "other.txt"), new[] { "off mygame" });
            File.WriteAllText(Path.Combine(settings.ListsFolder, "ignored.md"), "off chrome");
            if (!settings.RefreshLists()) return 14;
            if (settings.RefreshLists()) return 15;
            if (settings.Resolve("myfrontend", out source) != AppMode.Off || source != RuleSource.List) return 16;
            if (settings.Resolve("chrome", out source) != AppMode.On || source != RuleSource.List) return 17;
            if (settings.Resolve("mygame", out source) != AppMode.Off) return 18;
            if (settings.Resolve("fancyemu-qt", out source) != AppMode.Off) return 19;

            settings.User.Set("myfrontend", AppMode.On);
            settings.User.Set("pcsx2*", AppMode.On);
            settings.User.Set("Paint.exe", AppMode.Off);
            settings.Speed = 1.5;
            settings.AutoGames = false;
            settings.AutoFullscreen = false;
            if (!settings.Save() || !settings.Save()) return 20;

            var reloaded = new Settings(folder);
            reloaded.Load();
            reloaded.RefreshLists();
            if (reloaded.Resolve("myfrontend", out source) != AppMode.On || source != RuleSource.User) return 21;
            if (reloaded.Resolve("pcsx2-qt", out source) != AppMode.On || source != RuleSource.User) return 22;
            if (reloaded.Resolve("paint", out source) != AppMode.Off) return 23;
            if (Math.Abs(reloaded.Speed - 1.5) > 0.0001 || reloaded.AutoGames || reloaded.AutoFullscreen) return 24;
            reloaded.User.Set("myfrontend", AppMode.Auto);
            if (reloaded.Resolve("myfrontend", out source) != AppMode.Off || source != RuleSource.List) return 25;

            File.Delete(Path.Combine(settings.ListsFolder, "frontend.txt"));
            if (!reloaded.RefreshLists()) return 26;
            if (reloaded.Resolve("chrome", out source) != AppMode.On || source != RuleSource.Default) return 27;
            return 0;
        }
        finally
        {
            try { Directory.Delete(folder, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static int Check()
    {
        if (!Native.LayoutValid) return 1;
        int rules = CheckRules();
        if (rules != 0) return rules;
        Native.State state;
        uint result = Native.XInputGetState(0, out state);
        if (result != 0 && result != 1167) return 2;
        double x, y;
        Desktop.Stick(4000, 4000, 8000, out x, out y);
        if (x != 0 || y != 0) return 3;
        Desktop.Stick(-32768, 0, 8000, out x, out y);
        if (Math.Abs(x + 1) > 0.0001 || y != 0) return 4;
        var secondary = new Rectangle(-1920, 0, 1920, 1080);
        if (!Native.CoversMonitor(secondary, secondary) ||
            !Native.CoversMonitor(new Rectangle(-1928, -8, 1936, 1096), secondary) ||
            Native.CoversMonitor(new Rectangle(-1920, 0, 1920, 1040), secondary) ||
            Native.CoversMonitor(new Rectangle(-3840, 0, 3840, 1080), secondary) ||
            Native.IsFullscreenWindow(IntPtr.Zero))
            return 9;
        if (!GameDetector.IsGamePath(@"D:\SteamLibrary\steamapps\common\Hades\x64\Hades.exe", "hades") ||
            !GameDetector.IsGamePath(@"C:\Program Files\Epic Games\AlanWake2\AlanWake2.exe", "alanwake2") ||
            GameDetector.IsGamePath(@"C:\Program Files (x86)\Epic Games\Launcher\Portal\Binaries\Win64\EpicGamesLauncher.exe", "epicgameslauncher") ||
            !GameDetector.IsGamePath(@"C:\Custom\Palworld-Win64-Shipping.exe", "palworld-win64-shipping") ||
            GameDetector.IsGamePath(@"C:\Windows\System32\notepad.exe", "notepad") ||
            !GameDetector.IsGameModules(new[] { "kernel32.dll", "UnityPlayer.dll" }, "myindie") ||
            !GameDetector.IsGameModules(new[] { "d3d11.dll", "xinput1_4.dll" }, "myindie") ||
            GameDetector.IsGameModules(new[] { "d3d11.dll", "xinput1_4.dll", "libcef.dll" }, "somebrowserapp") ||
            GameDetector.IsGameModules(new[] { "d3d11.dll", "xinput1_4.dll", "steam_api64.dll" }, "steamwebhelper"))
            return 29;
        uint selfPid = (uint)Process.GetCurrentProcess().Id;
        string selfPath;
        if (Native.ProcessNameOf(selfPid, out selfPath).Length == 0 || selfPath.Length == 0) return 7;
        List<string> selfModules = Native.ModulesOf(selfPid);
        if (selfModules == null || selfModules.Count == 0) return 30;
        if (Desktop.PrecisionFactor(0) != 1 || Desktop.PrecisionFactor(30) != 1 ||
            Math.Abs(Desktop.PrecisionFactor(255) - 0.3) > 0.0001 || Desktop.PrecisionFactor(140) >= 1)
            return 8;
        string recorded = "";
        bool acceptKey = true;
        using (var editor = new Form())
        using (var keyboard = new Keyboard(delegate(ushort key, bool unicode) {
            recorded += (unicode ? "u" : "v") + key + " ";
            return acceptKey;
        }))
        using (var input = new TextBox { Dock = DockStyle.Top })
        using (var timer = new System.Windows.Forms.Timer { Interval = 150 })
        {
            int exitCode = 5;
            editor.Controls.Add(input);
            editor.Shown += delegate { editor.Activate(); input.Focus(); timer.Start(); };
            timer.Tick += delegate {
                timer.Stop();
                IntPtr foreground = Native.GetForegroundWindow();
                if (input.Focused)
                {
                    keyboard.Open();
                    int beforeDock = keyboard.Top;
                    keyboard.FlipDock();
                    if (keyboard.IsOpen && Native.GetForegroundWindow() == foreground && keyboard.Top != beforeDock)
                    {
                        keyboard.ActivateKey();
                        keyboard.MoveSelection(8);
                        keyboard.ToggleShift();
                        keyboard.ActivateKey();
                        keyboard.Type(8);
                        keyboard.Type(32);
                        keyboard.Type('!', true);
                        acceptKey = false;
                        keyboard.Type('x', true);
                        keyboard.TogglePreview();
                        keyboard.TogglePreview();
                        bool previewMatches = keyboard.PreviewText == "q !";
                        keyboard.CloseOverlay();
                        keyboard.Open();
                        exitCode = previewMatches && keyboard.PreviewText == "" && input.Focused &&
                            recorded == "u113 u87 v8 v32 u33 u120 " ? 0 : 6;
                    }
                }
                editor.Close();
            };
            Application.Run(editor);
            return exitCode;
        }
    }
}
