# subset_font.py —— 按工程实际用到的字符裁剪中文字体，控制 WebGL 包体
# 用法: python subset_font.py
import glob, os, subprocess, sys, shutil

ROOT = r'E:/UnityProject/Gunplay Siege Defense'
FONT = os.path.join(ROOT, 'Assets/Resources/Fonts/MainFont.ttf')
FULL_BACKUP = os.path.join(ROOT, 'Tools/MainFont_full.ttf')

chars = set()
# ASCII 可打印字符
chars.update(chr(c) for c in range(0x20, 0x7F))
# 常用中文标点与符号
chars.update('，。、；：？！“”‘’（）《》【】—…·×÷±％°★☆→←↑↓')
for pattern in ['Assets/**/*.cs', 'Assets/Resources/Config/*.json', 'Assets/**/*.asset']:
    for path in glob.glob(os.path.join(ROOT, pattern), recursive=True):
        try:
            with open(path, 'r', encoding='utf-8-sig', errors='ignore') as f:
                chars.update(f.read())
        except OSError:
            pass
chars = {c for c in chars if c.isprintable() or c == ' '}
text = ''.join(sorted(chars))

if not os.path.exists(FULL_BACKUP):
    os.makedirs(os.path.dirname(FULL_BACKUP), exist_ok=True)
    shutil.copy2(FONT, FULL_BACKUP)

out = os.path.join(ROOT, 'Assets/Resources/Fonts/MainFont_subset.ttf')
cmd = [sys.executable, '-m', 'fontTools.subset', FULL_BACKUP,
       '--text=' + text,
       '--output-file=' + out,
       '--layout-features=*',
       '--no-hinting',
       '--desubroutinize',
       '--drop-tables+=DSIG',
       '--name-IDs=*']
print('glyph text chars:', len(text))
subprocess.run(cmd, check=True)
print('subset size:', os.path.getsize(out), 'full size:', os.path.getsize(FULL_BACKUP))
os.replace(out, FONT)
print('replaced MainFont.ttf with subset:', os.path.getsize(FONT))
