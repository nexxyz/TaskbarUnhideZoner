# Taskbar Unhide Zoner

> Under construction: usable today, still being refined.

Taskbar Unhide Zoner is a lightweight Windows tray utility that helps you keep taskbar auto-hide on (for cleaner desktop and reduced OLED burn-in risk), while making taskbar reveal intentional through a configurable sensitivity zone.

## What it does

- Runs in the Windows notification area (tray), no main window.
- Left-click and right-click tray interactions use the same context-menu behavior.
- Reveals the taskbar when your cursor enters a configured zone and stays there for a dwell time.
- Supports edge zones (top/bottom/left/right) and a custom hot-zone rectangle.
- Selecting any zone from the menu opens an overlay to define that zone (`Esc` cancels).
- At runtime, both edge and hot-zone are detected as a single persisted rectangle (same detection path, different capture UX).
- Supports quick toggles from tray menu (enable/disable, startup, delay preset, trigger assist, zone selection).
- Uses no cursor hijacking in normal app behavior (no pointer nudging).

## Requirements

- Windows 10 or Windows 11
- Taskbar auto-hide enabled (required for runtime behavior)
- .NET 8 Desktop Runtime (installer includes a framework-dependent app)

## Install

1. Download the latest installer from Releases:
   - [Latest release](https://github.com/nexxyz/TaskbarUnhideZoner/releases/latest)
2. Run `TaskbarUnhideZoner-Setup.exe` and launch the app when setup completes.

## How to use

1. Launch app and find its tray icon.
2. Left-click or right-click the icon to open the menu.
3. Configure:
   - `Trigger Delay` (`Quick`, `Default`, `Long`; shows `Custom (from config)` for non-preset values)
   - `Trigger Assist` (`Off`, `Low`, `Medium`, `Strong`; shows `Custom (from config)` for non-preset values)
   - `Select zone`:
      - `Top/Bottom/Left/Right` opens an overlay and lets you click thickness for a full-width/full-height edge zone
      - `Hot Zone` opens a freeform rectangle draw overlay
4. Keep `Enable Taskbar Unhide Zoner` checked.

### Notes about auto-hide

- If Windows taskbar auto-hide is off, the app suspends monitoring.
- In that state, the enable item is grayed out and shows a message indicating auto-hide is off.
- Re-enable auto-hide in Windows settings, then reopen tray menu or wait for periodic refresh.

### Already running behavior

- If you launch the app again while it is already running, the existing tray instance shows a notification.

## Configuration file

Config path:

- `%LocalAppData%\TaskbarUnhideZoner\config.json`

You can edit this file directly while the app is not running. Main fields:

- `Enabled`
- `StartWithWindows`
- `TriggerDelayMs`
- `DelayPresets` (`QuickMs`, `DefaultMs`, `LongMs`)
- `Zone` (`Mode`, `ActiveZone`)
- `Trigger` (`CooldownMs`, `Assist`)
- `Trigger.Assist` (`Enabled`, `MinDelayPercent`, `CurveExponent`)
- `Fullscreen` (`SuspendWhenFullscreenAppActive`)
- `AutohideStatePollSeconds`

## Troubleshooting

- No unhide behavior:
  - Verify taskbar auto-hide is enabled in Windows settings.
  - Confirm app tray menu shows enabled state.
  - Try `Quick` delay preset.
- Hot zone seems wrong:
  - Re-select `Select zone -> Select Hot Zone...` to redraw.
  - Check `Zone.ActiveZone` values in config.
- Edge zone seems too thin/thick:
  - Re-select the edge (`Select zone -> Select Top/Bottom/Left/Right Edge...`) and click a new thickness in the overlay.
- Trigger feels too eager or too strict:
  - Try a different `Trigger Assist` preset.
  - `Low` keeps the hot area narrow near edge/center; `Strong` ramps faster across the zone.
- App seems inactive in fullscreen apps:
  - This is expected when fullscreen suspension is enabled.

Logs:

- `%LocalAppData%\TaskbarUnhideZoner\taskbar-unhide-zoner.log`

## Technical notes

- Build/release and developer workflows are documented in `docs/TECHNICAL.md`.
