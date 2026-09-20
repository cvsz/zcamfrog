SHELL := /bin/sh

.PHONY: help restore build test publish format lint security ci clean

help:
	@printf '%s\n' 'Targets: restore build test publish format lint security ci clean'

restore:
	dotnet restore CamfrogMultiID.sln

build:
	dotnet build CamfrogMultiID.sln -c Release --no-restore

test:
	dotnet test tests/CamfrogMultiID.Tests/CamfrogMultiID.Tests.csproj -c Release --no-restore --verbosity normal

publish:
	pwsh -NoProfile -File ./build-release.ps1

format:
	dotnet format CamfrogMultiID.sln --verify-no-changes --no-restore

lint:
	dotnet build CamfrogMultiID.sln -c Release --no-restore -warnaserror

security:
	@set -e; if git grep -n -E 'TODO|FIXME|NotImplementedException|Replace with project|ztemplate' -- ':!CHANGELOG.md'; then exit 1; fi
	@echo 'Static security marker scan passed.'

ci: restore build test lint security

clean:
	dotnet clean CamfrogMultiID.sln
