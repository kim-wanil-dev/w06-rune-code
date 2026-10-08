param(
    [string]$Spell = 'default',
    [string]$Scenario = 'dummy_line',
    [int]$Ticks = 600,
    [int]$Seed = 1,
    [int]$Stage = 1,
    [double]$Duration = 0,
    [int]$EnergyLevel = 0,
    [int]$CastInterval = 30,
    [string]$Output = 'Builds/sim-result.json'
)
$runeProject = Split-Path -Parent $PSScriptRoot
$runeExecutable = Join-Path $runeProject 'Builds/RuneCodePoC-ManualMods/RuneCodePoC.exe'
if (-not (Test-Path -LiteralPath $runeExecutable)) { throw 'Build the Windows PoC first with Rune Code > Build Windows PoC.' }
$runeOutput = [IO.Path]::GetFullPath((Join-Path $runeProject $Output))
$runeLog = Join-Path $runeProject 'Logs/rune-sim-player.log'
$runeArguments = @('-batchmode', '-nographics', '--sim', '--spell', $Spell, '--scenario', $Scenario, '--ticks', $Ticks, '--seed', $Seed, '--stage', $Stage, '--duration', $Duration.ToString([Globalization.CultureInfo]::InvariantCulture), '--energy-level', $EnergyLevel, '--cast-interval', $CastInterval, '--output', $runeOutput, '-logFile', $runeLog)
& $runeExecutable @runeArguments | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Simulation failed. Inspect $runeLog" }
Get-Content -LiteralPath $runeOutput
