# Camfrog Multi-ID V10 — UI Startup Fix

V9 compiled and published successfully, but a successful build does not guarantee that the WPF window reaches `Show()`.

This revision hardens startup by:

- Removing service construction from static field initializers.
- Initializing `AppPaths`, database, credentials, settings, and sessions inside guarded `OnStartup`.
- Explicitly assigning `MainWindow`, calling `Show()`, `Activate()`, and `Focus()`.
- Adding emergency startup logging at `%LOCALAPPDATA%\CamfrogMultiID\startup-error.log`.
- Adding UI dispatcher, AppDomain, and unobserved-task diagnostics.
- Keeping the existing single-instance protection.
- Adding `run-diagnostic.cmd` to launch the published executable and verify the process remains alive.

## Build

Run:

    build-release.cmd

## Launch

Published executable:

    src\CamfrogMultiID.App\bin\Release\net8.0-windows\win-x64\publish\CamfrogMultiID.exe

Or run:

    run-diagnostic.cmd

## If no window appears

Check:

    %LOCALAPPDATA%\CamfrogMultiID\startup-error.log

and:

    %LOCALAPPDATA%\CamfrogMultiID\logs\app.log

A build success only verifies compilation/publish. These logs identify runtime startup failures.


## V11 build gate

The build script now fails immediately when compilation fails and will not publish or report `BUILD SUCCESS` after a failed build. This prevents stale executables from a previous successful publish being mistaken for the current build output.

`run-diagnostic.cmd` only verifies that the process remains alive; it does not claim that a window is visible.

## V12 culture fix

The startup log identified the root cause of the invisible WPF window: WPF failed while evaluating a binding because `XmlLanguage.GetSpecificCulture()` could not resolve `en-us`. The App project had `<InvariantGlobalization>true</InvariantGlobalization>`, which removes the normal culture data required by WPF.

V12 sets `InvariantGlobalization` to `false`, allowing the Windows runtime to provide normal culture information. No binding or UI behavior is changed.
