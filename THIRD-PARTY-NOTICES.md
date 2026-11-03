# Third-party notices

## The application

The Windows application uses .NET (MIT) and System.IO.Ports (MIT), both
by the .NET Foundation and contributors, published by Microsoft. The
self-contained `RfLab.exe` includes the .NET runtime.

Its window and its controls (the frequency and amplitude dials, the digit
controls, the chart) are a port of the original RF-Lab 1.0 application
(Dr. Andreas Eppinger, 2025), with the commercial chart control of the
original replaced by a chart of its own.

## The firmware

The firmware in `firmware/DDS-AD9910-Arduino-Shield` is licensed under the
GNU General Public License, version 2 or later (see the header of
`DDS-AD9910-Arduino-Shield.ino`). The compiled firmware links libraries
under the GNU GPL version 3, so the firmware as distributed in a release is
under the GPL version 3. Its source is this repository's `firmware` folder.

| Part | Author | Licence |
|---|---|---|
| RF-Lab firmware, MCNF node | Dr. Andreas Eppinger | GPL-2.0-or-later |
| AD9910 shield code (`ad9910.cpp`, `ad9910.h`, `menuclk.*`, `main.*`) | GRA & AFCH (Grisha Anofriev), https://gra-afch.com | published with the shield's firmware without a licence statement; see below |
| Encoder (modified by GRA & AFCH), `firmware/libraries/Encoder` | Paul Stoffregen, GRA & AFCH | MIT |
| Float64, `firmware/libraries/Float64` | port of Berkeley SoftFloat | GPL-3.0 (SoftFloat: BSD) |
| PU2REO Si5351ArduinoLite, `firmware/libraries/PU2REO_Si5351ArduinoLite` | PU2REO, after Etherkit Si5351 (NT7S) | GPL-3.0 |
| Adafruit SSD1306 2.0.1 (library index) | Adafruit | BSD |
| Adafruit GFX Library 1.6.1 (library index) | Adafruit | BSD |
| GParser 1.4 (library index) | AlexGyver | MIT |
| GyverTimers 1.10 (library index) | AlexGyver | MIT |
| ClickButton 1.0 (library index) | raron | GPL-3.0 |
| Arduino AVR core | Arduino | LGPL-2.1 |

**The GRA & AFCH code.** GRA & AFCH publish the AD9910 shield's firmware
source for their customers to compile and modify
(https://github.com/afch/DDS-AD9910-Arduino-Shield); that repository carries
no licence file. The RF-Lab firmware is built on it. Its copyright stays
with GRA & AFCH.

The licence texts of the vendored libraries are in their folders under
`firmware/libraries`.
