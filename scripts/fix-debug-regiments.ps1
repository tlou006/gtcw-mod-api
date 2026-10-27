#Requires -Version 5.1
<#
.SYNOPSIS
    Upgrades Battlefields/DebugArmySetup/regiments.dat from the legacy 38-line
    regiment record to the current 39-line record.

.DESCRIPTION
    The companion to fix-debug-nations.ps1. The shipped debug army setup is
    stale in two files, and this is the second: its regiments.dat predates the
    morale field that BattleUnits.import_units now expects.

    Unlike nations.dat, this field IS version-gated:

        float num17 = ((!(num >= 0.7802f)) ? 1f : float.Parse(streamReader.ReadLine()));

    read as the 39th and last field of each record, and passed to
    create_regiment as its trailing `float morale = 1f` parameter. But the gate
    cannot save the file, because of where `num` comes from:

        float num = ImportExportUnitData.GetVersionFromFile(text + "/");
        if (num == 0f) num = MainMenuControls.currentversion;

    GetVersionFromFile returns 0 when version.dat is absent, and
    DebugArmySetup has no version.dat, so `num` falls back to the current game
    version -- far above 0.7802. The reader therefore expects 39 fields and the
    38-field records shift by one from the second regiment onward.

    Declaring an old version in a version.dat would also work and is one line,
    but `num` is consumed elsewhere in import_units by two comparisons whose
    scale is unexplained (`num < 14`, and `num >= unittyp && num < 16`), so
    bringing the file to the current format is the safer route.

    The appended value is 1, which is exactly what the legacy branch of that
    gate substitutes: full morale. So this reproduces the behaviour the file was
    written for, rather than inventing data. Nothing is copied from another
    scenario, so unlike the nations fix this one needs no reference file.

.PARAMETER GameDir
    The game install. Defaults to the repo's own copy under game/, per the
    convention in scripts/README.md; point it at a real install to fix one.

.PARAMETER Verify
    Check the file's structure and report, without writing anything.

.EXAMPLE
    .\scripts\fix-debug-regiments.ps1 -Verify

.EXAMPLE
    .\scripts\fix-debug-regiments.ps1 -GameDir "E:\SteamLibrary\steamapps\common\Grand Tactician The Civil War (1861-1865)"
#>
[CmdletBinding()]
param(
    [string]$GameDir = (Join-Path (Split-Path -Parent $PSScriptRoot) 'game'),
    [switch]$Verify
)

$ErrorActionPreference = 'Stop'

$LinesPerRecordNow    = 39
$LinesPerRecordLegacy = 38

# The legacy default from the version gate: create_regiment's `float morale = 1f`.
$MoraleDefault = '1'

# Offset of the regiment name within a record. Field 1 is read and discarded,
# field 2 is the name -- useful for showing that the record boundaries the
# arithmetic found are real.
$NameOffset = 1

$target = Join-Path $GameDir 'Battlefields\DebugArmySetup\regiments.dat'
if (-not (Test-Path -LiteralPath $target)) {
    throw "Not found: $target"
}

# import_units reads this one as UTF8, unlike nations.dat which is ANSI.
# Neither shipped copy carries a BOM, and a BOM would be fatal: the first line
# is a record count, and int.Parse would choke on the mark.
$enc = New-Object System.Text.UTF8Encoding($false)

function Read-Lines([string]$path) {
    $text = [System.IO.File]::ReadAllText($path, $enc)
    return ($text -replace "`r`n", "`n").TrimEnd("`n") -split "`n"
}

$lines = Read-Lines $target

$count = 0
if (-not [int]::TryParse($lines[0], [ref]$count)) {
    throw "First line is not a regiment count: '$($lines[0])'"
}
if ($count -le 0) {
    throw "Regiment count is $($count); nothing to upgrade."
}

$body       = $lines.Count - 1
$perRecord  = -1
if (($body % $count) -eq 0) {
    $perRecord = $body / $count
}

Write-Host ("regiments.dat  {0,5} lines  {1,3} regiments x {2} lines" -f $lines.Count, $count, $perRecord)
Write-Host ""

if ($perRecord -eq $LinesPerRecordNow) {
    Write-Host "Already in the current format; nothing to do." -ForegroundColor Green
    return
}

if ($perRecord -ne $LinesPerRecordLegacy) {
    throw "Neither the legacy ($($LinesPerRecordLegacy)) nor the current ($($LinesPerRecordNow)) record size: $($perRecord) lines per record. Refusing to guess."
}

if ($Verify) {
    Write-Host "Legacy format confirmed: $($count) records are one line short each." -ForegroundColor Yellow
    Write-Host "Run without -Verify to upgrade (the original is backed up)."
    return
}

# --- rewrite ----------------------------------------------------------------

$out = New-Object System.Collections.Generic.List[string]
$out.Add($lines[0])

for ($r = 0; $r -lt $count; $r++) {
    $start = 1 + ($r * $LinesPerRecordLegacy)

    for ($i = 0; $i -lt $LinesPerRecordLegacy; $i++) {
        $out.Add($lines[$start + $i])
    }

    $out.Add($MoraleDefault)

    $name = $lines[$start + $NameOffset]
    Write-Host ("  {0,3}. {1,-34} + morale={2}" -f ($r + 1), $name, $MoraleDefault)
}

$expected = 1 + ($count * $LinesPerRecordNow)
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

$text = ($out -join "`r`n") + "`r`n"
[System.IO.File]::WriteAllText($target, $text, $enc)

$after = Read-Lines $target
$afterPerRecord = ($after.Count - 1) / $count
if ($afterPerRecord -ne $LinesPerRecordNow) {
    throw "Post-write check failed: $($afterPerRecord) lines per record."
}

Write-Host ""
Write-Host ("Wrote $($after.Count) lines: $($count) regiments x $($afterPerRecord) lines.") -ForegroundColor Green
