<#
    Downloads the four NuGet packages into .\packages using the standard packages.config
    layout, so Visual Studio's own restore and this script agree on the paths.

    This exists because nuget.exe and the dotnet CLI are not installed on the target machine.
    A .nupkg is an ordinary zip, so Invoke-WebRequest plus Expand-Archive is enough.
#>
[CmdletBinding()]
param(
    [string] $PackagesDir
)

$ErrorActionPreference = 'Stop'

if (-not $PackagesDir) {
    $PackagesDir = Join-Path (Split-Path -Parent $PSScriptRoot) 'packages'
}

$packages = @(
    # The NuGet id is "TaskScheduler"; it is the package that contains Microsoft.Win32.TaskScheduler.dll.
    @{ Id = 'TaskScheduler';                 Version = '2.12.2';  Probe = 'lib\net48\Microsoft.Win32.TaskScheduler.dll' },
    @{ Id = 'Newtonsoft.Json';               Version = '13.0.4';  Probe = 'lib\net45\Newtonsoft.Json.dll' },
    @{ Id = 'NUnit';                         Version = '3.14.0';  Probe = 'lib\net45\nunit.framework.dll' },
    @{ Id = 'NUnitLite';                     Version = '3.14.0';  Probe = 'lib\net45\nunitlite.dll' }
)

# nuget.org requires TLS 1.2; Windows PowerShell 5.1 does not always default to it.
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

New-Item -ItemType Directory -Force -Path $PackagesDir | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem

$missing = 0

foreach ($package in $packages) {
    $target = Join-Path $PackagesDir ("{0}.{1}" -f $package.Id, $package.Version)

    if (Test-Path (Join-Path $target $package.Probe)) {
        Write-Host ("  present  {0} {1}" -f $package.Id, $package.Version)
        continue
    }

    $lower = $package.Id.ToLowerInvariant()
    $url = "https://api.nuget.org/v3-flatcontainer/$lower/$($package.Version)/$lower.$($package.Version).nupkg"
    $temp = Join-Path $env:TEMP ("{0}.{1}.nupkg" -f $lower, $package.Version)

    Write-Host ("  fetching {0} {1}" -f $package.Id, $package.Version)
    try {
        Invoke-WebRequest -UseBasicParsing -Uri $url -OutFile $temp -TimeoutSec 120

        if (Test-Path $target) { Remove-Item $target -Recurse -Force }
        New-Item -ItemType Directory -Force -Path $target | Out-Null
        [IO.Compression.ZipFile]::ExtractToDirectory($temp, $target)
        Remove-Item $temp -Force -ErrorAction SilentlyContinue

        if (-not (Test-Path (Join-Path $target $package.Probe))) {
            Write-Warning ("{0} {1} downloaded but {2} is not in it." -f $package.Id, $package.Version, $package.Probe)
            $missing++
        }
    }
    catch {
        Write-Warning ("Could not fetch {0} {1}: {2}" -f $package.Id, $package.Version, $_.Exception.Message)
        $missing++
    }
}

if ($missing -gt 0) {
    Write-Host ""
    Write-Host "$missing package(s) are missing. With no internet access, copy the packages" -ForegroundColor Yellow
    Write-Host "folder from a machine that has them, or restore the solution in Visual Studio." -ForegroundColor Yellow
    exit 1
}

exit 0
