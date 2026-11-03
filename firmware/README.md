# RF-Lab firmware

The firmware of the RF-Lab board: an Arduino Mega 2560 with the GRA & AFCH
AD9910 DDS shield, an Si5351 clock generator, an AD8307 detector on A8 and
an SSD1306 OLED. It is the RF-Lab R1.0 firmware, an MCNF node (node type
AE.70.10.80), and the same firmware the HASE RF-MiniLab uses.

- `DDS-AD9910-Arduino-Shield/` — the sketch. The MCNF part is
  `MCNF_*.ino` and `App_Device.ino` (the RF-Lab functions); the AD9910,
  menu and display code comes from GRA & AFCH.
- `libraries/` — the three libraries the Arduino library index does not
  carry in the needed form (the GRA & AFCH Encoder, Float64,
  PU2REO Si5351ArduinoLite).

Building and the other libraries: `docs/Building.md`. Flashing:
`tools/Flash-Firmware.ps1`. The protocol: `docs/Protocol.md`.

Licence: GPL (see the sketch's header and `COPYING`, the GNU GPL version 3,
under which the firmware as built with its libraries is distributed); see
`THIRD-PARTY-NOTICES.md` for the parts and their authors.
