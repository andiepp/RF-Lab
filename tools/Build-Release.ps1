<#
.SYNOPSIS
    Builds an RF-Lab release: the firmware, the Windows application and the
    manual, zipped for a GitHub release.

.DESCRIPTION
    1. Compiles the firmware with arduino-cli for the Arduino Mega 2560
       (core arduino:avr). The libraries come from firmware/libraries (the
       three the Arduino library index does not have in the needed form)
       and, at fixed versions, from the library index; those are installed
       into build/arduino, not into your own sketchbook.
    2. Runs the tests and publishes the application as one self-contained
       RfLab.exe for Windows x64 (no .NET installation needed).
    3. Builds docs/Manual.pdf (needs Python with reportlab; skipped
       otherwise). The application embeds it: build the PDF before
       publishing, which this script does.
    4. Writes to dist/:
         RF-Lab-<version>-windows-x64.zip   application, example presets, firmware, flash tool, documents
         RF-Lab-firmware-<version>.zip      the firmware alone

.PARAMETER ArduinoCli
    arduino-cli.exe; by default the one on the PATH or the one inside the Arduino IDE 2.

.PARAMETER Python
    A python.exe with reportlab, for the PDF; by default py or python from the PATH.

.PARAMETER SkipTests
    Publishes without running the tests first.

.EXAMPLE
    .\tools\Build-Release.ps1
