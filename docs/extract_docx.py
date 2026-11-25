# -*- coding: utf-8 -*-
"""Extract DOCX into readable markdown-like text preserving headings, lists and tables."""
import sys
from docx import Document
from docx.table import Table
from docx.text.paragraph import Paragraph

src = sys.argv[1]
dst = sys.argv[2]
doc = Document(src)
body = doc.element.body
lines = []
for child in body.iterchildren():
    tag = child.tag.split('}')[-1]
    if tag == 'p':
        p = Paragraph(child, doc)
        text = p.text.strip()
        if not text:
            continue
        style = (p.style.name if p.style is not None else '') or ''
        if style.startswith('Heading') or style.startswith('标题'):
            level = ''.join(ch for ch in style if ch.isdigit()) or '2'
            lines.append('#' * min(int(level), 6) + ' ' + text)
        elif style.startswith('List') or style.startswith('列表'):
            lines.append('- ' + text)
        else:
            lines.append(text)
    elif tag == 'tbl':
        t = Table(child, doc)
        for row in t.rows:
            cells = [c.text.replace('\n', ' ').strip() for c in row.cells]
            lines.append('| ' + ' | '.join(cells) + ' |')
        lines.append('')
with open(dst, 'w', encoding='utf-8') as f:
    f.write('\n'.join(lines))
print('LINES', len(lines), 'CHARS', sum(len(l) for l in lines))
