# Changelog

## 1.0 — 2026-11-03

The first standalone RF-Lab: the HASE RF-MiniLab's panel as a Windows
application of its own, talking to the board directly.

- The window of the original RF-Lab 1.0 application: frequency and
  amplitude dials, AM, FM, SWEEP, ANALYZE, MEASURE, the Si5351 clock
  outputs, a chart of its own instead of the commercial chart control.
- Connection bar: port, connect/disconnect; connecting checks the board's
  identity and sends the window's setting.
- Stored settings in the original format: apply, save, save as, rename,
  delete, open the folder; keys the application does not use are kept.
- Trace tab: every MCNF frame with its time, and the firmware's debug text.
- Themes: Classic (the original), Studio dark, Studio light.
- The firmware's debug text before AM and FM answers is skipped, so AM and
  FM work with the firmware as it is.
- Manual (embedded PDF, F1), protocol description, Python client
  `tools/rflab.py`, release and flash scripts.
- Firmware: the RF-Lab R1.0 (MCNF) firmware, unchanged, with the libraries
  and pinned versions to rebuild it.
