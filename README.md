# Taskbar Unhide Zoner

Taskbar Unhide Zoner is a lightweight Windows tray utility that reveals an auto-hidden taskbar when your mouse enters a configured sensitivity zone and stays there for a configured dwell time.

## Current status

This repository now includes:

- .NET 8 WinForms tray app (single-instance)
- Left-click and right-click tray context menu
- Enable/disable toggle
- Start-with-Windows toggle (HKCU Run)
- Trigger delay presets (Quick/Default/Long)
- Edge-bar and hot-zone modes
- Draw-hot-zone overlay (`Esc` cancels)
- Fullscreen suspension support
- Detection backends: mouse hook + polling fallback
- Dwell/cooldown state machine
- Basic live-test harness mode (`--harness`)
- Unit tests for zone geometry and state machine logic
- Inno Setup installer script

## Build

```powershell
dotnet build TaskbarUnhideZoner.slnx
```

## Run

```powershell
dotnet run --project src/TaskbarUnhideZoner/TaskbarUnhideZoner.csproj
```

## Harness

```powershell
dotnet run --project src/TaskbarUnhideZoner/TaskbarUnhideZoner.csproj -- --harness
```

Harness logs are written to:

- `%LocalAppData%\TaskbarUnhideZoner\taskbar-unhide-zoner.log`

The harness writes `HARNESS_RESULT:PASS` or `HARNESS_RESULT:FAIL` and exits with `0` on pass, non-zero on failure.

## Tests

```powershell
dotnet test TaskbarUnhideZoner.slnx
```

## Publish (win-x64)

```powershell
dotnet publish src/TaskbarUnhideZoner/TaskbarUnhideZoner.csproj -c Release -r win-x64 --self-contained false
```

## Installer

Inno Setup script:

- `installer/TaskbarUnhideZoner.iss`

Expected publish input path used by script:

- `src/TaskbarUnhideZoner/bin/Release/net8.0-windows/win-x64/publish/`
