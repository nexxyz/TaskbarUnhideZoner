# Technical Notes

## Local development

Build:

```powershell
dotnet build TaskbarUnhideZoner.slnx
```

Tests:

```powershell
dotnet test TaskbarUnhideZoner.slnx
```

Harness (basic runtime crash/regression sequence):

```powershell
dotnet run --project src/TaskbarUnhideZoner/TaskbarUnhideZoner.csproj -- --harness
```

No-move unhide probe:

```powershell
dotnet run --project src/TaskbarUnhideZoner/TaskbarUnhideZoner.csproj -- --test-unhide-loop --interval-ms 5000 --duration-sec 60
```

## Packaging

Publish `win-x64` build:

```powershell
dotnet publish src/TaskbarUnhideZoner/TaskbarUnhideZoner.csproj -c Release -r win-x64 --self-contained false
```

Build installer:

```powershell
iscc installer/TaskbarUnhideZoner.iss
```

Installer output:

- `installer/TaskbarUnhideZoner-Setup.exe`

## Release flow

Use `RELEASE_CHECKLIST.md` for the full step-by-step release process and manual verification matrix.

1. Update installer version in `installer/TaskbarUnhideZoner.iss`.
2. Commit and push to `main`.
3. Create and push tag (for example `v0.9.9`):

```powershell
git tag v0.9.9
git push origin v0.9.9
```

4. GitHub Actions release workflow builds installer and publishes GitHub release with installer asset.

## Pre-release validation (quick)

Run before tagging:

```powershell
dotnet build TaskbarUnhideZoner.slnx
dotnet test TaskbarUnhideZoner.slnx
dotnet run --project src/TaskbarUnhideZoner/TaskbarUnhideZoner.csproj -- --harness
```

Then do a quick manual smoke:

- Open tray menu (left and right click)
- Toggle Enable and Start with Windows
- Verify `Trigger Delay` and `Trigger Assist` presets plus `Custom (from config)` visibility behavior
- Re-select one edge zone and one hot zone
- Confirm app suspends while taskbar autohide is off

## CI and quality report

The CI workflow runs build/tests and generates quality artifacts using `lizard`.

Quality script:

- `tools/quality_report.py`

Local run:

```powershell
python tools/quality_report.py --md _logs/quality-report.md --lizard-csv _logs/lizard.csv
```

Generated artifacts:

- `_logs/quality-report.md`
- `_logs/lizard.csv`
