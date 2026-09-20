# Troubleshooting — CI and Build Failure Modes

Captured from real failures on this branch. Each entry shows the symptom,
the root cause, and the fix that resolved it.

## 1. `NETSDK1004`: project.assets.json not found

Symptom:

```text
error NETSDK1004: Assets file '...\CamfrogMultiID.App\obj\project.assets.json'
not found. Run a NuGet package restore to generate this file.
```

Root cause: the solution file referenced truncated project GUIDs in its
`Build.0` lines (e.g. `{22222222-2222-2222-222222222222}`), so
`dotnet restore CamfrogMultiID.sln` silently skipped those projects
(`BuildProjectInSolution=False`) and only restored a subset.

Fix: regenerate the solution so every project has valid
`ActiveCfg`/`Build.0` mappings:

```powershell
dotnet new sln -n CamfrogMultiID --force
dotnet sln CamfrogMultiID.sln add src/... src/... src/... tests/...
```

Verify with `dotnet restore -v diag | Select-String BuildProjectInSolution`
(all entries must read `True`).

## 2. `MSB4121`: project not selected for building

Symptom:

```text
The project "CamfrogMultiID.Infrastructure" is not selected for building in
solution configuration "Release|Any CPU".
```

Root cause: same truncated-GUID corruption as above. Do not suppress it;
fix the `.sln` (see entry 1).

## 3. `CS0246`: `FactAttribute` not found (xunit v3)

Symptom:

```text
error CS0246: The type or namespace name 'FactAttribute' could not be found
```

Root cause: the test project uses the `xunit.v3` package but never imports
its namespace. `ImplicitUsings` does not include it automatically.

Fix: add to the test `.csproj`:

```xml
<ItemGroup>
  <Using Include="Xunit" />
</ItemGroup>
```

## 4. `CA1707` errors on test method names

Symptom: `error CA1707: Remove the underscores from member name ...`
with `TreatWarningsAsErrors` enabled.

Root cause: analyzer naming rule conflicts with the
`Method_Condition_Expectation` test-naming convention.

Fix: keep production projects strict, exempt only the test project:

```xml
<NoWarn>CA1707</NoWarn>
```

## 5. Marker scan fails on `AGENTS.md`

Symptom: the CI "Scan for unfinished implementation markers" job fails
with hits in `AGENTS.md` (`TODO`, `FIXME`, `NotImplementedException`).

Root cause: `AGENTS.md` documents the defect-pattern search list, so the
scan matches its own definition. Exclude the defining document:

```text
':!AGENTS.md'
```

## 6. `GenerateBundle` file lock on publish

Symptom:

```text
error MSB4018: System.IO.IOException: The process cannot access the file
'...\publish\CamfrogMultiID.exe' because it is being used by another process.
```

Root cause: a previously published copy of the application is still
running and locks its own single-file bundle.

Fix: stop the running instance before republishing:

```powershell
Stop-Process -Name CamfrogMultiID -Force -ErrorAction SilentlyContinue
.\build-release.ps1
```

## 7. Startup `NullReferenceException` from XAML-wired handlers

Symptom: `Application startup failed. Object reference not set to an
instance of an object.` with a stack originating inside
`InitializeComponent` (e.g. through a `SelectionChanged` handler into a
refresh routine that touches not-yet-created controls).

Root cause: controls with default selections (such as a `ComboBox` with
`IsSelected="True"`) fire change events while the window is still being
constructed.

Fix: gate all XAML-wired change handlers (and any refresh entry point
they call) behind an `_initialized` flag set at the end of the
constructor.
