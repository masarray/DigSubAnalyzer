#!/usr/bin/env python3
"""Generate deterministic Quick Start and User Manual PDFs for release packages."""

from __future__ import annotations

import argparse
import hashlib
from pathlib import Path

from reportlab.lib import colors
from reportlab.lib.enums import TA_CENTER
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle, getSampleStyleSheet
from reportlab.lib.units import mm
from reportlab.pdfbase.pdfmetrics import stringWidth
from reportlab.platypus import PageBreak, Paragraph, SimpleDocTemplate, Spacer, Table, TableStyle

ROOT = Path(__file__).resolve().parents[1]
VERSION = "1.4.0-beta.2"
LICENSE = "GPL-3.0-or-later"
BOUNDARY = "85d43a0fe58a5888a9e8008c168ab76d2333ea87"


def styles():
    base = getSampleStyleSheet()
    return {
        "title": ParagraphStyle("Title", parent=base["Title"], fontName="Helvetica-Bold", fontSize=25, leading=30, textColor=colors.HexColor("#0B365A"), alignment=TA_CENTER, spaceAfter=12),
        "subtitle": ParagraphStyle("Subtitle", parent=base["Normal"], fontName="Helvetica", fontSize=11, leading=16, textColor=colors.HexColor("#48657B"), alignment=TA_CENTER, spaceAfter=18),
        "h1": ParagraphStyle("H1", parent=base["Heading1"], fontName="Helvetica-Bold", fontSize=17, leading=21, textColor=colors.HexColor("#0B568A"), spaceBefore=10, spaceAfter=8),
        "h2": ParagraphStyle("H2", parent=base["Heading2"], fontName="Helvetica-Bold", fontSize=13, leading=17, textColor=colors.HexColor("#123F5C"), spaceBefore=8, spaceAfter=5),
        "body": ParagraphStyle("Body", parent=base["BodyText"], fontName="Helvetica", fontSize=9.6, leading=14, textColor=colors.HexColor("#213746"), spaceAfter=6),
        "bullet": ParagraphStyle("Bullet", parent=base["BodyText"], fontName="Helvetica", fontSize=9.4, leading=13.5, leftIndent=12, firstLineIndent=-7, bulletIndent=4, textColor=colors.HexColor("#213746"), spaceAfter=4),
        "note": ParagraphStyle("Note", parent=base["BodyText"], fontName="Helvetica", fontSize=9, leading=13, borderColor=colors.HexColor("#77B9E6"), borderWidth=0.7, borderPadding=8, backColor=colors.HexColor("#EDF7FD"), textColor=colors.HexColor("#173F59"), spaceBefore=6, spaceAfter=10),
        "small": ParagraphStyle("Small", parent=base["BodyText"], fontName="Helvetica", fontSize=7.5, leading=10, textColor=colors.HexColor("#5F7483")),
    }


def footer(canvas, doc):
    canvas.saveState()
    canvas.setStrokeColor(colors.HexColor("#D5E2EB"))
    canvas.line(18 * mm, 14 * mm, 192 * mm, 14 * mm)
    canvas.setFillColor(colors.HexColor("#60798A"))
    canvas.setFont("Helvetica", 7.5)
    canvas.drawString(18 * mm, 9 * mm, f"Process Bus Insight {VERSION} · {LICENSE}")
    text = f"Page {doc.page}"
    canvas.drawRightString(192 * mm, 9 * mm, text)
    canvas.setTitle(doc.title)
    canvas.setAuthor("Ari Sulistiono and Process Bus Insight Contributors")
    canvas.setSubject("Receive-only IEC 61850 Process Bus engineering documentation")
    canvas.setCreator("Process Bus Insight deterministic PDF generator")
    canvas.saveState()
    canvas.restoreState()
    canvas.restoreState()


