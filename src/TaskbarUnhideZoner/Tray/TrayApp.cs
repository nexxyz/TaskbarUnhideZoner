using System.Runtime.InteropServices;
using System.Threading;
using TaskbarUnhideZoner.Config;
using TaskbarUnhideZoner.Logging;
using TaskbarUnhideZoner.Models;
using TaskbarUnhideZoner.Runtime;
using TaskbarUnhideZoner.UI;

namespace TaskbarUnhideZoner.Tray;

internal sealed class TrayApp : ApplicationContext
{
    private readonly RuntimeController _runtime;
    private readonly NotifyIcon _notifyIcon;
    private readonly SynchronizationContext? _uiContext;

    private ToolStripMenuItem? _enabledItem;
    private ToolStripMenuItem? _delayMenu;
    private ToolStripMenuItem? _zoneMenu;
    private ToolStripMenuItem? _drawHotZoneItem;
    private ToolStripMenuItem? _autohideInfoItem;
    private ToolStripMenuItem? _backendInfoItem;

    private bool _initializing = true;

    public TrayApp(RuntimeController runtime)
    {
        _runtime = runtime;
        _uiContext = SynchronizationContext.Current;
        _notifyIcon = new NotifyIcon
        {
            Icon = LoadTrayIcon(),
            Visible = true,
            Text = "Taskbar Unhide Zoner",
            ContextMenuStrip = BuildMenu()
        };

        _runtime.StateChanged += OnRuntimeStateChanged;

        _notifyIcon.MouseUp += (_, e) =>
        {
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            _runtime.RefreshAutohideState();
            _notifyIcon.ContextMenuStrip?.Show(Cursor.Position);
        };

        _initializing = false;
        RefreshUiState();
    }