#>
[CmdletBinding()]
param(
    [string] $ArduinoCli,
    [string] $Python,
    [switch] $SkipTests
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$build = Join-Path $root 'build'
$dist = Join-Path $root 'dist'

# the firmware's libraries at the versions it was built and tested with
$libraries = @(
    'Adafruit SSD1306@2.0.1',
    'Adafruit GFX Library@1.6.1',
    'GParser@1.4.0',
    'GyverTimers@1.10.0',
    'ClickButton@1.0.0'
)

function Step($text) { Write-Host "`n=== $text" -ForegroundColor Cyan }

function Find-ArduinoCli {
    if ($ArduinoCli) { return $ArduinoCli }
    $onPath = Get-Command arduino-cli -ErrorAction SilentlyContinue
    if ($onPath) { return $onPath.Source }
    $candidates = @(
        "$env:LOCALAPPDATA\Programs\Arduino IDE\resources\app\lib\backend\resources\arduino-cli.exe",
        "$env:ProgramFiles\Arduino IDE\resources\app\lib\backend\resources\arduino-cli.exe"
    )
    foreach ($candidate in $candidates) { if (Test-Path $candidate) { return $candidate } }
    throw 'arduino-cli was not found; name it with -ArduinoCli.'
}

# --- version ---------------------------------------------------------------------------
$props = [xml](Get-Content (Join-Path $root 'app\Directory.Build.props') -Raw)
$fullVersion = $props.Project.PropertyGroup.Version
$version = ($fullVersion -split '\.')[0..1] -join '.'
Write-Host "RF-Lab $version ($fullVersion)"

Remove-Item $build, $dist -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $build, $dist | Out-Null

# --- firmware ----------------------------------------------------------------------------
Step 'Firmware'
$cli = Find-ArduinoCli
$arduinoUser = Join-Path $build 'arduino'
$cliConfig = Join-Path $build 'arduino-cli.yaml'
@"
directories:
  data: $($env:LOCALAPPDATA -replace '\\', '/')/Arduino15
  user: $($arduinoUser -replace '\\', '/')
"@ | Set-Content $cliConfig -Encoding ascii
if (-not (& $cli --config-file $cliConfig core list | Select-String 'arduino:avr')) {
    & $cli --config-file $cliConfig core install arduino:avr
}
& $cli --config-file $cliConfig lib install @libraries
if ($LASTEXITCODE -ne 0) { throw 'Installing the firmware libraries failed.' }

$firmwareOut = Join-Path $build 'firmware-raw'
$sketch = Join-Path $root 'firmware\DDS-AD9910-Arduino-Shield'
& $cli --config-file $cliConfig compile --fqbn arduino:avr:mega:cpu=atmega2560 `
    --libraries (Join-Path $root 'firmware\libraries') --output-dir $firmwareOut $sketch
if ($LASTEXITCODE -ne 0) { throw "The firmware did not compile (exit code $LASTEXITCODE)." }

$firmware = Join-Path $build 'firmware'
New-Item -ItemType Directory -Force $firmware | Out-Null
Copy-Item (Join-Path $firmwareOut 'DDS-AD9910-Arduino-Shield.ino.hex') (Join-Path $firmware 'RF-Lab-firmware.hex')
Copy-Item (Join-Path $PSScriptRoot 'Flash-Firmware.ps1'), (Join-Path $PSScriptRoot 'Flash-Firmware.cmd') $firmware
@"
RF-Lab firmware $version for the Arduino Mega 2560 with the GRA & AFCH AD9910 shield

This is the MCNF firmware the HASE RF-MiniLab uses as well: one firmware
serves both the RF-Lab application and HASE.

Flash it with Flash-Firmware.cmd (double-click, then enter the COM port) or:
  powershell -ExecutionPolicy Bypass -File Flash-Firmware.ps1 -Port COM16
The script needs avrdude, which comes with the Arduino IDE. Other tools:
  avrdude -p atmega2560 -c wiring -b 115200 -D -U flash:w:RF-Lab-firmware.hex:i -P COM16

The firmware contains code under the GPL and code by GRA & AFCH; see
THIRD-PARTY-NOTICES.md. Source: https://github.com/andiepp/RF-Lab (firmware/)
"@ | Set-Content (Join-Path $firmware 'README.txt') -Encoding utf8

# --- documents -------------------------------------------------------------------------
Step 'Documents'
if (-not $Python) { $Python = (Get-Command py, python -ErrorAction SilentlyContinue | Select-Object -First 1).Source }
if ($Python) {
    & $Python (Join-Path $PSScriptRoot 'Build-ManualPdf.py') (Join-Path $root 'docs\Manual.md') (Join-Path $root 'docs\Manual.pdf')
}
if (-not (Test-Path (Join-Path $root 'docs\Manual.pdf'))) {
    Write-Warning 'No Manual.pdf (Python with reportlab missing): the application carries no manual.'
}

# --- application -----------------------------------------------------------------------
Step 'Application'
$solution = Join-Path $root 'app\RfLab.slnx'
if (-not $SkipTests) {
    dotnet test $solution -c Release
    if ($LASTEXITCODE -ne 0) { throw 'The tests failed.' }
}
$appOut = Join-Path $build 'app'
dotnet publish (Join-Path $root 'app\src\RfLab\RfLab.csproj') -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true `
    -p:DebugType=none -o $appOut
if ($LASTEXITCODE -ne 0) { throw 'Publishing the application failed.' }

# --- packages ----------------------------------------------------------------------------
Step 'Packages'
$name = "RF-Lab-$version-windows-x64"
$package = Join-Path $build $name
New-Item -ItemType Directory -Force $package | Out-Null
Copy-Item (Join-Path $appOut 'RfLab.exe') $package
Copy-Item (Join-Path $appOut 'presets') (Join-Path $package 'presets') -Recurse
Copy-Item $firmware (Join-Path $package 'firmware') -Recurse
$docs = Join-Path $package 'docs'
New-Item -ItemType Directory -Force $docs | Out-Null
Copy-Item (Join-Path $root 'docs\*.md'), (Join-Path $root 'docs\Manual.pdf') $docs -ErrorAction SilentlyContinue
Copy-Item (Join-Path $root 'LICENSE'), (Join-Path $root 'THIRD-PARTY-NOTICES.md') $package
# the firmware is GPL: its folder carries the GPL text, not the application's MIT licence
Copy-Item (Join-Path $root 'firmware\COPYING'), (Join-Path $root 'THIRD-PARTY-NOTICES.md') (Join-Path $package 'firmware')
@"
RF-Lab $version

1. Start RfLab.exe, choose the board's COM port, Connect.
   The board runs the RF-Lab (MCNF) firmware; if it does not, flash it
   with firmware\Flash-Firmware.cmd (see docs\Manual, chapter Installation).
2. The example stored settings in presets\ are copied to
   %LocalAppData%\RF-Lab\Presets at the first start.

Documentation: docs\Manual.pdf (also the Manual key in the application), docs\Protocol.md
Project: https://github.com/andiepp/RF-Lab
"@ | Set-Content (Join-Path $package 'README.txt') -Encoding utf8

# ZipFile writes '/' in entry names (Compress-Archive of Windows PowerShell 5.1 writes '\',
# which other systems' unzip tools do not take as folders)
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($package, (Join-Path $dist "$name.zip"), 'Optimal', $false)
[IO.Compression.ZipFile]::CreateFromDirectory((Join-Path $package 'firmware'), (Join-Path $dist "RF-Lab-firmware-$version.zip"), 'Optimal', $false)
Get-ChildItem $dist | ForEach-Object { Write-Host ("{0,-40} {1,8:N1} MB" -f $_.Name, ($_.Length / 1MB)) }
Write-Host "`nRelease $version built in $dist" -ForegroundColor Green
