# Building RF-Lab

## The application

Needs the .NET 10 SDK on Windows.

```
dotnet build app\RfLab.slnx
dotnet test app\RfLab.slnx
dotnet run --project app\src\RfLab
```

The tests run without a board: a byte-level stand-in for the firmware
answers the client (`app/tests/RfLab.Core.Tests/FakeRfLabNode.cs`).

| Project | Contents |
|---|---|
| `RfLab.Core` | the MCNF frames and session (`Mcnf/`), the serial port, the RF-Lab requests and ranges, `RfLabClient`, the stored settings (`Presets/`) |
| `RfLab` | the WPF window: the original RF-Lab layout and controls, themes, the view model |
| `RfLab.Core.Tests` | xUnit tests of the protocol, the client and the stored settings |

The manual is embedded in the program from `docs/Manual.pdf` when that
file exists. After changing `docs/Manual.md`, rebuild the PDF:

```
python tools\Build-ManualPdf.py docs\Manual.md docs\Manual.pdf
```

(Python with reportlab and the Windows fonts Segoe UI and Consolas.)

## The firmware

The sketch `firmware/DDS-AD9910-Arduino-Shield` builds with the Arduino IDE
2 or arduino-cli for the board **Arduino Mega or Mega 2560**, processor
**ATmega2560** (core `arduino:avr`). Its libraries:

| Library | Version | Where from |
|---|---|---|
| Encoder | 1.4.1, modified by GRA & AFCH | `firmware/libraries/Encoder` (the sketch refuses the unmodified one) |
| Float64 | — | `firmware/libraries/Float64` (not in the library index) |
| PU2REO Si5351ArduinoLite | 1.0.1 | `firmware/libraries/PU2REO_Si5351ArduinoLite` |
| Adafruit SSD1306 | 2.0.1 | library index |
| Adafruit GFX Library | 1.6.1 | library index |
| GParser | 1.4.0 | library index |
| GyverTimers | 1.10.0 | library index |
| ClickButton | 1.0.0 | library index |

With arduino-cli, after installing the index libraries
(`arduino-cli lib install "Adafruit SSD1306@2.0.1" …`):

```
arduino-cli compile --fqbn arduino:avr:mega:cpu=atmega2560 --libraries firmware\libraries firmware\DDS-AD9910-Arduino-Shield
```

In the Arduino IDE, copy the three folders of `firmware/libraries` into
your sketchbook's `libraries` folder (remove a stock Encoder library
first). The build takes about 60 KB of flash and 1.8 KB of RAM.

`AppConfig.h` holds the build switches (`USE_SI5351`, `USE_ENCODER` for the
front-panel encoder and RF-OUT key, both as the board is built) and the MCNF
node identity the application checks.

## A release

```
.\tools\Build-Release.ps1
```

compiles the firmware (installing the index libraries at the versions above
into `build\arduino`, not into your sketchbook), builds the manual's PDF,
runs the tests, publishes `RfLab.exe` as one self-contained file and writes
`dist\RF-Lab-<version>-windows-x64.zip` and `dist\RF-Lab-firmware-<version>.zip`.
The version is `<Version>` in `app/Directory.Build.props`. Options:
`-ArduinoCli <path>`, `-Python <path>`, `-SkipTests`.

## Screenshots and icon

`tools\Capture-Window.ps1 -Out docs\images\<name>.png` captures the running
application's window. `tools\Make-Icon.py` draws `app/src/RfLab/RfLab.ico`
(Pillow).
