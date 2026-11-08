# Makes the Microsoft Store screenshots (1920x1080) from the app screenshots in docs/.
# Run: python tools/make-store-shots.py   (needs Pillow)
import os
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DOCS = os.path.join(ROOT, 'docs')
OUT = os.path.join(ROOT, 'packaging', 'store')
W, H = 1920, 1080
TOP, BOTTOM = (139, 127, 255), (75, 59, 214)
FONTS = os.path.join(os.environ.get('WINDIR', r'C:\Windows'), 'Fonts')


def font(name, size):
    return ImageFont.truetype(os.path.join(FONTS, name), size)


def background():
    bg = Image.new('RGB', (W, H))
    d = ImageDraw.Draw(bg)
    for y in range(H):
        t = y / (H - 1)
        d.line([(0, y), (W, y)], fill=tuple(int(a + (b - a) * t) for a, b in zip(TOP, BOTTOM)))
    return bg.convert('RGBA')


def paste_card(bg, img, x, y, radius=22):
    img = img.convert('RGBA')
    mask = Image.new('L', img.size, 0)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, img.size[0] - 1, img.size[1] - 1], radius, fill=255)
    shadow = Image.new('RGBA', (img.size[0] + 120, img.size[1] + 120), (0, 0, 0, 0))
    ImageDraw.Draw(shadow).rounded_rectangle([60, 70, 60 + img.size[0], 70 + img.size[1]], radius, fill=(20, 10, 70, 110))
    shadow = shadow.filter(ImageFilter.GaussianBlur(28))
    bg.alpha_composite(shadow, (x - 60, y - 60))
    bg.paste(img, (x, y), mask)


def text_block(bg, x, title, body):
    d = ImageDraw.Draw(bg)
    tf, bf = font('segoeuib.ttf', 76), font('segoeui.ttf', 38)
    y = H // 2 - 150
    for line in title.split('\n'):
        d.text((x, y), line, font=tf, fill='white')
        y += 92
    y += 26
    for line in body.split('\n'):
        d.text((x, y), line, font=bf, fill=(236, 233, 255))
        y += 54


def trim_black(im):
    # The window captures in docs/ have a thin pure black margin around them.
    im = im.convert('RGBA')
    dark = Image.eval(im.convert('L'), lambda v: 255 if v > 2 else 0)
    box = dark.getbbox()
    return im.crop(box) if box else im


def scaled(name, height):
    im = trim_black(Image.open(os.path.join(DOCS, name)))
    return im.resize((round(im.size[0] * height / im.size[1]), height), Image.LANCZOS)


def window_shot(file, name, title, body, height=880):
    bg = background()
    win = scaled(name, height)
    paste_card(bg, win, W - win.size[0] - 170, (H - height) // 2)
    text_block(bg, 150, title, body)
    bg.convert('RGB').save(os.path.join(OUT, file))


def bubble_shot(file):
    bg = background()
    y = 250
    for name in ['bubble-listening.png', 'bubble-working.png', 'bubble-done.png']:
        b = scaled(name, 160)
        bg.alpha_composite(b.convert('RGBA'), (W - b.size[0] - 220, y))
        y += 210
    text_block(bg, 150, 'A tiny bubble\nwhile you talk', 'It shows your voice level and a timer,\nnever steals focus, and clicks\ngo right through it.')
    bg.convert('RGB').save(os.path.join(OUT, file))


def logo_art(file, w, h):
    # Store logos: 9:16 poster art (main logo on Windows) and 1:1 box art.
    global W, H
    saved = (W, H)
    W, H = w, h
    bg = background()
    W, H = saved
    logo = Image.open(os.path.join(DOCS, 'yappy-logo.png')).convert('RGBA')
    size = int(min(w, h) * 0.62)
    logo = logo.resize((size, size), Image.LANCZOS)
    top = (h - size) // 2 - int(h * 0.06)
    bg.alpha_composite(logo, ((w - size) // 2, top))
    d = ImageDraw.Draw(bg)
    tf = font('segoeuib.ttf', int(min(w, h) * 0.13))
    text = 'Yappity'
    tw = d.textlength(text, font=tf)
    d.text(((w - tw) / 2, top + size + int(h * 0.03)), text, font=tf, fill='white')
    bg.convert('RGB').save(os.path.join(OUT, file))


os.makedirs(OUT, exist_ok=True)
logo_art('poster-9x16.png', 720, 1080)
logo_art('boxart-1x1.png', 1080, 1080)
window_shot('screenshot-1.png', 'welcome-light.png', 'Free voice typing\nfor Windows', 'Tap Right Ctrl, talk, tap it again.\nYour words land wherever\nyour cursor is.')
bubble_shot('screenshot-2.png')
window_shot('screenshot-3.png', 'history-dark.png', 'Never lose a yap', 'Every recording is saved. Play it,\nsend it again, or drag it into\nany app or folder.')
window_shot('screenshot-4.png', 'settings-dark.png', 'Your shortcut,\nyour style', 'Raw, clean up, format or organize.\nEnglish, Tagalog or Taglish.\nRuns on Groq\'s free plan.', height=940)
print('Wrote', sorted(os.listdir(OUT)))
