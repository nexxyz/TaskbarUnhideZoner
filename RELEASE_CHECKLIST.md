- Build + tests
  - `dotnet build TaskbarUnhideZoner.slnx -c Release`
  - `dotnet test TaskbarUnhideZoner.slnx -c Release`

- Harness + quick automated checks
  - `dotnet run -c Release --project src/TaskbarUnhideZoner/TaskbarUnhideZoner.csproj -- --harness`
  - Optional: `dotnet run -c Release --project src/TaskbarUnhideZoner/TaskbarUnhideZoner.csproj -- --test-unhide-loop --interval-ms 5000 --duration-sec 60`

- Manual smoke run
  - Run `src/TaskbarUnhideZoner/bin/Release/net8.0-windows/TaskbarUnhideZoner.exe`
  - Verify tray icon appears and menu opens on left and right click
  - Verify Enable toggle works and state updates immediately
  - Verify Start with Windows toggle writes/removes startup entry
  - Verify `Trigger Delay` and `Trigger Assist` presets
  - Verify `Custom (from config)` row appears only for custom values and hides after selecting a preset
  - Verify zone capture overlays work (`Select Top/Bottom/Left/Right Edge...`, `Select Hot Zone...`)
  - Verify fullscreen suspension and autohide-off suspension behavior

- Packaging
  - Bump version in `installer/TaskbarUnhideZoner.iss`
  - `dotnet publish src/TaskbarUnhideZoner/TaskbarUnhideZoner.csproj -c Release -r win-x64 --self-contained false`
  - Build installer: `"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer\TaskbarUnhideZoner.iss`

- Release
  - Commit release changes to `main`
  - Create tag (example `vX.Y.Z`) and push tag
  - Confirm GitHub Actions release workflow uploads `TaskbarUnhideZoner-Setup.exe`

- Post-release verification
  - Download installer from release page and run a clean install smoke test
  - Confirm app starts and tray menu is responsive on first launch
