"""Makes every Yappy-chan image the app and README use, on transparent backgrounds.

    python tools/make-art.py

Reads design/yappy-chan/ and writes:
  assets/icon.ico                  .exe and window icon (logo)
  assets/mascot/tray.png           tray icon source (logo)
  assets/mascot/<name>.png         single images: logo, base, oops, sleepy
  assets/mascot/<anim>-0..3.png    animation frames: talk, type, tidy, done, wave, dance
  docs/yappy-<anim>.gif            animated GIFs for the README
"""
import io
import os
import struct
from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, 'design', 'yappy-chan')
OUT = os.path.join(ROOT, 'assets', 'mascot')
DOCS = os.path.join(ROOT, 'docs')
HEIGHT = 180          # height of the in-app images
SINGLES = ['logo', 'base', 'oops', 'sleepy']
ANIMS = ['talk', 'type', 'tidy', 'done', 'wave', 'dance']
os.makedirs(OUT, exist_ok=True)
os.makedirs(DOCS, exist_ok=True)


def remove_white(img):
    """Flood-fills the white background from the edges, then fades the light fringe."""
    img = img.convert('RGBA')
    w, h = img.size
    seeds = [(x, 0) for x in range(0, w, 12)] + [(x, h - 1) for x in range(0, w, 12)] + \
            [(0, y) for y in range(0, h, 12)] + [(w - 1, y) for y in range(0, h, 12)]
    for s in seeds:
        r, g, b, a = img.getpixel(s)
        if a > 0 and min(r, g, b) > 225:
            ImageDraw.floodfill(img, s, (255, 255, 255, 0), thresh=42)
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


def bbox(img):
    return img.getchannel('A').point(lambda a: 255 if a > 30 else 0).getbbox()


def fit_height(img, height):
    w = max(1, round(img.width * height / img.height))
    return img.resize((w, height), Image.LANCZOS)


def save(img, path):
    # A 256-color palette with transparency keeps each frame small (the app ships them all).
    small = img.quantize(colors=256, method=Image.Quantize.FASTOCTREE, dither=Image.Dither.NONE)
    small.save(path, optimize=True)


# Single images
for name in SINGLES:
    path = os.path.join(SRC, name + '.png')
    if not os.path.exists(path):
        print('missing', name)
        continue
    img = remove_white(Image.open(path))
    img = img.crop(bbox(img))
    save(fit_height(img, HEIGHT), os.path.join(OUT, name + '.png'))

# Animation sheets: 2x2 grid. All four frames get the same crop so her movement stays.
frames_by_anim = {}
for anim in ANIMS:
    path = os.path.join(SRC, 'sheet-%s.png' % anim)
    if not os.path.exists(path):
        print('missing', anim)
        continue
    sheet = remove_white(Image.open(path))
    cw, ch = sheet.width // 2, sheet.height // 2
    cells = [sheet.crop((c * cw, r * ch, (c + 1) * cw, (r + 1) * ch)) for r in range(2) for c in range(2)]
    boxes = [bbox(c) for c in cells]
    union = (min(b[0] for b in boxes), min(b[1] for b in boxes), max(b[2] for b in boxes), max(b[3] for b in boxes))
    frames = [fit_height(c.crop(union), HEIGHT) for c in cells]
    for i, f in enumerate(frames):
        save(f, os.path.join(OUT, '%s-%d.png' % (anim, i)))
    frames_by_anim[anim] = frames


# README GIFs on a soft lavender card, so they look right on GitHub's light and dark themes.
def gif(frames, path, ms=140, size=320):
    out = []
    for f in frames:
        card = Image.new('RGBA', (size, size), (0, 0, 0, 0))
        mask = Image.new('L', (size, size), 0)
        ImageDraw.Draw(mask).rounded_rectangle((0, 0, size - 1, size - 1), radius=44, fill=255)
        bg = Image.new('RGBA', (size, size), (238, 235, 255, 255))
        card.paste(bg, (0, 0), mask)
        fig = f.copy()
        fig.thumbnail((int(size * 0.84), int(size * 0.84)), Image.LANCZOS)
        card.alpha_composite(fig, ((size - fig.width) // 2, (size - fig.height) // 2))
        out.append(card)
    # Transparent corners: pick a key color and make it the GIF's transparent index.
    key = (1, 2, 3)
    flat = []
    for c in out:
        base = Image.new('RGB', c.size, key)
        base.paste(c, (0, 0), c.getchannel('A').point(lambda a: 255 if a > 128 else 0))
        flat.append(base.quantize(colors=255, method=Image.MEDIANCUT, dither=Image.NONE))
    flat[0].save(path, save_all=True, append_images=flat[1:], duration=ms, loop=0, disposal=2, optimize=False)


timing = {'talk': 130, 'type': 120, 'tidy': 150, 'done': 160, 'wave': 150, 'dance': 170}
for anim, frames in frames_by_anim.items():
    order = frames + frames[2:0:-1] if anim in ('talk', 'wave', 'dance') else frames
    gif(order, os.path.join(DOCS, 'yappy-%s.gif' % anim), timing[anim])


# Icons from the logo.
def square(img, size):
    s = img.copy()
    s.thumbnail((size, size), Image.LANCZOS)
    c = Image.new('RGBA', (size, size), (0, 0, 0, 0))
    c.paste(s, ((size - s.width) // 2, (size - s.height) // 2), s)
    return c


def write_ico(path, images):
    """Standard Windows .ico: 32-bit BMP entries for small sizes, PNG for 256."""
    blobs = []
    for im in images:
        s = im.width
        if s >= 256:
            b = io.BytesIO()
            im.save(b, 'PNG')
            blobs.append(b.getvalue())
            continue
        header = struct.pack('<IiiHHIIiiII', 40, s, s * 2, 1, 32, 0, 0, 0, 0, 0, 0)
        px = im.load()
        rows = []
        for y in range(s - 1, -1, -1):
            row = bytearray()
            for x in range(s):
                r, g, b, a = px[x, y]
                row += bytes((b, g, r, a))
            rows.append(bytes(row))
        blobs.append(header + b''.join(rows) + b'\0' * (((s + 31) // 32) * 4) * s)
    out = struct.pack('<HHH', 0, 1, len(images))
    offset = 6 + 16 * len(images)
    for im, blob in zip(images, blobs):
        d = 0 if im.width >= 256 else im.width
        out += struct.pack('<BBBBHHII', d, d, 0, 0, 1, 32, len(blob), offset)
        offset += len(blob)
    with open(path, 'wb') as f:
        f.write(out + b''.join(blobs))


logo_src = os.path.join(SRC, 'logo.png')
if os.path.exists(logo_src):
    logo = remove_white(Image.open(logo_src))
    logo = logo.crop(bbox(logo))
    square(logo, 64).save(os.path.join(OUT, 'tray.png'))
    write_ico(os.path.join(ROOT, 'assets', 'icon.ico'), [square(logo, s) for s in [16, 20, 24, 32, 40, 48, 64, 256]])
    square(logo, 512).save(os.path.join(DOCS, 'yappy-logo.png'))
print('done')

