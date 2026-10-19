#!/usr/bin/env python3
"""Build offline PDF manuals from the same Markdown shipped with the app.

Requires reportlab 4.4.x at packaging time; no runtime dependency or network calls.
"""
import argparse
import hashlib
import html
import json
import re
from pathlib import Path

from reportlab.lib import colors
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle
from reportlab.platypus import BaseDocTemplate, Frame, PageTemplate, Paragraph, Preformatted

NAVY = colors.HexColor("#122238")
BLUE = colors.HexColor("#186DC0")
INK = colors.HexColor("#1B2836")


def inline(source):
    source = html.escape(source, quote=True)
    source = re.sub(r"\[([^\]]+)\]\((https?://[^)]+)\)",
                    lambda m: f'<link href="{m.group(2)}" color="#186DC0">{m.group(1)}</link>', source)
    source = re.sub(r"\*\*(.+?)\*\*", r"<b>\1</b>", source)
    source = re.sub(r"\x60([^\x60]+)\x60", r'<font face="Courier" size="9">\1</font>', source)
    return source


def paragraphs(markdown, styles):
    story, pending, code = [], [], []
    in_code = False

    def flush():
        if pending:
            story.append(Paragraph(inline(" ".join(pending)), styles["body"]))
            pending.clear()

    for line in markdown.splitlines():
        value = line.strip()
        if value.startswith(chr(96) * 3):
            if in_code:
                story.append(Preformatted("\n".join(code), styles["code"],
                                          maxLineLength=85, splitChars=" /\\"))
                code.clear()
            else:
                flush()
            in_code = not in_code
            continue
        if in_code:
            code.append(line.rstrip())
        elif not value:
            flush()
        elif re.match(r"^#{1,3}\s", value):
            flush()
            depth = len(value.split(" ", 1)[0])
            story.append(Paragraph(inline(value[depth:].strip()), styles[f"h{depth}"]))
        elif re.match(r"^(?:[-*]\s|\d+\.\s)", value):
            flush()
            ordered = re.match(r"^(\d+)\.\s+(.*)$", value)
            number = f"{ordered.group(1)}." if ordered else "•"
            entry = ordered.group(2) if ordered else re.sub(r"^[-*]\s+", "", value)
            story.append(Paragraph(f'<font color="#186DC0">{number}</font>  {inline(entry)}',
                                   styles["list"]))
        else:
            pending.append(value)
    flush()
    if in_code:
        raise ValueError("Unclosed Markdown code block")
    return story


def render(source, destination):
    text = source.read_text(encoding="utf-8")
    if "Npcap" not in text or "Inno Setup" not in text:
        raise ValueError(f"Manual is missing current Inno Setup / Npcap guidance: {source}")

    style = {
        "h1": ParagraphStyle("h1", fontName="Helvetica-Bold", fontSize=18, leading=22,
                             textColor=NAVY, spaceBefore=8, spaceAfter=16),
        "h2": ParagraphStyle("h2", fontName="Helvetica-Bold", fontSize=13, leading=18,
                             textColor=NAVY, spaceBefore=13, spaceAfter=8, keepWithNext=True),
        "h3": ParagraphStyle("h3", fontName="Helvetica-Bold", fontSize=11, leading=15,
                             textColor=NAVY, spaceBefore=10, spaceAfter=6, keepWithNext=True),
        "body": ParagraphStyle("body", fontName="Helvetica", fontSize=10.3, leading=15.5,
                               textColor=INK, spaceAfter=8),
        "list": ParagraphStyle("list", fontName="Helvetica", fontSize=10.2, leading=15,
                               leftIndent=12, firstLineIndent=-10, textColor=INK, spaceAfter=5),
        "code": ParagraphStyle("code", fontName="Courier", fontSize=8.6, leading=12,
                               leftIndent=8, rightIndent=8, backColor=colors.HexColor("#F0F5F9"),
                               borderPadding=7, textColor=INK, spaceAfter=10),
    }

    page_width, page_height = A4
    destination.parent.mkdir(parents=True, exist_ok=True)
    doc = BaseDocTemplate(str(destination), pagesize=A4, leftMargin=48, rightMargin=48,
                          topMargin=66, bottomMargin=48,
                          title=f"Process Bus Insight - {source.stem.replace('_', ' ').title()}",
                          author="Process Bus Insight Contributors")
    frame = Frame(48, 48, page_width - 96, page_height - 114,
                  leftPadding=0, rightPadding=0, topPadding=0, bottomPadding=0)

    def decorate(canvas, document):
        canvas.saveState()
        canvas.setFillColor(NAVY)
        canvas.rect(0, page_height - 39, page_width, 39, fill=1, stroke=0)
        canvas.setFillColor(colors.white)
        canvas.setFont("Helvetica-Bold", 10)
        canvas.drawString(48, page_height - 25, "PROCESS BUS INSIGHT")
        canvas.setFont("Helvetica", 8)
        canvas.drawRightString(page_width - 48, page_height - 25, "OFFLINE OPERATOR GUIDE")
        canvas.setStrokeColor(BLUE)
        canvas.setLineWidth(2)
        canvas.line(48, 38, page_width - 48, 38)
        canvas.setFont("Helvetica", 8)
        canvas.setFillColor(colors.HexColor("#5C6D7D"))
        canvas.drawString(48, 27, "Receive-only  |  Windows / Npcap timing is screening evidence")
        canvas.drawRightString(page_width - 48, 27, f"Page {document.page}")
        canvas.restoreState()

    doc.addPageTemplates([PageTemplate(id="manual", frames=frame, onPage=decorate)])
    doc.build(paragraphs(text, style))
    if destination.stat().st_size < 1400:
        raise ValueError(f"Generated PDF too small: {destination}")
    return hashlib.sha256(text.encode("utf-8")).hexdigest()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo-root", default=".")
    parser.add_argument("--output-dir", default="artifacts/generated-docs")
    args = parser.parse_args()
    root = Path(args.repo_root).resolve()
    output = (root / args.output_dir).resolve()
    manifest = {"format": "ProcessBusInsight-offline-manuals-v1", "files": []}
    for stem, name in [("QUICK_START", "Quick Start.pdf"), ("USER_MANUAL", "User Manual.pdf")]:
        source, pdf = root / "docs" / f"{stem}.md", output / name
        source_hash = render(source, pdf)
        manifest["files"].append({
            "source": source.name, "sourceSha256": source_hash,
            "pdf": name, "pdfSha256": hashlib.sha256(pdf.read_bytes()).hexdigest()
        })
        print(f"{source.name} -> {pdf.name} ({pdf.stat().st_size} bytes)")
    (output / "manual-manifest.json").write_text(
        json.dumps(manifest, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
