"""Makes every Chatter-Chan image the app uses, all full body on transparent backgrounds.

    python tools/make-art.py

Writes:
  assets/icon.ico                 .exe and window icon (hello pose)
  assets/mascot/tray.png          tray icon source (hello pose)
  assets/mascot/<pose>.png        full body, 220 px tall (bubble, header, empty history, welcome)
  assets/mascot/talk-0..3.png     talking frames from the sprite sheet (bubble while recording)
"""
import os
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, 'design', 'mascots', 'chatter-chan')
OUT = os.path.join(ROOT, 'assets', 'mascot')
os.makedirs(OUT, exist_ok=True)
HEIGHT = 220


def trim(img):
    box = img.getchannel('A').point(lambda a: 255 if a > 30 else 0).getbbox()
    return img.crop(box)


def scaled(img, height):
    w = max(1, round(img.width * height / img.height))
    return img.resize((w, height), Image.LANCZOS)


def remove_white(img):
    """Flood-fills the white background from the edges, then softens the light fringe."""
    img = img.convert('RGBA')
    w, h = img.size
    seeds = [(x, 0) for x in range(0, w, 16)] + [(x, h - 1) for x in range(0, w, 16)] + \
            [(0, y) for y in range(0, h, 16)] + [(w - 1, y) for y in range(0, h, 16)]
    for s in seeds:
        r, g, b, a = img.getpixel(s)
        if a > 0 and min(r, g, b) > 225:
            ImageDraw.floodfill(img, s, (255, 255, 255, 0), thresh=40)
    # Pixels right next to the cut-out that are still near-white become see-through.
    alpha = img.getchannel('A')
    hole = alpha.point(lambda a: 255 if a == 0 else 0).filter(ImageFilter.MaxFilter(5))
    px, hp = img.load(), hole.load()
    for y in range(h):
        for x in range(w):
            if hp[x, y]:
                r, g, b, a = px[x, y]
                if a and min(r, g, b) > 205:
                    px[x, y] = (r, g, b, int(a * (255 - min(r, g, b)) / 50))
    return img


def feet_center(img):
    """Horizontal center of the bottom 6% of the figure, so frames line up by her feet."""
    a = img.getchannel('A')
    band = a.crop((0, int(img.height * 0.94), img.width, img.height)).point(lambda v: 255 if v > 30 else 0)
    box = band.getbbox()
    return (box[0] + box[2]) / 2 if box else img.width / 2


# Full-body poses
for name in sorted(os.listdir(SRC)):
    if not name.startswith('pose-') or not name.endswith('.png'):
        continue
    pose = name.split('-', 2)[2][:-4]
    img = trim(Image.open(os.path.join(SRC, name)).convert('RGBA'))
    scaled(img, HEIGHT).save(os.path.join(OUT, pose + '.png'), optimize=True)

# Talking frames: one shared scale, aligned by the feet, same canvas size.
sheet = remove_white(Image.open(os.path.join(SRC, 'gif-test', 'yapping-sheet.png')))
n = 4
tiles = [trim(sheet.crop((i * sheet.width // n, 0, (i + 1) * sheet.width // n, sheet.height))) for i in range(n)]
tallest = max(t.height for t in tiles)
scale = HEIGHT / tallest
tiles = [t.resize((round(t.width * scale), round(t.height * scale)), Image.LANCZOS) for t in tiles]
centers = [feet_center(t) for t in tiles]
left = max(c for c in centers)
right = max(t.width - c for t, c in zip(tiles, centers))
canvas_w = int(left + right) + 2
for i, (t, c) in enumerate(zip(tiles, centers)):
    frame = Image.new('RGBA', (canvas_w, HEIGHT), (0, 0, 0, 0))
    frame.paste(t, (int(left - c), HEIGHT - t.height), t)
    frame.save(os.path.join(OUT, 'talk-%d.png' % i), optimize=True)

# Icons: the hello pose, full body, centered in a square.
hello = trim(Image.open(os.path.join(SRC, 'pose-1-hello.png')).convert('RGBA'))


def square(img, size):
    s = scaled(img, size)
    if s.width > size:
        s = s.resize((size, max(1, round(s.height * size / s.width))), Image.LANCZOS)
    c = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    c.paste(s, ((size - s.width) // 2, size - s.height), s)
    return c


def write_ico(path, images):
    """Standard Windows .ico: 32-bit BMP entries for small sizes, PNG for 256."""
    import io, struct
    blobs = []
    for im in images:
        s = im.width
        if s >= 256:
            b = io.BytesIO()
            im.save(b, 'PNG')
            blobs.append(b.getvalue())
            continue
        header = struct.pack('<IiiHHIIiiII', 40, s, s * 2, 1, 32, 0, 0, 0, 0, 0, 0)
        rows = []
        px = im.load()
        for y in range(s - 1, -1, -1):
            row = bytearray()
            for x in range(s):
                r, g, b, a = px[x, y]
                row += bytes((b, g, r, a))
            rows.append(bytes(row))
        mask_row = ((s + 31) // 32) * 4
        blobs.append(header + b''.join(rows) + b'\0' * mask_row * s)
    out = struct.pack('<HHH', 0, 1, len(images))
    offset = 6 + 16 * len(images)
    for im, blob in zip(images, blobs):
        d = 0 if im.width >= 256 else im.width
        out += struct.pack('<BBBBHHII', d, d, 0, 0, 1, 32, len(blob), offset)
        offset += len(blob)
    with open(path, 'wb') as f:
        f.write(out + b''.join(blobs))


square(hello, 64).save(os.path.join(OUT, 'tray.png'))
sizes = [16, 20, 24, 32, 40, 48, 64, 256]
write_ico(os.path.join(ROOT, 'assets', 'icon.ico'), [square(hello, s) for s in sizes])
square(hello, 256).save(os.path.join(SRC, 'icon-preview.png'))
print('done')
