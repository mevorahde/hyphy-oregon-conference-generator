[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $Executable
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest
$exe = (Resolve-Path -LiteralPath $Executable).Path
$manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot "release-manifest.json") -Raw | ConvertFrom-Json

function Invoke-Package {
    param([string[]] $Arguments = @(), [string] $InputText = "", [int] $ExpectedCode = 0)
    $start = [System.Diagnostics.ProcessStartInfo]::new($exe)
    $start.WorkingDirectory = Split-Path -Parent $exe
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardInput = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $start
    try {
        if (-not $process.Start()) { throw "Could not start package." }
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        $process.StandardInput.Write($InputText)
        $process.StandardInput.Close()
        if (-not $process.WaitForExit(30000)) {
            $process.Kill($true)
            throw "Package timed out."
        }
        $outputText = $stdout.GetAwaiter().GetResult().Replace("`r`n", "`n")
        $errorText = $stderr.GetAwaiter().GetResult()
        if ($process.ExitCode -ne $ExpectedCode) { throw "Unexpected exit code: $($process.ExitCode)" }
        if ($ExpectedCode -eq 0 -and $errorText.Length -ne 0) { throw "Unexpected stderr." }
        if ($outputText.Contains("Press Enter when finished:")) { throw "Redirected execution must not pause." }
        return $outputText
    } finally {
        $process.Dispose()
    }
}

if ((Invoke-Package -Arguments @("--version")).Trim() -ne "Hyphy Oregon Conference Generator $($manifest.version)") {
    throw "Version mismatch."
}
if (-not (Invoke-Package -Arguments @("--help")).Contains("Usage:")) { throw "Help missing." }
$owners = @("Alex", "Blake", "Casey", "Devon", "Emery", "Finley", "Gray", "Harper", "Indigo", "Jordan")
$drawArguments = @()
foreach ($owner in $owners) { $drawArguments += @("--owner", $owner) }
$drawArguments += @("--seed", "20200830")
$first = Invoke-Package -Arguments $drawArguments
$second = Invoke-Package -Arguments $drawArguments
if ($first -cne $second) { throw "Seed repeatability failed." }
$golden = "East`n  - Indigo`n  - Devon`n  - Casey`n  - Finley`n  - Blake`n`nWest`n  - Emery`n  - Jordan`n  - Alex`n  - Gray`n  - Harper"
if (-not $first.Contains($golden)) { throw "Golden draw mismatch." }
$interactive = Invoke-Package -InputText ("y`n" + ($owners -join "`n") + "`n20200830`n")
if (-not $interactive.Contains($golden)) { throw "Default interactive draw mismatch." }
$custom = Invoke-Package -InputText "n`n2`n4`nNorth`nSouth`nAlex`nBlake`nCasey`nDevon`n42`n"
foreach ($conference in @("North", "South")) {
    if ($custom -notmatch "(?m)^$conference\n  - [^\n]+\n  - [^\n]+") { throw "Custom conference balance failed." }
}
foreach ($owner in $owners[0..3]) {
    if ([regex]::Matches($custom, "(?m)^  - $owner$").Count -ne 1) { throw "Custom owner uniqueness failed." }
}
$random = Invoke-Package -InputText ("y`n" + ($owners -join "`n") + "`n`n")
foreach ($owner in $owners) {
    if ([regex]::Matches($random, "(?m)^  - $owner$").Count -ne 1) { throw "Unseeded owner uniqueness failed." }
}
$null = Invoke-Package -Arguments @("--unknown") -ExpectedCode 2
$null = Invoke-Package -InputText "" -ExpectedCode 3
Write-Output "Package smoke checks passed: version, help, seeded golden/repeatability, default/custom/unseeded draws, invalid option, EOF, and non-pausing redirected execution."
