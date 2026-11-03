"""A small Python client for the RF-Lab board (MCNF over USB serial).

Needs pyserial (pip install pyserial). As a library:

    from rflab import RfLab
    with RfLab("COM16") as board:
        print(board.node_type())          # 'AE.70.10.80'
        board.carrier(10_000_000, 20)     # 10 MHz, -20 dB
        print(board.sensor())             # (adc, millivolts, dB)

From the command line:

    python rflab.py COM16 info
    python rflab.py COM16 carrier 10000000 20
    python rflab.py COM16 am 10000000 20 1000 80
    python rflab.py COM16 fm 10000000 20 1000 5000
    python rflab.py COM16 sweep 10000000 30000000 2000 20 ramp
    python rflab.py COM16 clock 0 1000000
    python rflab.py COM16 led on
    python rflab.py COM16 sensor 10

Opening the port restarts the board (about 3 s); the board keeps the last
signal after the port closes. The protocol is described in docs/Protocol.md.
"""
import struct
import sys
import time

import serial

DEVICE = 0xA5           # device channel 1
NODE_ADMIN = 0xA4
SWEEP_MODES = {"bidirectional": 0, "ramp": 1, "singleramp": 2}


class RfLabError(Exception):
    pass


class RfLab:
    def __init__(self, port, settle=3.0):
        self.port = serial.Serial(port, 115200, timeout=2)
        self.port.dtr = True
        self.port.rts = True
        time.sleep(settle)                      # the Mega restarts when the port opens
        self.port.reset_input_buffer()
        self.port.write(b"\xA1")                # connectivity test
        if self.port.read(1) != b"\x21":
            raise RfLabError("no MCNF node answers on " + port)

    def close(self):
        self.port.close()

    def __enter__(self):
        return self

    def __exit__(self, *exc):
        self.close()

    def exchange(self, channel, function, params=b"", response_length=2):
        """Sends one frame and returns the response payload (error byte and checksum removed)."""
        body = bytes([function]) + params
        frame = bytes([channel, len(body) + 1, response_length, 0]) + body
        frame += bytes([(0xFF - sum(frame)) & 0xFF])
        self.port.write(frame)
        # the firmware prints debug text before its answer to AM and FM:
        # skip printable bytes until the error byte, which never is printable
        first = self.port.read(1)
        while first and (0x20 <= first[0] < 0x7F or first[0] in (0x0A, 0x0D)):
            first = self.port.read(1)
        rest = self.port.read(response_length - 1)
        response = first + rest
        if len(response) != response_length:
            raise RfLabError("no complete answer to function 0x%02X" % function)
        if response[0] != 0:
            raise RfLabError("the board refused function 0x%02X: error 0x%02X" % (function, response[0]))
        if (0xFF - sum(response[:-1])) & 0xFF != response[-1]:
            raise RfLabError("checksum error in the answer to function 0x%02X" % function)
        return response[1:-1]

    def node_type(self):
        return ".".join("%02X" % b for b in self.exchange(NODE_ADMIN, 220, response_length=6))

    def configuration(self):
        """(LED on, Si5351 fitted)"""
        data = self.exchange(DEVICE, 201, bytes([1, 0, 0, 0]), response_length=6)
        return bool(data[3] & 1), bool(data[3] & 2)

    def led(self, on=None):
        """Switches the LED (on=True/False) or reads it (on=None); returns its state."""
        function = 0x03 if on is None else (0x01 if on else 0x02)
        return self.exchange(DEVICE, function, response_length=3)[0] == 1

    def carrier(self, hertz, attenuation_db):
        self.exchange(DEVICE, 0x10, struct.pack(">IH", hertz, attenuation_db))

    def am(self, hertz, attenuation_db, modulation_hertz, depth_percent):
        self.exchange(DEVICE, 0x11, struct.pack(">IHII", hertz, attenuation_db, modulation_hertz, depth_percent))

    def fm(self, hertz, attenuation_db, modulation_hertz, deviation_hertz):
        self.exchange(DEVICE, 0x12, struct.pack(">IHII", hertz, attenuation_db, modulation_hertz, deviation_hertz))

    def sweep(self, start_hertz, stop_hertz, time_ms, attenuation_db, mode="bidirectional"):
        self.exchange(DEVICE, 0x13, struct.pack(">IIHHB", start_hertz, stop_hertz, time_ms, attenuation_db,
                                                SWEEP_MODES[mode.lower()]))

    def clock(self, output, hertz):
        """Sets Si5351 output 0..2; the wire unit is 0.01 Hz."""
        self.exchange(DEVICE, 0x40, struct.pack(">BQ", output, hertz * 100))

    def sensor(self):
        """(converter value, millivolts, AD8307 level in dB)"""
        adc = struct.unpack(">H", self.exchange(DEVICE, 0x20, response_length=4))[0]
        millivolts = adc * 2.5
        level = -70.0 if millivolts < 200 else -(2235.0 - millivolts) / 25.917 - 0.5
        return adc, millivolts, round(level, 1)


def main(argv):
    if len(argv) < 3:
        print(__doc__)
        return 2
    port, command, args = argv[1], argv[2].lower(), argv[3:]
    numbers = [int(a) for a in args if a.lstrip("-").isdigit()]
    with RfLab(port) as board:
        if command == "info":
            led, si5351 = board.configuration()
            print("node type %s, LED %s, Si5351 %s" % (board.node_type(), "on" if led else "off",
                                                       "fitted" if si5351 else "absent"))
        elif command == "carrier":
            board.carrier(*numbers)
        elif command == "am":
            board.am(*numbers)
        elif command == "fm":
            board.fm(*numbers)
        elif command == "sweep":
            board.sweep(*numbers[:4], mode=args[4] if len(args) > 4 else "bidirectional")
        elif command == "clock":
            board.clock(*numbers)
        elif command == "led":
            state = board.led(None if not args else args[0].lower() == "on")
            print("LED", "on" if state else "off")
        elif command == "sensor":
            for _ in range(numbers[0] if numbers else 1):
                print("adc %d  %.1f mV  %.1f dB" % board.sensor())
                time.sleep(0.2)
        else:
            print("unknown command", command)
            return 2
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
