# Taskbar Unhide Zoner - Design Documentation (v1.2)

## Repository scope and locations

- Target local repository: `F:\dev\TaskbarUnhideZoner`
- Target remote repository: `https://github.com/nexxyz/TaskbarUnhideZoner`
- Reference repository (inspiration only): `https://github.com/nexxyz/WinPanX.2`
- Reference area reviewed: `https://github.com/nexxyz/WinPanX.2/tree/main/src/WinPanX2/Tray`
- Important boundary: this project has no runtime/code dependency on WinPanX.2; only interaction and architecture learnings are reused.

## Product goal

Create a lightweight Windows tray utility that reveals an auto-hidden taskbar when the cursor enters a configured sensitivity zone and remains there for a configured dwell time.

Primary use case: keep taskbar auto-hide enabled to reduce OLED burn-in risk while still making unhide behavior intentional and comfortable.

## UX and behavior

- Tray-only app (`NotifyIcon`), no main window.
- Left click and right click on tray icon open the same context menu.
- Context menu includes:
  - Enable/Disable monitoring
  - Start with Windows toggle
  - Trigger delay presets: Quick / Default / Long
  - Zone mode selection:
    - Edge bar (top, bottom, left, right) with overlay-based thickness selection
    - Hot zone rectangle via drag overlay
  - Reveal method selection:
    - Explorer message (experimental)
    - ABM state toggle (reliable)
    - Re-detect best method
- Open config file
- Exit
- Settings are persisted and restored on next launch.
- Trigger pipeline is suspended while a fullscreen foreground app is active (default behavior).
- If taskbar autohide is off, runtime monitoring is suspended and the main enable item is shown as disabled.

## Configuration model

Use a human-editable JSON config file with safe defaults. The menu changes update this file; users can also edit exact values manually.

Expected configurable fields:

- `enabled`
- `startWithWindows`
- `triggerDelayMs`
- Preset mapping (`quickMs`, `defaultMs`, `longMs`)
- Zone mode (`EdgeBar` or `HotZone`)
- Edge settings (`edge`, `edgeThicknessPx`, persisted `edgeZone` rectangle)
- Hot zone rectangle (`x`, `y`, `width`, `height`) in virtual-screen coordinates
- Detection backend mode (`MouseHook` default, optional explicit `Polling` for diagnostics)
- Poll interval (only used if explicit polling backend is selected)
- Trigger behavior settings (`cooldownMs`, strategy)
- Reveal method setting (`revealMethod`)
- Fullscreen behavior setting (`suspendWhenFullscreenAppActive`)
- Autohide state check interval (`autohideStatePollSeconds`, default 5)

## Detection and trigger strategy

Do not hard-commit to one detection mechanism up front. Implement a pluggable monitor abstraction and choose defaults after practical validation.

Primary zone-entry backend:

- Event-driven low-level mouse hook (`WH_MOUSE_LL`) with asynchronous/coalesced processing and low-frequency idle sampling for stable dwell timing

Optional diagnostic backend:

- Cursor position polling (`GetCursorPos` + timer)

Shared logic:

- Zone hit-testing
- Dwell timing
- One trigger per entry cycle
- Cooldown handling

Taskbar reveal strategy:

1. Strict no-move policy: cursor movement/synthetic pointer nudging is not allowed.
2. Detect-once method selection on first startup:
   - If Explorer message probe works, select Explorer message mode.
   - Otherwise select ABM state-toggle mode.
3. Allow manual override and manual re-detection from tray menu.

## No-move and autohide state decisions

- No cursor movement is allowed for taskbar reveal. Any cursor-nudge/synthetic mouse movement approach is out of scope.
- Primary reveal mechanism is selected once at startup and stored in config (`revealMethod`).
- Candidate methods:
  - Explorer message mode (undocumented message to `Shell_TrayWnd`, no state mutation)
  - ABM state-toggle mode via `SHAppBarMessage` (`ABM_GETSTATE` / `ABM_SETSTATE`)
- Zone behavior:
  - In Explorer mode, on dwell-complete zone enter send reveal message; on leave no explicit restore action.
  - In ABM mode, on dwell-complete zone enter disable autohide; on leave restore prior autohide state.
