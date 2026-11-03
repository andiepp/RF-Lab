<#
.SYNOPSIS
    Writes the RF-Lab firmware into the Arduino Mega 2560 of an RF-Lab board.

.DESCRIPTION
    Uses avrdude with the Mega's own bootloader (protocol "wiring", 115200 baud),
    exactly as the Arduino IDE uploads a sketch. Nothing else on the board
    changes; the EEPROM keeps its contents.

    avrdude comes with the Arduino IDE (in %LocalAppData%\Arduino15) and is
    found there, on the PATH, or where -Avrdude names it.

    Close the RF-Lab application (and a HASE host using the board) first:
    the port must be free.

.PARAMETER Port
    The board's serial port, for example COM16. Asked for when missing.

.PARAMETER Hex
    The firmware file; by default RF-Lab-firmware.hex beside this script.

.PARAMETER Avrdude
    avrdude.exe, when it is not found by itself.

.PARAMETER Check
    Only talks to the bootloader and reads the chip's signature; writes nothing.

.EXAMPLE
    .\Flash-Firmware.ps1 -Port COM16
.EXAMPLE
    .\Flash-Firmware.ps1 -Port COM16 -Check
#>
[CmdletBinding()]
param(
    [string] $Port,
    [string] $Hex,
    [string] $Avrdude,
    [switch] $Check
)

$ErrorActionPreference = 'Stop'

function Find-Avrdude {
    if ($Avrdude) { return $Avrdude }
    $onPath = Get-Command avrdude -ErrorAction SilentlyContinue
    if ($onPath) { return $onPath.Source }
    $tools = Join-Path $env:LOCALAPPDATA 'Arduino15\packages\arduino\tools\avrdude'
    if (Test-Path $tools) {
        $found = Get-ChildItem $tools -Recurse -Filter avrdude.exe | Sort-Object FullName -Descending | Select-Object -First 1
        if ($found) { return $found.FullName }
    }
    throw 'avrdude was not found. Install the Arduino IDE (it brings avrdude) or name avrdude.exe with -Avrdude.'
}

$tool = Find-Avrdude
# avrdude 7 and later keep avrdude.conf in ..\etc, older ones beside the program
$config = @(
    (Join-Path (Split-Path (Split-Path $tool)) 'etc\avrdude.conf'),
    (Join-Path (Split-Path $tool) 'avrdude.conf')
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $config) { throw "avrdude.conf was not found next to $tool." }

if (-not $Port) {
    $ports = [IO.Ports.SerialPort]::GetPortNames() | Sort-Object
    Write-Host "Serial ports: $($ports -join ', ')"
    $Port = Read-Host 'The RF-Lab board''s port (for example COM16)'
}
if ($Port -notmatch '^COM\d+$') { throw "'$Port' is not a COM port." }

$arguments = @("-C$config", '-patmega2560', '-cwiring', "-P$Port", '-b115200', '-D')
if ($Check) {
    $arguments += '-n'
    Write-Host "Reading the bootloader's signature on $Port (writes nothing) ..."
} else {
    if (-not $Hex) { $Hex = Join-Path $PSScriptRoot 'RF-Lab-firmware.hex' }
    if (-not (Test-Path $Hex)) { throw "The firmware file $Hex does not exist." }
    $arguments += "-Uflash:w:$((Resolve-Path $Hex).Path):i"
    Write-Host "Writing $(Split-Path $Hex -Leaf) to the Arduino Mega on $Port ..."
}

& $tool @arguments
if ($LASTEXITCODE -ne 0) { throw "avrdude ended with exit code $LASTEXITCODE." }
if ($Check) { Write-Host 'The bootloader answered.' -ForegroundColor Green }
else { Write-Host 'Done. The board restarts with the new firmware.' -ForegroundColor Green }
