"""Draws the RF-Lab application icon (app/src/RfLab/RfLab.ico): a sine
burst on a dark tile with the frequency dial's red. Run with any Python
that has Pillow: python tools/Make-Icon.py"""
import math
from pathlib import Path
from PIL import Image, ImageDraw

SIZE = 256
SCALE = 4  # draw large, shrink for smooth edges
big = SIZE * SCALE
image = Image.new("RGBA", (big, big), (0, 0, 0, 0))
draw = ImageDraw.Draw(image)
draw.rounded_rectangle([0, 0, big - 1, big - 1], radius=48 * SCALE, fill=(17, 21, 27, 255))
draw.rounded_rectangle([14 * SCALE, 14 * SCALE, big - 14 * SCALE, big - 14 * SCALE],
                       radius=38 * SCALE, outline=(128, 0, 0, 255), width=10 * SCALE)

points = []
for i in range(0, 401):
    x = 40 + 176 * i / 400
    envelope = math.sin(math.pi * i / 400)
    y = 128 - 62 * envelope * math.sin(2 * math.pi * 5.0 * i / 400)
    points.append((x * SCALE, y * SCALE))
draw.line(points, fill=(47, 196, 178, 255), width=12 * SCALE, joint="curve")

icon = image.resize((SIZE, SIZE), Image.LANCZOS)
target = Path(__file__).resolve().parent.parent / "app" / "src" / "RfLab" / "RfLab.ico"
icon.save(target, sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
print(target)
