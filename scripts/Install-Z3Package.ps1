<#
.SYNOPSIS
    Downloads the Microsoft.Z3 NuGet package from its GitHub release into this
    repository's local folder feed, so that `dotnet restore` can find it.

.DESCRIPTION
    The Z3 project stopped publishing Microsoft.Z3 to nuget.org after 4.12.2 (May 2023).
    Every release since then - twenty and counting - ships the .NET package only as a
    GitHub release asset. That matters here rather than being a mere inconvenience:
    4.12.2 contains native binaries for win-x64 and osx-x64 *only*, so on Linux or on
    any arm64 machine the managed assembly loads and then fails at the first solve with
    DllNotFoundException (or, worse, silently binds to an unrelated system libz3 and
    fails later with EntryPointNotFoundException). The version fetched here ships
    linux-x64, linux-arm64, osx-x64, osx-arm64, win-x64 and win-arm64.

    Because the package comes from a GitHub release rather than a signed nuget.org feed,
    the download is checked against a pinned SHA-256 hash and only moved into the feed
    once it matches. An interrupted or substituted download therefore never becomes a
    package that restore will consume.

    The script is idempotent - if the package is already in the feed with the expected
    hash it does nothing - so it is cheap to run on every build.

.PARAMETER Version
    The Microsoft.Z3 version to install. Must match the PackageVersion in
    solutions/Directory.Packages.props.

.PARAMETER ExpectedHash
    The SHA-256 hash of the expected .nupkg, as uppercase hex.

.PARAMETER FeedPath
    The local folder feed to populate. Must match the source registered in NuGet.config.

.EXAMPLE
    ./scripts/Install-Z3Package.ps1

    Populates the feed ready for `dotnet build` or `dotnet restore`. The ZeroFailed
    build runs this automatically; you only need it by hand when building the solution
    directly (for example from an IDE) on a clean clone.

.NOTES
    To move to a new Z3 version, update Version and ExpectedHash here and the matching
    PackageVersion in solutions/Directory.Packages.props together. The hash of a release
    asset can be obtained with:
        (Get-FileHash ./Microsoft.Z3.<version>.nupkg -Algorithm SHA256).Hash
#>
[CmdletBinding()]
param (
    [Parameter()]
    [string] $Version = '5.1.0',

    [Parameter()]
    [string] $ExpectedHash = 'D808E6BD31D96895ECA446ECEB37E90DD24480B0D9B5F513113F3E2FD312ABF2',

    [Parameter()]
    [string] $FeedPath = (Join-Path $PSScriptRoot '..' '_z3-feed')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 4.0

$packageFileName = "Microsoft.Z3.$Version.nupkg"
$feed = New-Item -ItemType Directory -Path $FeedPath -Force
$packagePath = Join-Path $feed.FullName $packageFileName

function Test-ExpectedHash {
    param ([string] $Path)

    if (-not (Test-Path -Path $Path -PathType Leaf)) {
        return $false
    }

    return (Get-FileHash -Path $Path -Algorithm SHA256).Hash -eq $ExpectedHash
}

if (Test-ExpectedHash -Path $packagePath) {
    Write-Host "Microsoft.Z3 $Version already present in $($feed.FullName)"
    return
}

$uri = "https://github.com/Z3Prover/z3/releases/download/z3-$Version/$packageFileName"
$downloadPath = "$packagePath.download"

Write-Host "Downloading $uri"

# The package is ~64MB and Invoke-WebRequest's progress rendering dominates the transfer
# time for a file this size, so suppress it for the duration of the download.
$previousProgressPreference = $ProgressPreference
try {
    $ProgressPreference = 'SilentlyContinue'
    Invoke-WebRequest -Uri $uri -OutFile $downloadPath
}
finally {
    $ProgressPreference = $previousProgressPreference
}

if (-not (Test-ExpectedHash -Path $downloadPath)) {
    $actualHash = (Get-FileHash -Path $downloadPath -Algorithm SHA256).Hash
    Remove-Item -Path $downloadPath -Force
    throw "Hash mismatch for $packageFileName - expected $ExpectedHash but got $actualHash. " +
          "The release asset has changed or the download was corrupted; the package has not been installed."
}

Move-Item -Path $downloadPath -Destination $packagePath -Force

Write-Host "Installed Microsoft.Z3 $Version into $($feed.FullName)"
