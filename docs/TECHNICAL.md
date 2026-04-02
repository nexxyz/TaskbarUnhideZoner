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

1. Update installer version in `installer/TaskbarUnhideZoner.iss`.
2. Commit and push to `main`.
3. Create and push tag (for example `v0.9.3`):

```powershell
git tag v0.9.3
git push origin v0.9.3
```

4. GitHub Actions release workflow builds installer and publishes GitHub release with installer asset.

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
