# RF-Lab Protocol

The RF-Lab board speaks MCNF, a small binary command-response protocol, on
the Arduino Mega's USB serial port. The PC sends one request frame and reads
one answer frame of a length it states itself; nothing else is on the line
(except the debug text noted under AM and FM). This document describes the
protocol as the RF-Lab firmware 1.0 implements it. The examples are frames
captured from a board.

## The serial port

| | |
|---|---|
| Format | 115200 baud, 8 data bits, no parity, 1 stop bit, no handshake |
| DTR, RTS | asserted |
| Start-up | opening the port restarts the Mega (DTR pulses its reset); wait 3 s before the first request (1.5 s is too short) and discard what arrived |
| Pace | one request at a time; send the next only after the answer |

## Connectivity test

A single byte, before any frame:

```
PC → A1
PC ← 21
```

## Frames

A request:

| Byte | Meaning |
|---|---|
| 0 | channel: `A4` node administration, `A5` the RF-Lab device |
| 1 | N, the number of bytes that follow the header: function, parameters and checksum |
| 2 | R, the length of the expected answer, error byte and checksum included |
| 3 | T, execution time; always `00` |
| 4 | function |
| 5 … | parameters, multi-byte values most significant byte first |
| last | checksum: `FF` minus the sum of all preceding bytes, modulo 256 |

The answer:

| Byte | Meaning |
|---|---|
| 0 | error: `00` success, otherwise an error code (below) |
| 1 … | data |
| last | checksum over the preceding answer bytes, as above |

The firmware computes the answer's checksum only for a successful answer;
an answer with an error code carries an arbitrary last byte.

Example, a 10 MHz carrier at -20 dB:

```
PC → A5 08 02 00 10 00 98 96 80 00 14 7E
       │  │  │  │  │  └──────────┘ └───┘ └ checksum
       │  │  │  │  │   10 000 000   20 dB
       │  │  │  │  function 10: carrier
       │  │  │  T
       │  │  R = 2: error byte + checksum
       │  N = 8: function, 6 parameter bytes, checksum
       channel A5
PC ← 00 FF
```

## Node administration (channel A4)

| Function | Request | Answer data |
|---|---|---|
| `DC` (220) node type | — | 4 bytes: `AE 70 10 80` (implementor, platform Mega 2560, application RF-Lab, UART) |
| `DD` (221) buffer size | — | 1 byte: the message buffer, `40` (64) |
| `DE` (222) error status | — | 1 byte: the last error, then cleared |

```
PC → A4 02 06 00 DC 77
PC ← 00 AE 70 10 80 51
```

A program should check the node type before it sends anything else: other
MCNF boards (and other devices) may use the same USB chip.

## The RF-Lab device (channel A5)

### Configuration

| Function | Request | Answer data |
|---|---|---|
| `C9` (201) read configuration | device number low, high, `00 00` (`01 00 00 00`) | 4 bytes: variable sets, active set, capabilities, flags — bit 0 LED on, bit 1 Si5351 fitted |

```
PC → A5 06 06 00 C9 01 00 00 00 84
```

### LED

| Function | Request | Answer data |
|---|---|---|
| `01` LED on | — | 1 byte: the LED's state, `01` |
| `02` LED off | — | 1 byte: the LED's state, `00` |
| `03` LED state | — | 1 byte: `00` off, `01` on |

The LED is the one function that reads a pin back: a good first test.

```
PC → A5 02 03 00 03 52
PC ← 00 01 FE
```

### Signal

All signal functions answer with no data (R = 2). The attenuation travels as
a positive number of dB, 0 – 80; the firmware negates it.

| Function | Parameters |
|---|---|
| `10` carrier | frequency (4 bytes, Hz), attenuation (2 bytes, dB) |
| `11` AM | carrier (4, Hz), attenuation (2, dB), modulation frequency (4, Hz), depth (4, %) |
| `12` FM | carrier (4, Hz), attenuation (2, dB), modulation frequency (4, Hz), deviation (4, Hz) |
| `13` sweep | start (4, Hz), stop (4, Hz), sweep time (2, ms), attenuation (2, dB), mode (1: `00` bidirectional, `01` ramp, `02` single ramp) |

A new signal request replaces whatever the board was doing; the carrier
request also ends a modulation or a sweep. There is no "output off".

```
PC → A5 10 02 00 11 00 98 96 80 00 1E 00 00 03 E8 00 00 00 32 4E      AM 10 MHz, -30 dB, 1000 Hz, 50 %
PC ← highByte((uint16_t)Step_Rate)=1 ⏎ lowByte(...)=… ⏎ … ⏎ 00 FF
PC → A5 0F 02 00 13 00 98 96 80 01 C9 C3 80 07 D0 00 1E 01 85         ramp 10 – 30 MHz, 2 s, -30 dB
PC ← 00 FF
```

**Debug text before AM and FM answers.** While the firmware computes an AM
or FM waveform it prints about 100 – 130 bytes of debug text (the AD9910
code's `Serial.print` lines) on the same port, followed by the regular
answer. A client must skip it: read bytes until the first one that is not
printable ASCII (0x20 – 0x7E) or CR/LF — that is the error byte — and then
the rest of the answer. The RF-Lab application and `tools/rflab.py` do
this; the application shows the text in its trace.

### Detector

| Function | Request | Answer data |
|---|---|---|
| `20` read sensor | — | 2 bytes: the converter value 0 – 1023 of input A8 against the 2.56 V reference |

```
PC → A5 02 04 00 20 34
PC ← 00 00 96 69              150 → 375 mV
```

Millivolts = value × 2.5. The AD8307 level in dB, as the RF-Lab application
computes it: −(2235 − mV) / 25.917 − 0.5, and −70 below 200 mV.

### Clock generator

| Function | Parameters |
|---|---|
| `40` set clock | output (1 byte, 0 – 2), frequency (8 bytes, in 0.01 Hz) |

Range 10 kHz – 160 MHz. Errors: `10` no Si5351, `11` no such output.

```
PC → A5 0B 02 00 40 00 00 00 00 00 05 F5 E1 00 32      CLK0 = 1 MHz (100 000 000 × 0.01 Hz)
PC ← 00 FF
```

## Error codes

| Code | Meaning |
|---|---|
| `01` | checksum error in the request |
| `02` | unknown ID (channel or device number) |
| `03` | unknown function |
| `04`, `05` | variable index or data out of range |
| `06` | message type |
| `07` | buffer overflow (request or answer longer than the node's buffer) |
| `08` | sync missing |
| `0B` | receive timeout: the rest of the request did not arrive within 500 ms |
| `10` | Si5351 not fitted |
| `11` | Si5351 output number above 2 |

Codes `09`, `0A` and `0C` – `0E` (port, connection, storage and gateway
errors) belong to larger MCNF nodes.

## Functions not used

The firmware also stores parameters for a message generator (functions
`30` – `32`, RTTY/NAVTEX patterns of the original application) but never
transmits them; neither the application nor this document uses them.
