# Taskbar Unhide Zoner

> Under construction: this tool is already usable, but behavior and UX are still being refined.

Taskbar Unhide Zoner is a lightweight Windows tray utility that helps you keep taskbar auto-hide on (for cleaner desktop and reduced OLED burn-in risk), while making taskbar reveal intentional through a configurable sensitivity zone.

## What it does

- Runs in the Windows notification area (tray), no main window.
- Reveals the taskbar when your cursor enters a configured zone and stays there for a dwell time.
- Supports edge zones (top/bottom/left/right) and a custom hot-zone rectangle.
- Lets you draw the hot zone directly on screen (`Esc` cancels).
- Supports quick toggles from tray menu (enable/disable, startup, delay preset, zone mode).
- Uses no cursor hijacking in normal app behavior (no pointer nudging).

## Requirements

- Windows 10 or Windows 11
- Taskbar auto-hide enabled (required for runtime behavior)
- .NET 8 Desktop Runtime (if using framework-dependent build)

## Install and run

### Option A: Installer (recommended)

1. Build publish output:

```powershell
dotnet publish src/TaskbarUnhideZoner/TaskbarUnhideZoner.csproj -c Release -r win-x64 --self-contained false
```

2. Build installer with Inno Setup:

```powershell
iscc installer/TaskbarUnhideZoner.iss
```

3. Run generated installer:

- `installer/TaskbarUnhideZoner-Setup.exe`

### Option B: Run from source

```powershell
dotnet run --project src/TaskbarUnhideZoner/TaskbarUnhideZoner.csproj
```

## How to use

1. Launch app and find its tray icon.
2. Left-click or right-click the icon to open the menu.
3. Configure:
   - `Trigger Delay` (`Quick`, `Default`, `Long`)
   - `Zone` (`Edge Bar` or `Hot Zone`)
   - `Draw Hot Zone...` to select a rectangle on screen
4. Keep `Enable Taskbar Unhide Zoner` checked.

### Notes about auto-hide

- If Windows taskbar auto-hide is off, the app suspends monitoring.
- In that state, the enable item is grayed out and shows a message indicating auto-hide is off.
- Re-enable auto-hide in Windows settings, then reopen tray menu or wait for periodic refresh.

## Configuration file

Config path:

- `%LocalAppData%\TaskbarUnhideZoner\config.json`

You can edit this file directly while the app is not running. Main fields:

- `Enabled`
- `StartWithWindows`
- `TriggerDelayMs`
- `DelayPresets` (`QuickMs`, `DefaultMs`, `LongMs`)
- `Zone` (`Mode`, `Edge`, `EdgeThicknessPx`, `HotZone`)
- `Detection` (`Backend`, `PollIntervalMs`)
- `Trigger` (`CooldownMs`, `Strategy`)
- `Fullscreen` (`SuspendWhenFullscreenAppActive`)
- `AutohideStatePollSeconds`

## Troubleshooting

- No unhide behavior:
  - Verify taskbar auto-hide is enabled in Windows settings.
  - Confirm app tray menu shows enabled state.
  - Try `Quick` delay preset.
- Hot zone seems wrong:
  - Re-draw using `Draw Hot Zone...`.
  - Check `HotZone` coordinates in config.
- App seems inactive in fullscreen apps:
  - This is expected when fullscreen suspension is enabled.

Logs:

- `%LocalAppData%\TaskbarUnhideZoner\taskbar-unhide-zoner.log`

## Developer commands

Build:

```powershell
dotnet build TaskbarUnhideZoner.slnx
```

Tests:

```powershell
dotnet test TaskbarUnhideZoner.slnx
```

Harness (basic crash/regression sequence):

```powershell
dotnet run --project src/TaskbarUnhideZoner/TaskbarUnhideZoner.csproj -- --harness
```

No-move unhide loop probe:

```powershell
dotnet run --project src/TaskbarUnhideZoner/TaskbarUnhideZoner.csproj -- --test-unhide-loop --interval-ms 5000 --duration-sec 60
```
