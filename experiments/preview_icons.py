#!/usr/bin/env python3
"""Write enlarged previews of the icons (nearest neighbour) on light and dark backgrounds."""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
import make_icons  # noqa: E402


def compose(pixels, scale, background):
    out = []
    for row in pixels:
        line = []
        for r, g, b, a in row:
            mix = tuple((c * a + bg * (255 - a)) // 255 for c, bg in zip((r, g, b), background)) + (255,)
            line.extend([mix] * scale)
        out.extend([list(line) for _ in range(scale)])
    return out


def side_by_side(*images, gap=8, background=(128, 128, 128, 255)):
    height = max(len(i) for i in images)
    rows = []
    for y in range(height):
        row = []
        for n, img in enumerate(images):
            if n:
                row.extend([background] * gap)
            width = len(img[0])
            row.extend(img[y] if y < len(img) else [background] * width)
        rows.append(row)
    return rows


target = Path(sys.argv[1] if len(sys.argv) > 1 else "icon-preview.png")
light, dark = (0xF5, 0xF5, 0xF5), (0x3B, 0x44, 0x53)
images = []
for bg in (light, dark):
    images.append(compose(make_icons.draw_32(), 8, bg))
    images.append(compose(make_icons.draw_16(), 16, bg))
target.write_bytes(make_icons.png_bytes(side_by_side(*images)))
print("written", target)
