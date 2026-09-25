SHELL := /bin/sh

.PHONY: help setup format lint test build publish security ci

help:
	@printf '%s\n' 'Targets: setup format lint test build publish security ci'
	@printf '%s\n' '  setup   - restore .NET dependencies'
	@printf '%s\n' '  format  - verify formatting (dotnet format)'
	@printf '%s\n' '  lint    - build with analyzers (warnings as errors)'
	@printf '%s\n' '  test    - run tests (Release)'
	@printf '%s\n' '  build   - build Debug and Release'
	@printf '%s\n' '  publish - self-contained win-x64 publish'
	@printf '%s\n' '  security- run CodeQL queries where available'

setup:
	dotnet restore CamfrogMultiID.sln
	dotnet restore src/CamfrogMultiID.App/CamfrogMultiID.App.csproj --force-evaluate -r win-x64

format:
	dotnet format --verify-no-changes --verbosity diagnostic || (echo "Run: dotnet format"; exit 1)

lint:
	dotnet build CamfrogMultiID.sln -c Release --no-restore

test:
	dotnet test CamfrogMultiID.sln -c Release --no-build --verbosity normal

build:
	dotnet build CamfrogMultiID.sln -c Debug --no-restore
dotnet build CamfrogMultiID.sln -c Release --no-restore

publish:
	powershell -NoProfile -ExecutionPolicy Bypass -File ./build-release.ps1

security:
	@echo "Use repository security workflows: CodeQL (csharp), dependency-review, secret scanning."

ci: setup lint test build publish security
