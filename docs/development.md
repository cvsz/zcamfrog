# Development

## Requirements

- Windows 10/11 x64 for the WPF application.
- .NET 8 SDK or newer SDK capable of targeting `net8.0-windows`.
- Git.
- PowerShell 7 recommended for release automation.
- Ubuntu 24.04 LTS is the supported host for the optional MinGW/Wine/vcpkg native helper toolchain.

## Build and test on Windows

```powershell
dotnet restore .\CamfrogMultiID.sln
dotnet build .\CamfrogMultiID.sln -c Release --no-restore
dotnet test .\tests\CamfrogMultiID.Tests\CamfrogMultiID.Tests.csproj -c Release --no-restore
```

Run locally:

```powershell
dotnet run --project .\src\CamfrogMultiID.App\CamfrogMultiID.App.csproj -c Release
```

Production publish:

```powershell
.\build-release.ps1
```

## Optional Ubuntu native-toolchain setup

The repository includes:

```bash
bash scripts/bootstrap-ubuntu-mingw-wine-vcpkg.sh
```

This installs MinGW-w64, Wine, vcpkg, and generates `toolchain-x86_64-w64-mingw32.cmake`.

Use it only for native helper components or native Windows tests. A MinGW build cannot replace the supported .NET WPF production build.

Example native CMake configure:

```bash
cmake -S . -B build-mingw -G Ninja \
  -DCMAKE_TOOLCHAIN_FILE="$PWD/toolchain-x86_64-w64-mingw32.cmake" \
  -DCMAKE_BUILD_TYPE=Release
cmake --build build-mingw
```

## Quality gates

Before opening a pull request:

1. Build Release with warnings treated as errors.
2. Run regression tests.
3. Run `dotnet format .\CamfrogMultiID.sln --verify-no-changes --no-restore`.
4. Run repository security marker checks.
5. Confirm no secrets or local profile/database artifacts are tracked.
6. Review process lifecycle changes for PID reuse and fail-closed behavior.

## Testing strategy

Infrastructure tests cover case-insensitive username uniqueness, DPAPI credential round trips, settings normalization/persistence, and deletion safety for running accounts.

Changes to process identity or termination must add regression coverage where practical and must never weaken identity checks just to make tests pass.

## Test limitations

Interactive WPF behavior and the proprietary Camfrog client's actual profile-isolation behavior require Windows/manual validation. CI verifies buildable/publishable artifacts and automated infrastructure tests; it does not prove every Camfrog client version honors every optional argument.