- If taskbar autohide is already off:
  - Suspend zone monitoring entirely (no mouse hook/polling monitor running).
  - Do not run trigger logic.
- Method detection policy:
  - Run automatically on first startup when `revealMethod` is unset.
  - Use binary outcome (works / does not work), no probabilistic scoring.
  - If detection cannot be run safely (e.g., autohide currently off), default to ABM mode.
- Tray UX when autohide is off:
  - Gray out the main enable entry.
  - Show text similar to: `Disabled - taskbar autohide is off`.
  - Keep `Start with Windows`, `Open Config`, and `Exit` available.
- Autohide state detection:
  - Read with `ABM_GETSTATE` on startup.
  - Re-check when tray menu opens.
  - Background polling interval is low-frequency: every 5 seconds (max cadence).
- Conflict avoidance with our own toggles:
  - Track app-initiated state changes separately from external/manual changes.
  - If external/manual change is detected, adopt it as the new baseline and avoid thrashing.
- Monitoring backend policy:
  - Do not silently fallback to another backend at runtime.
  - If mouse hook initialization fails, surface a clear disabled/unavailable status in the tray UI.
- Safety:
  - On exit, restore state only if this app changed it.
  - Do not leave taskbar state unintentionally altered after normal shutdown.

## Zone state machine

The runtime decision flow is modeled as a small explicit state machine to keep behavior deterministic and testable.

States:

- `OutsideZone`
- `InsideZoneCounting`
- `TriggeredCooldown`

Transitions:

- `OutsideZone` -> `InsideZoneCounting`
  - Condition: cursor enters active zone.
  - Action: record `enteredAt` timestamp.
- `InsideZoneCounting` -> `OutsideZone`
  - Condition: cursor leaves active zone before dwell threshold.
  - Action: clear `enteredAt` and pending trigger state.
- `InsideZoneCounting` -> `TriggeredCooldown`
  - Condition: `now - enteredAt >= triggerDelayMs`.
  - Action: apply no-move taskbar state transition (show), set `cooldownUntil`.
- `TriggeredCooldown` -> `OutsideZone`
  - Condition: cursor leaves active zone.
  - Action: clear cooldown marker after timeout and reset cycle.
- `TriggeredCooldown` (self)
  - Condition: cursor remains in zone and/or cooldown not elapsed.
  - Action: do not retrigger; retrigger requires leave-and-reenter cycle.

Boundary-jitter handling:

- Tiny cursor oscillations near a border must not cause repeated triggers.
- Cooldown plus leave-and-reenter requirement prevents rapid retrigger loops.

Timing notes:

- Dwell timing uses monotonic elapsed time.
- Trigger decision happens immediately when dwell threshold is reached, subject to backend event cadence.

Fullscreen handling:

- Default policy: suspend detection/trigger execution while a fullscreen foreground app is active.
- When fullscreen state ends, resume normal zone monitoring.
- Goal: avoid interference and pointless trigger attempts during immersive fullscreen usage.

## Quality goals

- Keep CPU and RAM usage very low.
- Keep behavior unobtrusive and stable over long runtime sessions.
- Keep code clean and maintainable:
  - clear module boundaries
  - small, focused classes
  - minimal shared mutable state
  - explicit error-handling on OS interop boundaries

## Test strategy

Unit tests where they add value (pure logic and deterministic behavior), especially:

- Zone hit-testing math (edge bars + arbitrary rectangles)
- Dwell timer state machine (enter/leave/re-enter/cooldown)
- Config load/save/validation and defaults
- Preset mapping behavior

Avoid over-mocking native APIs for unit tests; isolate those APIs behind interfaces and test logic around them.

## Local live-testing harness

Provide a basic local live-test setup for Windows to quickly detect crashes and runtime regressions during interaction.

Scope of harness:

- A CLI flag or dedicated harness mode that:
  - boots core services without full installer flow
  - executes a scripted sequence of common actions (enable, disable, preset switch, zone switch, hot-zone assign/cancel, trigger simulation where possible)
  - logs pass/fail checkpoints and unhandled exceptions
- Keep it simple and fast so it can be run repeatedly during development.

Reference note: this follows the same spirit as the lightweight runtime harness pattern used in WinPanX.2, adapted for this app's behaviors.

