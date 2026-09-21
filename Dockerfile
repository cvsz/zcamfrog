# Camfrog Multi-ID Manager is a Windows WPF desktop application.
# It does not run inside a Linux container.
# This Dockerfile is intentionally inert and documents that container deployment is not supported.
# Build and publish must use the Windows .NET 8 WPF pipeline: ./build-release.ps1 (win-x64, self-contained).
FROM mcr.microsoft.com/windows/servercore:ltsc2022 AS not-supported
WORKDIR /app
RUN echo "CamfrogMultiID is Windows-only WPF. Use build-release.ps1 on Windows." && exit 1