def cover(st, title, subtitle):
    return [
        Spacer(1, 28 * mm),
        Paragraph("PROCESS BUS INSIGHT", st["small"]),
        Spacer(1, 4 * mm),
        Paragraph(title, st["title"]),
        Paragraph(subtitle, st["subtitle"]),
        Table(
            [["Version", VERSION], ["Community license", LICENSE], ["Platform", "Windows 10/11 x64"], ["Product boundary", "Receive-only; no SV/GOOSE publishing or IEC 61850 control commands"]],
            colWidths=[46 * mm, 113 * mm],
            style=TableStyle([
                ("BACKGROUND", (0, 0), (0, -1), colors.HexColor("#EAF4FA")),
                ("TEXTCOLOR", (0, 0), (0, -1), colors.HexColor("#164D70")),
                ("FONTNAME", (0, 0), (0, -1), "Helvetica-Bold"),
                ("FONTNAME", (1, 0), (1, -1), "Helvetica"),
                ("FONTSIZE", (0, 0), (-1, -1), 9),
                ("GRID", (0, 0), (-1, -1), 0.4, colors.HexColor("#BFD3DF")),
                ("VALIGN", (0, 0), (-1, -1), "TOP"),
                ("LEFTPADDING", (0, 0), (-1, -1), 7),
                ("RIGHTPADDING", (0, 0), (-1, -1), 7),
                ("TOPPADDING", (0, 0), (-1, -1), 7),
                ("BOTTOMPADDING", (0, 0), (-1, -1), 7),
            ]),
        ),
        Spacer(1, 12 * mm),
        Paragraph("This document describes engineering use and evidence boundaries. It is not a certificate of IEC 61850 conformance, calibrated timing, functional safety, cybersecurity approval, equipment isolation, or switching authority.", st["note"]),
        PageBreak(),
    ]


def add_bullets(story, st, items):
    for item in items:
        story.append(Paragraph(f"• {item}", st["bullet"]))


def build_quick_start(path: Path):
    st = styles()
    story = cover(st, "Quick Start", "Safe first-run workflow for live capture and sanitized PCAP replay")
    story += [Paragraph("1. Prepare the engineering workstation", st["h1"])]
    add_bullets(story, st, [
        "Use Windows 10 or Windows 11 x64 and install Npcap separately when live raw Ethernet capture is required.",
        "Use an authorized TAP, mirror port, or isolated engineering test switch. Do not insert an unverified workstation into a protection-critical path.",
        "Avoid Wi-Fi, VPN, virtual, loopback, and unverified USB adapters for serious arrival-timing interpretation.",
    ])
    story += [Paragraph("2. Verify and run the package", st["h1"])]
    add_bullets(story, st, [
        "Verify the ZIP against SHA256SUMS.txt and review release-manifest.json, SOURCE.md, and sbom.cdx.json.",
        "Extract the package to a local folder and run ProcessBusInsight.exe.",
        "Open About and confirm the displayed version and build commit match the package evidence.",
    ])
    story += [Paragraph("3. Capture or replay", st["h1"])]
    add_bullets(story, st, [
        "Select the intended physical adapter before starting capture.",
        "Confirm SV, GOOSE, and PTP evidence appears only from the expected observation point.",
        "Use sanitized classic Ethernet PCAP replay for reproducible offline investigation. PCAPNG is not currently claimed.",
    ])
    story += [Paragraph("4. Record scoped evidence", st["h1"])]
    add_bullets(story, st, [
        "Record adapter, capture path, APPID, VLAN, MAC, svID or GOOSE identity, timestamp source, SCL expectation, and observed field.",
        "Keep configured expectation, observed traffic, software interpretation, and external-device response separate.",
        "Treat ordinary Windows/Npcap timing as screening evidence unless independently validated with suitable hardware and procedures.",
    ])
    story += [Paragraph("Licensing", st["h1"]), Paragraph(f"Post-transition source and packages are {LICENSE}. Historical Apache-2.0 rights remain attached only to revisions at or before commit {BOUNDARY}. COMMERCIAL-LICENSE.md is an invitation to negotiate and grants no additional rights by itself.", st["body"])]
    doc = SimpleDocTemplate(str(path), pagesize=A4, rightMargin=18 * mm, leftMargin=18 * mm, topMargin=18 * mm, bottomMargin=20 * mm, title="Process Bus Insight Quick Start")
    doc.build(story, onFirstPage=footer, onLaterPages=footer)


