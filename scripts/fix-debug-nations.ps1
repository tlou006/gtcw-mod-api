#Requires -Version 5.1
<#
.SYNOPSIS
    Upgrades Battlefields/DebugArmySetup/nations.dat from the legacy 40-line
    alliance format to the current 42-line format.

.DESCRIPTION
    The shipped debug army setup is stale. Its nations.dat still uses an
    alliance block of 40 lines, and GameVars.import_nations has no version gate
    for the two fields it lacks, so debug_loaddebugarmysetup throws a
    FormatException on load. That makes the game's own deterministic-scenario
    path unusable, which is the cheapest test fixture a hook library could ask
    for.

    The two missing fields are the last two of each alliance block:

        line 41  volunteersadj
        line 42  contractlength

    Established by diffing the file against a shipped battlefield nations.dat
    with the same state count: the first 40 lines align field for field, and
    the alliance names align 1:1, so the additions are at the end.

    Why the version gate does not save this file: import_nations does read a
    version, but the only thing it gates is

        if ((versionFromFile >= 0.7403f) | !foldername.Contains("Save"))

    and a Battlefields path contains no "Save", so the branch is always taken
    and line 42 is always read. There is no escape by declaring an old version
    in version.dat -- the file itself has to be current.

    This script emits no game data of its own. It reads the publisher's two
    files and writes a merge of them into the install, which is why the
    transformation is tracked here and the result is not.

.PARAMETER GameDir
    The game install. Defaults to the repo's own copy under game/, per the
    convention in scripts/README.md; point it at a real install to fix one.

.PARAMETER Reference
    Battlefield folder, relative to the install, supplying the two values per
    alliance. Must be in the current 42-line format and is verified to be.

.PARAMETER Verify
    Check the file's structure and report, without writing anything.

.EXAMPLE
    .\scripts\fix-debug-nations.ps1 -Verify

.EXAMPLE
    .\scripts\fix-debug-nations.ps1 -GameDir "E:\SteamLibrary\steamapps\common\Grand Tactician The Civil War (1861-1865)"
#>
[CmdletBinding()]
param(
    [string]$GameDir = (Join-Path (Split-Path -Parent $PSScriptRoot) 'game'),
    [string]$Reference = 'Battlefields\000\A',
    [switch]$Verify
)

$ErrorActionPreference = 'Stop'

# Field counts, from GameVars.import_nations. The alliance block is 20 scalar
# fields, then taxcaps[5], debt, subsidies[8], then eight more scalars.
$LinesPerState         = 19
$LinesPerAllianceNow   = 42
$LinesPerAllianceLegacy = 40
$Alliances             = 8

$target    = Join-Path $GameDir 'Battlefields\DebugArmySetup\nations.dat'
$reference = Join-Path $GameDir (Join-Path $Reference 'nations.dat')

foreach ($path in @($target, $reference)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Not found: $path"
    }
}

# The game reads these with Encoding.Default (system ANSI), so round-trip the
# same bytes rather than normalising to UTF-8.
$enc = [System.Text.Encoding]::Default

function Read-Lines([string]$path) {
    # Split manually: Get-Content would strip the distinction between a
    # trailing newline and none, and line count is load-bearing here.
    $text = [System.IO.File]::ReadAllText($path, $enc)
    return ($text -replace "`r`n", "`n").TrimEnd("`n") -split "`n"
}

function Measure-Nations([string[]]$lines, [string]$label) {
    $states = 0
    if (-not [int]::TryParse($lines[0], [ref]$states)) {
        throw "$($label): first line is not a state count: '$($lines[0])'"
    }

    $allianceCountIndex = 1 + ($states * $LinesPerState)
    if ($lines.Count -le $allianceCountIndex) {
        throw "$($label): file ends before the alliance count; $($states) states would need $($allianceCountIndex + 1) lines, file has $($lines.Count)."
    }

    $allianceCount = 0
    if (-not [int]::TryParse($lines[$allianceCountIndex], [ref]$allianceCount)) {
        throw "$($label): expected an alliance count at line $($allianceCountIndex + 1), found '$($lines[$allianceCountIndex])'."
    }

    $allianceLines = $lines.Count - $allianceCountIndex - 1
    $perAlliance   = -1
    if ($allianceCount -gt 0 -and ($allianceLines % $allianceCount) -eq 0) {
        $perAlliance = $allianceLines / $allianceCount
    }

    return [pscustomobject]@{
        Label              = $label
        Lines              = $lines.Count
        States             = $states
        AllianceCount      = $allianceCount
        AllianceCountIndex = $allianceCountIndex
        LinesPerAlliance   = $perAlliance
    }
}

