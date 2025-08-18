#!/bin/env pwsh
#Requires -Version 7

[CmdletBinding(DefaultParameterSetName='none')]
param(
    [string] $OutputPath,
    [string] $Version,
    [switch] $SelfContained,
    [switch] $ReadyToRun,
    [switch] $Trimmed,
    [switch] $DebugBuild,
    [switch] $CleanBuild,
    [switch] $BuildNative,
    [Parameter(Mandatory=$true, ParameterSetName='Named')]
    [ValidateSet('windows','linux','macOS')]
    [string] $OperatingSystem,
    [Parameter(Mandatory=$true, ParameterSetName='Named')]
    [ValidateSet('x64','arm64')]
    [string] $Architecture,
    [ValidateSet('none','runtime', 'agnostic')]
    [string] $PackStyle="none"
)

$ErrorActionPreference = 'Stop'

. "$PSScriptRoot/../common/scripts/common.ps1"
$RepoRoot = $RepoRoot.Path.Replace('\', '/')

$npmPackagePath = "$RepoRoot/eng/npm/platform"
$projectDir = "$RepoRoot/core/src/AzureMcp.Cli"
$projectFile = "$projectDir/AzureMcp.Cli.csproj"

if(!$Version) {
    $Version = & "$PSScriptRoot/Get-Version.ps1"
}

if (!$OutputPath) {
    $OutputPath = "$RepoRoot/.work"
}

Push-Location $RepoRoot
try {
    $runtime = $([System.Runtime.InteropServices.RuntimeInformation]::RuntimeIdentifier)
    $parts = $runtime.Split('-')
    if($OperatingSystem) {
        switch($OperatingSystem) {
            'windows' { $os = 'win' }
            'linux' { $os = 'linux' }
            'macos' { $os = 'osx' }
            default { Write-Error "Unsupported operating system: $OperatingSystem"; return }
        }
    } else {
        $os = $parts[0]
    }

    if($Architecture) {
        switch($Architecture) {
            'x64' { $arch = 'x64' }
            'arm64' { $arch = 'arm64' }
            default { Write-Error "Unsupported architecture: $Architecture"; return }
        }
    } else {
        $arch = $parts[1]
    }

    switch($os) {
        'win' { $node_os = 'win32'; $extension = '.exe' }
        'osx' { $node_os = 'darwin'; $extension = '' }
        default { $node_os = $os; $extension = '' }
    }

    $configuration = if ($DebugBuild) { 'Debug' } else { 'Release' }

    if ($PackStyle -eq 'agnostic') {
        $outputDirNuget = "$OutputPath/nuget"
        Write-Host "Building agnostic package in $outputDirNuget" -ForegroundColor Green
        Remove-Item -Path $outputDirNuget -Recurse -Force -ErrorAction SilentlyContinue -ProgressAction SilentlyContinue
        New-Item -Path $outputDirNuget -ItemType Directory -Force | Out-Null
        
        $packCommand = "dotnet pack '$projectFile' --output '$outputDirNuget' /p:Configuration=$configuration /p:Version=$Version /p:BuildNative=true"

        Invoke-LoggedCommand $packCommand -GroupOutput
        # Keep only the .nupkg file with the shortest name (entry-point package)
        $allPkgs = Get-ChildItem -Path $outputDirNuget -Filter "*.nupkg"
        $shortest = $allPkgs | Sort-Object { $_.Name.Length } | Select-Object -First 1
        $allPkgs | Where-Object { $_.FullName -ne $shortest.FullName } | Remove-Item -Force
    }
    else {
        $outputDirNpm = Join-Path $OutputPath "npm"
        $outputDirDist = Join-Path $outputDirNpm "dist"
        $outputDirNuget = Join-Path $OutputPath "nuget"
        Write-Host "Building version $Version, $os-$arch in $outputDirNpm" -ForegroundColor Green

        if ($CleanBuild) {
            # Clean up any previous azmcp build artifacts.
            Invoke-LoggedCommand "dotnet clean '$projectFile' --configuration $configuration" -GroupOutput
        }

        # Clear and recreate the package output directory
        Remove-Item -Path $outputDirNpm -Recurse -Force -ErrorAction SilentlyContinue -ProgressAction SilentlyContinue
        Remove-Item -Path $outputDirNuget -Recurse -Force -ErrorAction SilentlyContinue -ProgressAction SilentlyContinue
        New-Item -Path $outputDirDist -ItemType Directory -Force | Out-Null
        New-Item -Path $outputDirNuget -ItemType Directory -Force | Out-Null

        # Copy the platform package files to the output directory
        Copy-Item -Path "$npmPackagePath/*" -Recurse -Destination $outputDirNpm -Force

        $publishCommand = "dotnet publish '$projectFile' --runtime '$os-$arch' --output '$outputDirDist' /p:Version=$Version /p:Configuration=$configuration"
        $packCommand = "dotnet pack '$projectFile' --runtime '$os-$arch' --output '$outputDirNuget' /p:Version=$Version /p:Configuration=$configuration /p:BuildNative=true"

        if($SelfContained) {
            $publishCommand += " --self-contained"
        }

        if($ReadyToRun) {
            $publishCommand += " /p:PublishReadyToRun=true"
        }

        if($Trimmed) {
            $publishCommand += " /p:PublishTrimmed=true"
        }

        if($BuildNative) {
            $publishCommand += " /p:BuildNative=true"
        }

        Invoke-LoggedCommand $publishCommand -GroupOutput

        if ($PackStyle -eq 'runtime' -and -not ($os -eq 'linux' -and $arch -eq 'arm64')) {
            Invoke-LoggedCommand $packCommand -GroupOutput
        }

        $package = Get-Content "$outputDirNpm/package.json" -Raw
        $package = $package.Replace('{os}', $node_os)
        $package = $package.Replace('{cpu}', $arch)
        $package = $package.Replace('{version}', $Version)
        $package = $package.Replace('{executable}', "azmcp$extension")

        # confirm all the placeholders are replaced
        if ($package -match '\{\w+\}') {
            Write-Error "Failed to replace $($Matches[0]) in package.json"
            return
        }

        $package
        | Out-File -FilePath "$outputDirNpm/package.json" -Encoding utf8

        Write-Host "Updated package.json in $outputDirNpm" -ForegroundColor Yellow

        Write-Host "`nBuild completed successfully!" -ForegroundColor Green
    }
}
finally {
    Pop-Location
}