def build_manual(path: Path):
    st = styles()
    story = cover(st, "User Manual", "IEC 61850 SV, GOOSE, PTP, SCL, and PCAP evidence workflow")
    sections = [
        ("Product boundary", ["Process Bus Insight is receive-only and raw-passive.", "It does not publish Sampled Values or GOOSE and does not send IEC 61850 control commands.", "It complements—not replaces—approved test equipment, project procedures, and engineering authority."]),
        ("Sampled Values workspace", ["Use stream identity, APPID, svID, VLAN, source MAC, confRev, counters, waveform, RMS, phasor, and sequence diagnostics for the selected stream.", "The selected stream owns the displayed waveform, metering, phasor, and details. Cross-stream combinations are not valid evidence.", "Missing, duplicate, gap, or timing indications are investigation leads whose confidence depends on the capture path."]),
        ("GOOSE workspace", ["Inspect publisher identity, stNum, sqNum, typed dataset values, and event history.", "Observed frames do not prove that an external IED accepted, trusted, or acted on the traffic."]),
        ("PTP timing context", ["Review visible transport, domain, message type, grandmaster context, and freshness where decoded.", "The application does not discipline clocks or certify time synchronization."]),
        ("SCL expected-vs-observed", ["Load SCD, ICD, or CID files and compare expected APPID, destination MAC, VLAN, DataSet, svID, confRev, and related identity evidence.", "A match describes evidence at the selected observation point; it is not formal conformance certification."]),
        ("PCAP replay", ["Classic Ethernet PCAP can be replayed through the same decoder/analyzer entry point used by live capture.", "Replay preserves recorded evidence but cannot recreate switch loading, NIC buffering, unrecorded packet loss, or hardware timestamp behavior."]),
        ("Security and data handling", ["Treat captures and engineering files as potentially sensitive.", "Sanitize customer, employer, station, device, credential, MAC/IP, and project identifiers before sharing.", "Report parser or package-integrity vulnerabilities through a private GitHub security advisory."]),
        ("Licensing and source", [f"Post-transition community revisions and packages use {LICENSE}.", f"The historical Apache-2.0 boundary is commit {BOUNDARY}.", "SOURCE.md identifies the exact source corresponding to a packaged binary. The commercial notice is not itself a license."]),
    ]
    for heading, bullets in sections:
        story.append(Paragraph(heading, st["h1"]))
        add_bullets(story, st, bullets)
    story += [Paragraph("Evidence wording", st["h1"]), Paragraph("Use terms such as configured, observed, decoded, calculated, replayed, screened, provisional, unsupported, or not yet verified. Do not claim certification, calibration, deterministic timing, functional safety, cybersecurity approval, universal interoperability, switching authority, or IED acceptance without separate competent evidence.", st["note"])]
    doc = SimpleDocTemplate(str(path), pagesize=A4, rightMargin=18 * mm, leftMargin=18 * mm, topMargin=18 * mm, bottomMargin=20 * mm, title="Process Bus Insight User Manual")
    doc.build(story, onFirstPage=footer, onLaterPages=footer)


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true", help="Regenerate in memory location and compare SHA256 with tracked PDFs")
    args = parser.parse_args()
    docs = ROOT / "docs"
    docs.mkdir(exist_ok=True)
    quick = docs / "QUICK_START.pdf"
    manual = docs / "USER_MANUAL.pdf"
    if args.check:
        tmp = ROOT / "artifacts" / "pdf-check"
        tmp.mkdir(parents=True, exist_ok=True)
        tq, tm = tmp / quick.name, tmp / manual.name
        build_quick_start(tq)
        build_manual(tm)
        mismatches = [p.name for p, generated in ((quick, tq), (manual, tm)) if not p.exists() or digest(p) != digest(generated)]
        if mismatches:
            raise SystemExit("Generated PDFs are stale: " + ", ".join(mismatches))
        print("Release PDFs are deterministic and current.")
        return
    build_quick_start(quick)
    build_manual(manual)
    print(f"Generated {quick} ({digest(quick)})")
    print(f"Generated {manual} ({digest(manual)})")


if __name__ == "__main__":
    main()