$targetLines = Read-Lines $target
$refLines    = Read-Lines $reference

$t = Measure-Nations $targetLines 'DebugArmySetup'
$r = Measure-Nations $refLines    'reference'

Write-Host ("{0,-16} {1,5} lines  {2,3} states  {3} alliances x {4} lines" -f `
    $t.Label, $t.Lines, $t.States, $t.AllianceCount, $t.LinesPerAlliance)
Write-Host ("{0,-16} {1,5} lines  {2,3} states  {3} alliances x {4} lines" -f `
    $r.Label, $r.Lines, $r.States, $r.AllianceCount, $r.LinesPerAlliance)
Write-Host ""

if ($r.LinesPerAlliance -ne $LinesPerAllianceNow) {
    throw "The reference is not in the current format ($($r.LinesPerAlliance) lines per alliance, expected $($LinesPerAllianceNow)). Pick another battlefield folder with -Reference."
}

if ($t.LinesPerAlliance -eq $LinesPerAllianceNow) {
    Write-Host "Already in the current format; nothing to do." -ForegroundColor Green
    return
}

if ($t.LinesPerAlliance -ne $LinesPerAllianceLegacy) {
    throw "DebugArmySetup is neither the legacy nor the current format ($($t.LinesPerAlliance) lines per alliance). Refusing to guess."
}

if ($t.AllianceCount -ne $Alliances -or $r.AllianceCount -ne $Alliances) {
    throw "Expected $($Alliances) alliances in both files; found $($t.AllianceCount) and $($r.AllianceCount)."
}

if ($Verify) {
    Write-Host "Legacy format confirmed: $($Alliances) alliance blocks are two lines short each." -ForegroundColor Yellow
    Write-Host "Run without -Verify to upgrade (the original is backed up)."
    return
}

# --- merge ------------------------------------------------------------------

# Everything up to and including the alliance count is kept verbatim.
$out = New-Object System.Collections.Generic.List[string]
for ($i = 0; $i -le $t.AllianceCountIndex; $i++) {
    $out.Add($targetLines[$i])
}

for ($a = 0; $a -lt $Alliances; $a++) {
    $tStart = $t.AllianceCountIndex + 1 + ($a * $LinesPerAllianceLegacy)
    $rStart = $r.AllianceCountIndex + 1 + ($a * $LinesPerAllianceNow)

    # Alliance 0 is the Union in both files and so on down; if that ever stops
    # holding, the two values would be copied onto the wrong nation.
    $tName = $targetLines[$tStart]
    $rName = $refLines[$rStart]
    if ($tName -ne $rName) {
        throw "Alliance $($a) is '$($tName)' in DebugArmySetup but '$($rName)' in the reference. The blocks are not in the same order; refusing to merge."
    }

    for ($i = 0; $i -lt $LinesPerAllianceLegacy; $i++) {
        $out.Add($targetLines[$tStart + $i])
    }

    # volunteersadj, then contractlength.
    $volunteersadj  = $refLines[$rStart + 40]
    $contractlength = $refLines[$rStart + 41]
    $out.Add($volunteersadj)
    $out.Add($contractlength)

    Write-Host ("  {0,-16} + volunteersadj={1,-5} contractlength={2}" -f $tName, $volunteersadj, $contractlength)
}

$expected = 1 + ($t.States * $LinesPerState) + 1 + ($Alliances * $LinesPerAllianceNow)
if ($out.Count -ne $expected) {
    throw "Produced $($out.Count) lines, expected $($expected). Not written."
}

$backup = "$($target).orig"
if (-not (Test-Path -LiteralPath $backup)) {
    Copy-Item -LiteralPath $target -Destination $backup
    Write-Host ""
    Write-Host "Backed up the original to $($backup)"
}
else {
    Write-Host ""
    Write-Host "Backup already exists at $($backup); leaving it alone."
}

# CRLF throughout, including a trailing one, which is what the shipped files
# have and what the line arithmetic above assumes.
$text = ($out -join "`r`n") + "`r`n"
[System.IO.File]::WriteAllText($target, $text, $enc)

$check = Measure-Nations (Read-Lines $target) 'written'
if ($check.LinesPerAlliance -ne $LinesPerAllianceNow) {
    throw "Post-write check failed: $($check.LinesPerAlliance) lines per alliance."
}

Write-Host ""
Write-Host ("Wrote $($check.Lines) lines: $($check.States) states, $($check.AllianceCount) alliances x $($check.LinesPerAlliance) lines.") -ForegroundColor Green
