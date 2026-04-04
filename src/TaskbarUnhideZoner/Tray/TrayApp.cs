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
    private readonly Form _menuAnchor;
    private readonly EventWaitHandle _alreadyRunningEvent;
    private readonly RegisteredWaitHandle _alreadyRunningWait;

    private ToolStripMenuItem? _enabledItem;
    private ToolStripMenuItem? _delayMenu;
    private ToolStripMenuItem? _assistMenu;
    private ToolStripMenuItem? _zoneMenu;
    private ToolStripMenuItem? _autohideInfoItem;

    private bool _initializing = true;

    public TrayApp(RuntimeController runtime, EventWaitHandle alreadyRunningEvent)
    {
        _runtime = runtime;
        _alreadyRunningEvent = alreadyRunningEvent;
        _uiContext = SynchronizationContext.Current;
        _menuAnchor = CreateMenuAnchor();
        _notifyIcon = new NotifyIcon
        {
            Icon = LoadTrayIcon(),
            Visible = true,
            Text = "Taskbar Unhide Zoner",
            ContextMenuStrip = BuildMenu()
        };

        _alreadyRunningWait = ThreadPool.RegisterWaitForSingleObject(
            _alreadyRunningEvent,
            (_, _) => OnAlreadyRunningSignal(),
            null,
            Timeout.Infinite,
            executeOnlyOnce: false);

        _runtime.StateChanged += OnRuntimeStateChanged;

        _notifyIcon.MouseUp += (_, e) =>
        {
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            _runtime.RefreshAutohideState();
            ShowTrayMenuAtCursor();
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
        var monitorUnavailable = !string.IsNullOrWhiteSpace(_runtime.MonitoringError);
        var interactive = !suspendedByAutohide && !monitorUnavailable;

        _notifyIcon.Text = BuildTrayText(suspendedByAutohide, monitorUnavailable);
        _enabledItem.Enabled = interactive;
        _enabledItem.Checked = _runtime.Config.Enabled && interactive;
        _enabledItem.Text = BuildEnabledItemText(suspendedByAutohide, monitorUnavailable);

        if (_delayMenu != null) _delayMenu.Enabled = interactive;
        if (_assistMenu != null) _assistMenu.Enabled = interactive;
        if (_zoneMenu != null) _zoneMenu.Enabled = interactive;

        if (_autohideInfoItem != null)
        {
            _autohideInfoItem.Visible = suspendedByAutohide || monitorUnavailable;
            _autohideInfoItem.Text = suspendedByAutohide
                ? "Turn on taskbar autohide in Windows settings to use this app"
                : "Mouse hook monitoring could not start";
        }

    }

    private void Exit()
    {
        _runtime.StateChanged -= OnRuntimeStateChanged;

        try
        {
            _alreadyRunningWait.Unregister(null);
            _menuAnchor.Dispose();
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
        menu.Closed += (_, _) =>
        {
            PostMessage(_menuAnchor.Handle, WmNull, IntPtr.Zero, IntPtr.Zero);
        };
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
        _assistMenu = BuildTriggerAssistMenu();
        _zoneMenu = BuildZoneMenu();

        var openConfig = new ToolStripMenuItem("Open Config");
        openConfig.Click += (_, _) => OpenFile(Paths.ConfigFilePath);

        var openLog = new ToolStripMenuItem("Open Log");
        openLog.Click += (_, _) => OpenFile(Paths.LogFilePath);

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
        menu.Items.Add(_assistMenu);
        menu.Items.Add(_zoneMenu);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_autohideInfoItem);
        menu.Items.Add(openConfig);
        menu.Items.Add(openLog);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);

        return menu;
    }

    private void OnAlreadyRunningSignal()
    {
        if (_uiContext == null)
        {
            ShowAlreadyRunningNotification();
            return;
        }

        _uiContext.Post(_ => ShowAlreadyRunningNotification(), null);
    }

    private void ShowAlreadyRunningNotification()
    {
        _notifyIcon.BalloonTipTitle = "Taskbar Unhide Zoner";
        _notifyIcon.BalloonTipText = "Taskbar Unhide Zoner is already running in the notification area.";
        _notifyIcon.BalloonTipIcon = ToolTipIcon.Info;
        _notifyIcon.ShowBalloonTip(2500);
    }

    private void ShowTrayMenuAtCursor()
    {
        var menu = _notifyIcon.ContextMenuStrip;
        if (menu == null)
        {
            return;
        }

        SetForegroundWindow(_menuAnchor.Handle);
        menu.Show(Cursor.Position);
    }

    private static Form CreateMenuAnchor()
    {
        var form = new Form
        {
            ShowInTaskbar = false,
            Opacity = 0,
            FormBorderStyle = FormBorderStyle.None,
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-32000, -32000),
            Size = new Size(1, 1)
        };

        form.Load += (_, _) => form.Hide();
        form.Show();
        form.Hide();
        return form;
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

    private ToolStripMenuItem BuildTriggerAssistMenu()
    {
        var menu = new ToolStripMenuItem("Trigger Assist");
        var off = new ToolStripMenuItem("Off") { CheckOnClick = true };
        var low = new ToolStripMenuItem("Low") { CheckOnClick = true };
        var medium = new ToolStripMenuItem("Medium") { CheckOnClick = true };
        var strong = new ToolStripMenuItem("Strong") { CheckOnClick = true };

        void SetPreset(TriggerAssistPreset preset)
        {
            if (_initializing)
            {
                return;
            }

            _runtime.SetTriggerAssistPreset(preset);
            var selectedPreset = GetSelectedTriggerAssistPreset();
            off.Checked = selectedPreset == TriggerAssistPreset.Off;
            low.Checked = selectedPreset == TriggerAssistPreset.Low;
            medium.Checked = selectedPreset == TriggerAssistPreset.Medium;
            strong.Checked = selectedPreset == TriggerAssistPreset.Strong;
        }

        off.Click += (_, _) => SetPreset(TriggerAssistPreset.Off);
        low.Click += (_, _) => SetPreset(TriggerAssistPreset.Low);
        medium.Click += (_, _) => SetPreset(TriggerAssistPreset.Medium);
        strong.Click += (_, _) => SetPreset(TriggerAssistPreset.Strong);

        var selected = GetSelectedTriggerAssistPreset();
        off.Checked = selected == TriggerAssistPreset.Off;
        low.Checked = selected == TriggerAssistPreset.Low;
        medium.Checked = selected == TriggerAssistPreset.Medium;
        strong.Checked = selected == TriggerAssistPreset.Strong;

        menu.DropDownItems.Add(off);
        menu.DropDownItems.Add(low);
        menu.DropDownItems.Add(medium);
        menu.DropDownItems.Add(strong);
        return menu;
    }

    private TriggerAssistPreset GetSelectedTriggerAssistPreset()
    {
        var assist = _runtime.Config.Trigger.Assist;
        if (!assist.Enabled)
        {
            return TriggerAssistPreset.Off;
        }

        if (assist.MinDelayPercent == 90 && Math.Abs(assist.CurveExponent - 3.0) < 0.001)
        {
            return TriggerAssistPreset.Low;
        }

        if (assist.MinDelayPercent == 60 && Math.Abs(assist.CurveExponent - 1.7) < 0.001)
        {
            return TriggerAssistPreset.Medium;
        }

        if (assist.MinDelayPercent == 10 && Math.Abs(assist.CurveExponent - 0.55) < 0.001)
        {
            return TriggerAssistPreset.Strong;
        }

        return TriggerAssistPreset.Low;
    }

    private ToolStripMenuItem BuildZoneMenu()
    {
        var menu = new ToolStripMenuItem("Select zone");

        menu.DropDownItems.Add(CreateEdgeItem("Select Top Edge...", EdgePosition.Top));
        menu.DropDownItems.Add(CreateEdgeItem("Select Bottom Edge...", EdgePosition.Bottom));
        menu.DropDownItems.Add(CreateEdgeItem("Select Left Edge...", EdgePosition.Left));
        menu.DropDownItems.Add(CreateEdgeItem("Select Right Edge...", EdgePosition.Right));
        menu.DropDownItems.Add(new ToolStripSeparator());

        var hotZoneItem = new ToolStripMenuItem("Select Hot Zone...");
        hotZoneItem.Click += (_, _) => SelectHotZone();
        menu.DropDownItems.Add(hotZoneItem);

        return menu;
    }

    private ToolStripMenuItem CreateEdgeItem(string label, EdgePosition edge)
    {
        var item = new ToolStripMenuItem(label);
        item.Click += (_, _) => SelectEdgeZone(edge);
        return item;
    }

    private void SelectEdgeZone(EdgePosition edge)
    {
        if (_initializing)
        {
            return;
        }

        var rect = EdgeZoneOverlayForm.SelectEdgeRectangle(edge, _runtime.Config.Trigger.Assist);
        if (rect == null)
        {
            return;
        }

        _runtime.SetEdgeZone(edge, rect.Value);
        _runtime.ReinitializeDetection();
        RefreshUiState();
    }

    private void SelectHotZone()
    {
        if (_initializing)
        {
            return;
        }

        var rect = HotZoneOverlayForm.SelectRectangle(_runtime.Config.Trigger.Assist);
        if (rect == null)
        {
            return;
        }

        _runtime.SetHotZone(rect.Value);
        _runtime.ReinitializeDetection();
        RefreshUiState();
    }

    private string BuildTrayText(bool suspendedByAutohide, bool monitorUnavailable)
    {
        var zone = _runtime.Config.Zone.ActiveZone;
        var zoneText = $"{zone.Width}x{zone.Height}";

        if (suspendedByAutohide)
        {
            return "Taskbar Unhide Zoner (Autohide Off)";
        }

        if (monitorUnavailable)
        {
            return "Taskbar Unhide Zoner (Monitoring Unavailable)";
        }

        return _runtime.Config.Enabled
            ? $"Taskbar Unhide Zoner ({zoneText})"
            : "Taskbar Unhide Zoner (Disabled)";
    }

    private static string BuildEnabledItemText(bool suspendedByAutohide, bool monitorUnavailable)
    {
        if (suspendedByAutohide)
        {
            return "Disabled - taskbar autohide is off";
        }

        if (monitorUnavailable)
        {
            return "Disabled - mouse hook monitoring unavailable";
        }

        return "Enable Taskbar Unhide Zoner";
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

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private const uint WmNull = 0x0000;
}
