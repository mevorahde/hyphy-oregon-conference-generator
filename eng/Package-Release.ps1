[CmdletBinding()]
param(
    [Parameter()]
    [string] $OutputDirectory = "artifacts/release"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
$project = Join-Path $repositoryRoot "src/HyphyOregon.ConferenceGenerator.Cli/HyphyOregon.ConferenceGenerator.Cli.csproj"
$manifestPath = Join-Path $repositoryRoot "eng/release-manifest.json"
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$resolvedOutput = [System.IO.Path]::GetFullPath(
    $(if ([System.IO.Path]::IsPathRooted($OutputDirectory)) {
        $OutputDirectory
    } else {
        Join-Path $repositoryRoot $OutputDirectory
    }))

if ($resolvedOutput -eq $repositoryRoot -or $resolvedOutput -eq [System.IO.Path]::GetPathRoot($resolvedOutput)) {
    throw "The release output directory must not be a repository or filesystem root."
}

if (Test-Path -LiteralPath $resolvedOutput) {
    if (@(Get-ChildItem -LiteralPath $resolvedOutput -Force).Count -ne 0) {
        throw "The release output directory must be empty."
    }
} else {
    New-Item -ItemType Directory -Path $resolvedOutput | Out-Null
}

$frameworkDirectory = Join-Path $resolvedOutput $manifest.frameworkDependent.rootDirectory
$windowsDirectory = Join-Path $resolvedOutput $manifest.windowsX64.rootDirectory
$releaseOutputName = [System.IO.Path]::GetFileNameWithoutExtension(
    $manifest.frameworkDependent.entryPoint)
$obsoleteOutputNames = @(
    "HyphyOregon.ConferenceGenerator.Cli",
    "HyphyOregonConferenceGenerator"
)

dotnet restore $project --runtime win-x64
if ($LASTEXITCODE -ne 0) {
    throw "Runtime-specific restore failed."
}

dotnet clean $project --configuration Release
if ($LASTEXITCODE -ne 0) {
    throw "Framework-dependent clean failed."
}

dotnet clean $project --configuration Release --runtime win-x64
if ($LASTEXITCODE -ne 0) {
    throw "Windows x64 clean failed."
}

dotnet publish $project `
    --configuration Release `
    --no-restore `
    --self-contained false `
    --output $frameworkDirectory `
    -p:UseAppHost=false `
    -p:DebugSymbols=false `
    -p:DebugType=None
if ($LASTEXITCODE -ne 0) {
    throw "Framework-dependent publish failed."
}

dotnet publish $project `
    --configuration Release `
    --no-restore `
    --runtime win-x64 `
    --self-contained true `
    --output $windowsDirectory `
    -p:DebugSymbols=false `
    -p:DebugType=None
if ($LASTEXITCODE -ne 0) {
    throw "Windows x64 publish failed."
}

$frameworkRequiredFiles = @(
    $manifest.frameworkDependent.entryPoint,
    "$releaseOutputName.deps.json",
    "$releaseOutputName.runtimeconfig.json"
)
foreach ($requiredFile in $frameworkRequiredFiles) {
    if (-not (Test-Path -LiteralPath (Join-Path $frameworkDirectory $requiredFile))) {
        throw "A required framework-dependent output file is missing: $requiredFile"
    }
}

if (-not (Test-Path -LiteralPath (
    Join-Path $windowsDirectory $manifest.windowsX64.entryPoint))) {
    throw "The required Windows output file is missing: $($manifest.windowsX64.entryPoint)"
}

foreach ($directory in @($frameworkDirectory, $windowsDirectory)) {
    Copy-Item -LiteralPath (Join-Path $repositoryRoot "LICENSE") -Destination $directory
    Copy-Item -LiteralPath (Join-Path $repositoryRoot "README.md") -Destination $directory

    $forbidden = @(Get-ChildItem -LiteralPath $directory -Recurse -File | Where-Object {
        ($_.Extension -in @(".pdb", ".nupkg", ".snupkg")) -or
        ($_.FullName -match "[\\/](bin|obj|TestResults|coverage|\.git|\.vs|\.idea)[\\/]") -or
        ($_.Name -match "(?i)(legacy|favicon)")
    })
    if ($forbidden.Count -ne 0) {
        throw "A forbidden file was found in a publish directory."
    }

    $obsolete = @(Get-ChildItem -LiteralPath $directory -Recurse -File | Where-Object {
        $fileName = $_.Name
        $obsoleteOutputNames | Where-Object {
            $fileName -eq $_ -or
            $fileName.StartsWith("$_.", [System.StringComparison]::OrdinalIgnoreCase)
        }
    })
    if ($obsolete.Count -ne 0) {
        throw "An obsolete output name was found in a publish directory."
    }
}

$frameworkArchive = Join-Path $resolvedOutput $manifest.frameworkDependent.archive
$windowsArchive = Join-Path $resolvedOutput $manifest.windowsX64.archive
Compress-Archive -LiteralPath $frameworkDirectory -DestinationPath $frameworkArchive -CompressionLevel Optimal
Compress-Archive -LiteralPath $windowsDirectory -DestinationPath $windowsArchive -CompressionLevel Optimal

foreach ($archive in @($frameworkArchive, $windowsArchive)) {
    $hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
    $checksumPath = "$archive.sha256"
    Set-Content -LiteralPath $checksumPath -Value "$hash  $([System.IO.Path]::GetFileName($archive))" -Encoding utf8NoBOM
}

Write-Output $frameworkArchive
Write-Output "$frameworkArchive.sha256"
Write-Output $windowsArchive
Write-Output "$windowsArchive.sha256"

# Keep existing folder packages intact; publish the standalone download separately.
$singleDirectory = Join-Path $resolvedOutput $manifest.windowsSingleFile.rootDirectory
dotnet publish $project `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --output $singleDirectory `
    -p:PublishSingleFile=true `
    -p:IncludeAllContentForSelfExtract=true `
    -p:PublishTrimmed=false `
    -p:DebugSymbols=false `
    -p:DebugType=None
if ($LASTEXITCODE -ne 0) {
    throw "Single-file Windows publish failed."
}

$singleFiles = @(Get-ChildItem -LiteralPath $singleDirectory -Recurse -File -Force)
if ($singleFiles.Count -ne 1 -or $singleFiles[0].Name -ne $manifest.windowsX64.entryPoint) {
    throw "Single-file output must contain exactly the Windows executable."
}
$singleDownload = Join-Path $resolvedOutput $manifest.windowsSingleFile.download
Copy-Item -LiteralPath $singleFiles[0].FullName -Destination $singleDownload
& (Join-Path $PSScriptRoot "Test-WindowsPackage.ps1") -Executable $singleDownload
$hash = (Get-FileHash -LiteralPath $singleDownload -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath "$singleDownload.sha256" `
    -Value "$hash  $([System.IO.Path]::GetFileName($singleDownload))" -Encoding utf8NoBOM
Write-Output $singleDownload
Write-Output "$singleDownload.sha256"