    private static Icon LoadTrayIcon()
    {
        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "assets", "Taskbar Unhide Zoner.png");
            if (File.Exists(iconPath))
            {
                using var bitmap = new Bitmap(iconPath);
                var handle = bitmap.GetHicon();
                try
                {
                    using var icon = Icon.FromHandle(handle);
                    return (Icon)icon.Clone();
                }
                finally
                {
                    DestroyIcon(handle);
                }
            }
        }
        catch
        {
        }

        return SystemIcons.Application;
    }

    private void OnRuntimeStateChanged(object? sender, EventArgs e)
    {
        if (_uiContext == null)
        {
            RefreshUiState();
            return;
        }

        _uiContext.Post(_ => RefreshUiState(), null);
    }

    private void RefreshUiState()
    {
        if (_enabledItem == null)
        {
            return;
        }

        var suspendedByAutohide = _runtime.IsAutohideOffSuspended;
        var zoneMode = _runtime.Config.Zone.Mode == ZoneMode.EdgeBar
            ? _runtime.Config.Zone.Edge.ToString()
            : "Hot Zone";

        _notifyIcon.Text = suspendedByAutohide
            ? "Taskbar Unhide Zoner (Autohide Off)"
            : _runtime.Config.Enabled
                ? $"Taskbar Unhide Zoner ({zoneMode})"
                : "Taskbar Unhide Zoner (Disabled)";

        _enabledItem.Enabled = !suspendedByAutohide;
        _enabledItem.Checked = _runtime.Config.Enabled && !suspendedByAutohide;
        _enabledItem.Text = suspendedByAutohide
            ? "Disabled - taskbar autohide is off"
            : "Enable Taskbar Unhide Zoner";

        var interactive = !suspendedByAutohide;
        if (_delayMenu != null) _delayMenu.Enabled = interactive;
        if (_zoneMenu != null) _zoneMenu.Enabled = interactive;
        if (_drawHotZoneItem != null) _drawHotZoneItem.Enabled = interactive;

        if (_autohideInfoItem != null)
        {
            _autohideInfoItem.Visible = suspendedByAutohide;
        }

        if (_backendInfoItem != null)
        {
            _backendInfoItem.Text = $"Backend: {_runtime.ActiveBackend}";
        }
    }

    private void Exit()
    {
        _runtime.StateChanged -= OnRuntimeStateChanged;

        try
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }
        catch
        {
        }

        Application.Exit();
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Opening += (_, _) =>
        {
            _runtime.RefreshAutohideState();
            RefreshUiState();
        };

        _enabledItem = new ToolStripMenuItem("Enable Taskbar Unhide Zoner")
        {
            CheckOnClick = true,
            Checked = _runtime.Config.Enabled
        };
        _enabledItem.CheckedChanged += (_, _) =>
        {
            if (_initializing)
            {
                return;
            }

            _runtime.SetEnabled(_enabledItem.Checked);
            RefreshUiState();
        };

        var startupItem = new ToolStripMenuItem("Start with Windows")
        {
            CheckOnClick = true,
            Checked = _runtime.Config.StartWithWindows
        };
        startupItem.CheckedChanged += (_, _) =>
        {
            if (_initializing)
            {
                return;
            }

            _runtime.SetStartup(startupItem.Checked);
        };

        _delayMenu = BuildDelayMenu();
        _zoneMenu = BuildZoneMenu();

        var openConfig = new ToolStripMenuItem("Open Config");
        openConfig.Click += (_, _) => OpenFile(Paths.ConfigFilePath);

        _drawHotZoneItem = new ToolStripMenuItem("Draw Hot Zone...");
        _drawHotZoneItem.Click += (_, _) =>
        {
            var rect = HotZoneOverlayForm.SelectRectangle();
            if (rect == null)
            {
                return;
            }

            _runtime.SetHotZone(rect.Value);
            _runtime.ReinitializeDetection();
            RefreshUiState();
        };

        _backendInfoItem = new ToolStripMenuItem($"Backend: {_runtime.ActiveBackend}")
        {
            Enabled = false
        };

        _autohideInfoItem = new ToolStripMenuItem("Turn on taskbar autohide in Windows settings to use this app")
        {
            Enabled = false,
            Visible = false
        };

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => Exit();

        menu.Items.Add(_enabledItem);
        menu.Items.Add(startupItem);
        menu.Items.Add(_delayMenu);
        menu.Items.Add(_zoneMenu);
        menu.Items.Add(_drawHotZoneItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_autohideInfoItem);
        menu.Items.Add(_backendInfoItem);
        menu.Items.Add(openConfig);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);

        return menu;
    }

    private ToolStripMenuItem BuildDelayMenu()
    {
        var menu = new ToolStripMenuItem("Trigger Delay");
        var quick = new ToolStripMenuItem("Quick") { CheckOnClick = true };
        var normal = new ToolStripMenuItem("Default") { CheckOnClick = true };
        var longDelay = new ToolStripMenuItem("Long") { CheckOnClick = true };

        void SetPreset(DelayPreset preset)
        {
            if (_initializing)
            {
                return;
            }

            _runtime.SetDelayPreset(preset);
            var selectedMs = _runtime.Config.TriggerDelayMs;
            quick.Checked = selectedMs == _runtime.Config.DelayPresets.QuickMs;
            normal.Checked = selectedMs == _runtime.Config.DelayPresets.DefaultMs;
            longDelay.Checked = selectedMs == _runtime.Config.DelayPresets.LongMs;
        }

        quick.Click += (_, _) => SetPreset(DelayPreset.Quick);
        normal.Click += (_, _) => SetPreset(DelayPreset.Default);
        longDelay.Click += (_, _) => SetPreset(DelayPreset.Long);

        var delayMs = _runtime.Config.TriggerDelayMs;
        quick.Checked = delayMs == _runtime.Config.DelayPresets.QuickMs;
        normal.Checked = delayMs == _runtime.Config.DelayPresets.DefaultMs;
        longDelay.Checked = delayMs == _runtime.Config.DelayPresets.LongMs;

        menu.DropDownItems.Add(quick);
        menu.DropDownItems.Add(normal);
        menu.DropDownItems.Add(longDelay);
        return menu;
    }

    private ToolStripMenuItem BuildZoneMenu()
    {
        var menu = new ToolStripMenuItem("Zone");

        var edgeMenu = new ToolStripMenuItem("Edge Bar");
        var top = new ToolStripMenuItem("Top") { CheckOnClick = true };
        var bottom = new ToolStripMenuItem("Bottom") { CheckOnClick = true };
        var left = new ToolStripMenuItem("Left") { CheckOnClick = true };
        var right = new ToolStripMenuItem("Right") { CheckOnClick = true };
        var hotZone = new ToolStripMenuItem("Hot Zone") { CheckOnClick = true };

        void SetEdge(EdgePosition edge)
        {
            if (_initializing)
            {
                return;
            }

            _runtime.SetEdgePosition(edge);
            _runtime.ReinitializeDetection();
            UpdateChecks();
            RefreshUiState();
        }

        void SetHotZone()
        {
            if (_initializing)
            {
                return;
            }

            _runtime.SetZoneMode(ZoneMode.HotZone);
            _runtime.ReinitializeDetection();
            UpdateChecks();
            RefreshUiState();
        }

        void UpdateChecks()
        {
            var isEdge = _runtime.Config.Zone.Mode == ZoneMode.EdgeBar;
            top.Checked = isEdge && _runtime.Config.Zone.Edge == EdgePosition.Top;
            bottom.Checked = isEdge && _runtime.Config.Zone.Edge == EdgePosition.Bottom;
            left.Checked = isEdge && _runtime.Config.Zone.Edge == EdgePosition.Left;
            right.Checked = isEdge && _runtime.Config.Zone.Edge == EdgePosition.Right;
            hotZone.Checked = _runtime.Config.Zone.Mode == ZoneMode.HotZone;
        }

        top.Click += (_, _) => SetEdge(EdgePosition.Top);
        bottom.Click += (_, _) => SetEdge(EdgePosition.Bottom);
        left.Click += (_, _) => SetEdge(EdgePosition.Left);
        right.Click += (_, _) => SetEdge(EdgePosition.Right);
        hotZone.Click += (_, _) => SetHotZone();

        edgeMenu.DropDownItems.Add(top);
        edgeMenu.DropDownItems.Add(bottom);
        edgeMenu.DropDownItems.Add(left);
        edgeMenu.DropDownItems.Add(right);
        menu.DropDownItems.Add(edgeMenu);
        menu.DropDownItems.Add(hotZone);

        UpdateChecks();
        return menu;
    }

    private static void OpenFile(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            RollingFileLogger.Error($"Failed to open file '{path}': {ex.Message}");
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