## Validation matrix

The following scenarios define the baseline verification matrix. Each item should be marked as `Pass` or `Fail` during local validation.

- Edge zone top/bottom/left/right triggers correctly with each delay preset (`Manual` + `Harness` where feasible).
- Hot-zone draw flow commits rectangle on mouse release and cancels on `Esc` (`Manual`).
- Enable/disable from tray applies immediately and persists across restart (`Manual` + `Harness`).
- Start-with-Windows toggle writes/removes HKCU Run value correctly (`Manual` + `Harness`).
- Switching zone mode (EdgeBar <-> HotZone) updates runtime behavior without app restart (`Manual` + `Harness`).
- Taskbar located on non-primary monitor still reveals from configured zone (`Manual`).
- Mixed monitor layout with negative virtual coordinates still matches configured hot zone (`Manual`).
- Explorer restart (`explorer.exe`) does not leave app in a crash loop; tray behavior recovers or exits gracefully (`Manual`).
- If mouse hook initialization fails, app surfaces monitoring-unavailable state without silent backend switching (`Manual`).
- Fullscreen-app interaction does not crash the process during cursor movement and zone entry (`Manual`).
- Fullscreen foreground app causes trigger suspension, and normal triggering resumes after leaving fullscreen (`Manual` + `Harness` where feasible).
- If taskbar autohide is disabled in Windows settings, monitoring suspends and tray status reflects disabled-by-autohide-off state (`Manual` + `Harness`).
- If taskbar autohide is enabled while app is running, monitoring resumes within one autohide poll interval (`Manual`).

Harness execution contract:

- Harness mode must produce explicit step logs and a clear final status line.
- Harness exits with code `0` on success; non-zero on failure.
- Unhandled exceptions are logged and treated as failure.

## Architecture outline

- `Program`
  - single-instance mutex
  - app initialization and startup guards
- `TrayApp`
  - tray icon lifecycle
  - context menu wiring
  - immediate runtime apply for menu actions
- `Config`
  - path resolution
  - load/save/validate
- `Startup`
  - HKCU Run key integration
- `Detection`
  - `IZoneMonitor` abstraction (mouse-hook primary, polling optional diagnostic mode)
  - zone evaluator and dwell engine
- `Trigger`
  - no-move reveal methods, detect-once selection, and ABM restore logic
- `UI`
  - temporary hot-zone selection overlay
- `Interop`
  - Win32 wrappers isolated from business logic

## Installer plan (Inno Setup)

- Per-user install (no admin requirement by default).
- Install app binaries and defaults.
- Add Start Menu entries and uninstaller.
- Optional launch-at-startup choice aligned with app startup setting.

## Milestones

1. Scaffold app shell (single instance, tray icon, context menu, config persistence, startup toggle).
2. Implement zone model, dwell logic, and mouse-hook backend.
3. Add optional polling backend for diagnostics only (no silent runtime fallback).
4. Implement strict no-move reveal methods and detect-once selection.
5. Implement hot-zone draw overlay (`Esc` cancel).
6. Add unit tests for logic modules.
7. Add local live-test harness and logging checks.
8. Add Inno Setup installer script.

## Acceptance criteria

- App runs as tray-only and remains stable over long sessions.
- Left/right click behavior is consistent and reliable.
- Enable/disable works immediately and persists.
- If taskbar autohide is off, app suspends monitoring and clearly indicates disabled state in tray menu.
- Delay presets and exact config values both function.
- Edge and hot-zone modes trigger as expected.
- Hot-zone drawing supports cancel via `Esc`.
- Startup toggle persists via HKCU Run key.
- Unit tests cover core deterministic logic.
- Live-test harness can quickly catch crashes for common user actions.
- Installer installs/uninstalls cleanly.

## Non-functional targets

- Idle CPU target: effectively near 0% in normal desktop idle conditions, practical threshold `< 0.2%` average over 60 seconds.
- Additional trigger latency target: `< 100 ms` beyond configured dwell threshold under normal load.
- Startup target: tray icon visible and menu responsive within `< 1.5 s` on warm start.
- Memory target: steady-state working set `< 50 MB`.
- Stability target: no unhandled exceptions during common tray/menu/config workflows in validation matrix.
