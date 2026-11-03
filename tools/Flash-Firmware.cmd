@echo off
rem Writes the RF-Lab firmware: double-click, then enter the board's COM port.
"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -ExecutionPolicy Bypass -File "%~dp0Flash-Firmware.ps1" %*
pause
