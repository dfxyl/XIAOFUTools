from __future__ import annotations

from pathlib import Path
from typing import Iterable


ROOT = Path(__file__).resolve().parent.parent
ICON_DIR = ROOT / "IconTools" / "Icons"
CATALOG_FILE = ROOT / "IconTools" / "IconCatalog.html"

STROKE = "#5C7B92"
BLUE = "#4E86CC"
GREEN = "#4C9A61"
AMBER = "#C27A1F"
TEAL = "#2E8E85"
RED = "#C85E58"
VIOLET = "#715CB6"
GRAY = "#7E93A7"

COMMON_DEFS = """
  <defs>
    <linearGradient id="glassFill" x1="3" y1="2" x2="29" y2="30" gradientUnits="userSpaceOnUse">
      <stop offset="0" stop-color="#FFFFFF" stop-opacity=".92"/>
      <stop offset=".58" stop-color="#F8FBFF" stop-opacity=".78"/>
      <stop offset="1" stop-color="#EDF4FB" stop-opacity=".68"/>
    </linearGradient>
    <linearGradient id="glassStroke" x1="2" y1="2" x2="30" y2="30" gradientUnits="userSpaceOnUse">
      <stop offset="0" stop-color="#FFFFFF" stop-opacity=".95"/>
      <stop offset="1" stop-color="#C3D2E3" stop-opacity=".74"/>
    </linearGradient>
    <linearGradient id="blueGrad" x1="0" y1="6" x2="0" y2="26" gradientUnits="userSpaceOnUse">
      <stop offset="0" stop-color="#D7EEFF"/>
      <stop offset="1" stop-color="#8BC1F4"/>
    </linearGradient>
    <linearGradient id="greenGrad" x1="0" y1="6" x2="0" y2="26" gradientUnits="userSpaceOnUse">
      <stop offset="0" stop-color="#DCF3D3"/>
      <stop offset="1" stop-color="#8ECF7A"/>
    </linearGradient>
    <linearGradient id="amberGrad" x1="0" y1="6" x2="0" y2="26" gradientUnits="userSpaceOnUse">
      <stop offset="0" stop-color="#FFE5B7"/>
      <stop offset="1" stop-color="#F2B44C"/>
    </linearGradient>
    <linearGradient id="tealGrad" x1="0" y1="6" x2="0" y2="26" gradientUnits="userSpaceOnUse">
      <stop offset="0" stop-color="#D3F4F0"/>
      <stop offset="1" stop-color="#76D0C1"/>
    </linearGradient>
    <linearGradient id="redGrad" x1="0" y1="6" x2="0" y2="26" gradientUnits="userSpaceOnUse">
      <stop offset="0" stop-color="#FFD6D6"/>
      <stop offset="1" stop-color="#EE8A84"/>
    </linearGradient>
    <linearGradient id="violetGrad" x1="0" y1="6" x2="0" y2="26" gradientUnits="userSpaceOnUse">
      <stop offset="0" stop-color="#E1DAFF"/>
      <stop offset="1" stop-color="#AB97EC"/>
    </linearGradient>
    <linearGradient id="grayGrad" x1="0" y1="6" x2="0" y2="26" gradientUnits="userSpaceOnUse">
      <stop offset="0" stop-color="#F3F6FA"/>
      <stop offset="1" stop-color="#D3DCE6"/>
    </linearGradient>
    <linearGradient id="skyGrad" x1="0" y1="0" x2="0" y2="12" gradientUnits="userSpaceOnUse">
      <stop offset="0" stop-color="#DFF2FF"/>
      <stop offset="1" stop-color="#A5D1F8"/>
    </linearGradient>
    <linearGradient id="groundGrad" x1="0" y1="12" x2="0" y2="24" gradientUnits="userSpaceOnUse">
      <stop offset="0" stop-color="#DFF4D6"/>
      <stop offset="1" stop-color="#90C97E"/>
    </linearGradient>
    <linearGradient id="dbTopGrad" x1="0" y1="8" x2="0" y2="18" gradientUnits="userSpaceOnUse">
      <stop offset="0" stop-color="#AEE8C8"/>
      <stop offset="1" stop-color="#5ABA83"/>
    </linearGradient>
    <linearGradient id="dbSideGrad" x1="0" y1="10" x2="0" y2="22" gradientUnits="userSpaceOnUse">
      <stop offset="0" stop-color="#62BE87"/>
      <stop offset="1" stop-color="#2E8B57"/>
    </linearGradient>
    <filter id="glassShadow" x="-20%" y="-20%" width="140%" height="160%">
      <feDropShadow dx="0" dy="1.1" stdDeviation="1.2" flood-color="#5879A0" flood-opacity=".12"/>
    </filter>
  </defs>
""".strip()


def wrap(body: str) -> str:
    return "\n".join(
        [
            '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 32 32">',
            COMMON_DEFS,
            body,
            "</svg>",
            "",
        ]
    )


def save(name: str, body: str) -> None:
    (ICON_DIR / f"{name}.svg").write_text(wrap(body), encoding="utf-8")


def join(parts: Iterable[str]) -> str:
    return "".join(part for part in parts if part)


def tone_fill(tone: str) -> str:
    return {
        "blue": "url(#blueGrad)",
        "green": "url(#greenGrad)",
        "amber": "url(#amberGrad)",
        "teal": "url(#tealGrad)",
        "red": "url(#redGrad)",
        "violet": "url(#violetGrad)",
        "gray": "url(#grayGrad)",
    }[tone]


def tone_soft(tone: str) -> str:
    return {
        "blue": "#EEF6FF",
        "green": "#F1FBEA",
        "amber": "#FFF5DE",
        "teal": "#EEFDF9",
        "red": "#FFF0F0",
        "violet": "#F4F1FF",
        "gray": "#F8FAFC",
    }[tone]


def tone_dark(tone: str) -> str:
    return {
        "blue": BLUE,
        "green": GREEN,
        "amber": AMBER,
        "teal": TEAL,
        "red": RED,
        "violet": VIOLET,
        "gray": GRAY,
    }[tone]


def glass_base() -> str:
    return """
  <g filter="url(#glassShadow)">
    <rect x="1.5" y="1.5" width="29" height="29" rx="8" fill="url(#glassFill)" stroke="url(#glassStroke)" stroke-width="1"/>
    <path d="M5 6.2h22" stroke="#FFFFFF" stroke-width="1.2" stroke-linecap="round" opacity=".86"/>
    <path d="M6.5 25.7h19" stroke="#D9E6F2" stroke-width="1" stroke-linecap="round" opacity=".34"/>
    <path d="M4.2 4.8h9.5" stroke="#FFFFFF" stroke-width=".9" stroke-linecap="round" opacity=".8"/>
  </g>
"""


def page_card(
    x: float,
    y: float,
    w: float,
    h: float,
    tone: str = "blue",
    detail: str = "lines",
    corner: bool = False,
    header: bool = True,
) -> str:
    header_fill = tone_soft(tone)
    main_fill = tone_fill(tone)
    right = x + w
    parts = [
        f'<path d="M{x} {y}h{w-2}l2 2v{h-2}l-2 2H{x+2}l-2-2V{y+2}z" fill="#FFFFFF" stroke="{STROKE}" stroke-width="1"/>',
    ]
    if header:
        parts.append(f'<path d="M{x} {y}h{w-2}l2 2v2.2H{x}z" fill="{header_fill}"/>')
    parts.append(f'<rect x="{x+0.6}" y="{y+3.2}" width="{w-1.2}" height="{h-3.8}" fill="{main_fill}" rx=".7"/>')
    if corner:
        parts.append(f'<path d="M{right-2.2} {y}l2.2 2.2h-2.2z" fill="#FFFFFF" opacity=".95"/>')
        parts.append(f'<path d="M{right-2.2} {y}l2.2 2.2" stroke="#D6E3F0" stroke-width=".8"/>')
    if detail == "lines":
        parts.extend(
            [
                f'<path d="M{x+1.8} {y+6.2}h{w-3.6}M{x+1.8} {y+8.8}h{w-3.6}M{x+1.8} {y+11.4}h{w-5.2}" stroke="#F7FBFF" stroke-width=".95" stroke-linecap="round"/>',
                f'<circle cx="{x+1.6}" cy="{y+1.6}" r=".55" fill="#97BDE6"/><circle cx="{x+3.3}" cy="{y+1.6}" r=".55" fill="#97BDE6"/>',
            ]
        )
    elif detail == "grid":
        parts.extend(
            [
                f'<path d="M{x+1.7} {y+6.1}h{w-3.4}M{x+1.7} {y+8.8}h{w-3.4}M{x+1.7} {y+11.5}h{w-3.4}" stroke="#ECFFF0" stroke-width=".9" stroke-linecap="round"/>',
                f'<path d="M{x+3.8} {y+5.3}v{h-5.6}M{x+6.1} {y+5.3}v{h-5.6}" stroke="#ECFFF0" stroke-width=".85" stroke-linecap="round"/>',
            ]
        )
    elif detail == "image":
        parts.extend(
            [
                f'<rect x="{x+0.9}" y="{y+4.4}" width="{w-1.8}" height="{h-5.0}" fill="url(#skyGrad)" rx=".6"/>',
                f'<path d="M{x+0.9} {y+10.6}h{w-1.8}v{h-6.2}H{x+0.9}z" fill="url(#groundGrad)"/>',
                f'<circle cx="{x+w-2.6}" cy="{y+6.3}" r="1.2" fill="#FFF4B6"/>',
                f'<path d="M{x+1.7} {y+h-2.5}l2.6-3.6 2.2 2.4 2.1-1.8 2.2 3z" fill="none" stroke="{BLUE}" stroke-width="1" stroke-linecap="round" stroke-linejoin="round"/>',
            ]
        )
    elif detail == "layout":
        parts.extend(
            [
                f'<rect x="{x+1.4}" y="{y+4.8}" width="{w-2.8}" height="{h-6}" rx=".6" fill="#F8FBFF" opacity=".85"/>',
                f'<rect x="{x+2.1}" y="{y+5.5}" width="{w-5.5}" height="4.1" rx=".5" fill="#CEE7FF"/>',
                f'<rect x="{x+w-5.8}" y="{y+5.5}" width="3.1" height="4.1" rx=".5" fill="#E0F5D8"/>',
                f'<path d="M{x+2.1} {y+11.1}h{w-4.2}M{x+2.1} {y+13.3}h{w-6.1}" stroke="#9BB8D6" stroke-width=".8" stroke-linecap="round"/>',
            ]
        )
    elif detail == "cad":
        parts.extend(
            [
                f'<path d="M{x+1.6} {y+6.1}h{w-3.2}M{x+1.6} {y+8.7}h{w-3.2}M{x+1.6} {y+11.3}h{w-3.2}" stroke="#667584" stroke-width=".8" stroke-linecap="round"/>',
                f'<path d="M{x+4.2} {y+5.2}v{h-5.4}M{x+7.1} {y+5.2}v{h-5.4}" stroke="#AAB5C2" stroke-width=".8" stroke-linecap="round"/>',
                f'<path d="M{x+2.2} {y+h-2.2}l{w-4.2}-4.5" stroke="{BLUE}" stroke-width="1.1" stroke-linecap="round"/>',
            ]
        )
    elif detail == "schema":
        parts.extend(
            [
                f'<rect x="{x+1.5}" y="{y+5.2}" width="{w-3}" height="2" rx=".5" fill="#D8E8F8"/>',
                f'<rect x="{x+1.5}" y="{y+8.1}" width="{w-5}" height="2" rx=".5" fill="#D8F1D5"/>',
                f'<rect x="{x+1.5}" y="{y+11.0}" width="{w-4}" height="2" rx=".5" fill="#FFEFD2"/>',
                f'<path d="M{x+w-6.1} {y+6.2}h2.1M{x+w-8.1} {y+9.1}h4.1M{x+w-7.2} {y+12.0}h3.1" stroke="{BLUE}" stroke-width=".8" stroke-linecap="round"/>',
            ]
        )
    elif detail == "text":
        parts.append(f'<path d="M{x+1.7} {y+6.0}h{w-3.4}M{x+1.7} {y+8.3}h{w-4.4}M{x+1.7} {y+10.6}h{w-3.1}M{x+1.7} {y+12.9}h{w-5.4}" stroke="#EEF3F8" stroke-width=".95" stroke-linecap="round"/>')
    elif detail == "table":
        parts.extend(
            [
                f'<path d="M{x+1.3} {y+5.5}h{w-2.6}v{h-5.2}H{x+1.3}z" fill="#F7FBFF" opacity=".78"/>',
                f'<path d="M{x+1.3} {y+8.0}h{w-2.6}M{x+1.3} {y+10.4}h{w-2.6}M{x+1.3} {y+12.8}h{w-2.6}" stroke="#9CB7D5" stroke-width=".75" stroke-linecap="round"/>',
                f'<path d="M{x+4.0} {y+5.5}v{h-5.2}M{x+6.7} {y+5.5}v{h-5.2}" stroke="#9CB7D5" stroke-width=".72" stroke-linecap="round"/>',
            ]
        )
    elif detail == "map":
        parts.extend(
            [
                f'<rect x="{x+1.1}" y="{y+4.5}" width="{w-2.2}" height="{h-4.8}" fill="url(#skyGrad)" rx=".6"/>',
                f'<path d="M{x+1.1} {y+10.5}h{w-2.2}v{h-6.0}H{x+1.1}z" fill="url(#groundGrad)"/>',
                f'<path d="M{x+2.1} {y+11.6}l1.8-1.6 1.7 1.1 2.2-2.7 2.1 1.8 1.4-1.1" fill="none" stroke="{BLUE}" stroke-width="1" stroke-linecap="round" stroke-linejoin="round"/>',
            ]
        )
    return join(parts)


def database_icon(x: float, y: float, scale: float = 1.0) -> str:
    rx = 5.2 * scale
    ry = 2.4 * scale
    w = 10.4 * scale
    h = 8.6 * scale
    cx = x + rx
    return join(
        [
            f'<ellipse cx="{cx}" cy="{y}" rx="{rx}" ry="{ry}" fill="url(#dbTopGrad)" stroke="#33724F" stroke-width="1"/>',
            f'<path d="M{x} {y}v{h}c0 1.3 {rx-0.4} {ry} {rx} {ry}s{rx}-.7 {rx}-2V{y}" fill="url(#dbSideGrad)" stroke="#33724F" stroke-width="1"/>',
            f'<path d="M{x} {y+3.0*scale}c0 1.3 {rx-0.4} {ry} {rx} {ry}s{rx}-.7 {rx}-2M{x} {y+5.6*scale}c0 1.3 {rx-0.4} {ry} {rx} {ry}s{rx}-.7 {rx}-2" fill="none" stroke="#BDE7CA" stroke-width=".95" opacity=".95"/>',
            f'<path d="M{x+1.4*scale} {y+1.2*scale}h{w-2.8*scale}" stroke="#E8FFF0" stroke-width=".8" stroke-linecap="round" opacity=".9"/>',
        ]
    )


def polygon_icon(
    points: str,
    tone: str = "amber",
    stroke: str | None = None,
    nodes: bool = False,
    hole: bool = False,
) -> str:
    tone_stroke = stroke or {
        "amber": "#8E5F1A",
        "blue": BLUE,
        "green": GREEN,
        "teal": TEAL,
        "red": RED,
        "violet": VIOLET,
        "gray": GRAY,
    }[tone]
    items = [f'<path d="M{points}z" fill="{tone_fill(tone)}" stroke="{tone_stroke}" stroke-width="1"/>']
    if nodes:
        cleaned = points.replace(",", " ")
        nums = [float(v) for v in cleaned.split()]
        for i in range(0, len(nums), 2):
            cx = nums[i]
            cy = nums[i + 1]
            items.append(f'<circle cx="{cx}" cy="{cy}" r=".75" fill="#FFF6E0" stroke="{tone_stroke}" stroke-width=".6"/>')
    if hole:
        items.append('<circle cx="19" cy="18.5" r="2" fill="#FFFFFF" opacity=".88"/>')
        items.append('<circle cx="19" cy="18.5" r="1.3" fill="#FFD6D6"/>')
    return join(items)


def arrow_right(x1: float, y: float, x2: float, color: str = BLUE) -> str:
    return join(
        [
            f'<path d="M{x1} {y}h{x2-x1-1.8}" stroke="{color}" stroke-width="1.8" stroke-linecap="round"/>',
            f'<path d="m{x2-2.6} {y-2.0} 3.4 2-3.4 2" fill="none" stroke="{color}" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"/>',
        ]
    )


def double_arrow(x1: float, y: float, x2: float) -> str:
    return join(
        [
            f'<path d="M{x1+1.4} {y-2.2}h{x2-x1-4.2}" stroke="{BLUE}" stroke-width="1.4" stroke-linecap="round"/>',
            f'<path d="m{x2-3.4} {y-4.0} 2.8 1.8-2.8 1.8" fill="none" stroke="{BLUE}" stroke-width="1.4" stroke-linecap="round" stroke-linejoin="round"/>',
            f'<path d="M{x2-1.4} {y+2.2}H{x1+3.2}" stroke="{TEAL}" stroke-width="1.4" stroke-linecap="round"/>',
            f'<path d="m{x1+3.8} {y+0.4} -2.8 1.8 2.8 1.8" fill="none" stroke="{TEAL}" stroke-width="1.4" stroke-linecap="round" stroke-linejoin="round"/>',
        ]
    )


def badge_circle(cx: float, cy: float, r: float, tone: str = "blue", inner: str = "") -> str:
    return join(
        [
            f'<circle cx="{cx}" cy="{cy}" r="{r}" fill="{tone_fill(tone)}" stroke="{tone_dark(tone)}" stroke-width="1"/>',
            inner,
        ]
    )


def plus_badge(cx: float, cy: float, tone: str = "blue") -> str:
    return badge_circle(
        cx,
        cy,
        4.2,
        tone,
        f'<path d="M{cx} {cy-1.8}v3.6M{cx-1.8} {cy}h3.6" stroke="#FFFFFF" stroke-width="1.4" stroke-linecap="round"/>',
    )


def download_badge(cx: float, cy: float, tone: str = "blue") -> str:
    return badge_circle(
        cx,
        cy,
        4.2,
        tone,
        join(
            [
                f'<path d="M{cx} {cy-2.2}v3.1" stroke="#FFFFFF" stroke-width="1.35" stroke-linecap="round"/>',
                f'<path d="m{cx-1.7} {cy-0.2} 1.7 2 1.7-2" fill="none" stroke="#FFFFFF" stroke-width="1.35" stroke-linecap="round" stroke-linejoin="round"/>',
                f'<path d="M{cx-2.1} {cy+2.1}h4.2" stroke="#FFFFFF" stroke-width="1.2" stroke-linecap="round"/>',
            ]
        ),
    )


def magnifier_badge(cx: float, cy: float, tone: str = "blue") -> str:
    return badge_circle(
        cx,
        cy,
        4.2,
        tone,
        join(
            [
                f'<circle cx="{cx-0.6}" cy="{cy-0.6}" r="1.7" fill="none" stroke="#FFFFFF" stroke-width="1.2"/>',
                f'<path d="M{cx+0.7} {cy+0.7}l1.6 1.6" stroke="#FFFFFF" stroke-width="1.2" stroke-linecap="round"/>',
            ]
        ),
    )


def clock_badge(cx: float, cy: float, tone: str = "violet") -> str:
    return badge_circle(
        cx,
        cy,
        4.2,
        tone,
        join(
            [
                f'<circle cx="{cx}" cy="{cy}" r="2.2" fill="none" stroke="#FFFFFF" stroke-width="1.2"/>',
                f'<path d="M{cx} {cy}V{cy-1.3}M{cx} {cy}l1.1.9" stroke="#FFFFFF" stroke-width="1.1" stroke-linecap="round"/>',
            ]
        ),
    )


def check_badge(cx: float, cy: float, tone: str = "green") -> str:
    return badge_circle(
        cx,
        cy,
        4.2,
        tone,
        f'<path d="m{cx-1.8} {cy+.1} 1.2 1.3 2.4-2.7" fill="none" stroke="#FFFFFF" stroke-width="1.35" stroke-linecap="round" stroke-linejoin="round"/>',
    )


def warning_badge(cx: float, cy: float, tone: str = "amber") -> str:
    return badge_circle(
        cx,
        cy,
        4.2,
        tone,
        join(
            [
                f'<path d="M{cx} {cy-1.8}v2.5" stroke="#FFFFFF" stroke-width="1.35" stroke-linecap="round"/>',
                f'<circle cx="{cx}" cy="{cy+2.1}" r=".7" fill="#FFFFFF"/>',
            ]
        ),
    )


def pencil_badge(cx: float, cy: float, tone: str = "amber") -> str:
    return badge_circle(
        cx,
        cy,
        4.2,
        tone,
        join(
            [
                f'<path d="M{cx-1.7} {cy+1.2}l2.8-2.8 1.3 1.3-2.8 2.8-1.8.5z" fill="#FFFFFF" stroke="#FFFFFF" stroke-width=".4" stroke-linejoin="round"/>',
                f'<path d="M{cx+1.4} {cy-1.8}l.7-.7.9.9-.7.7" fill="#FFEEC6"/>',
            ]
        ),
    )


def rotate_badge(cx: float, cy: float, tone: str = "blue") -> str:
    return badge_circle(
        cx,
        cy,
        4.2,
        tone,
        join(
            [
                f'<path d="M{cx+1.7} {cy-1.1}a2.6 2.6 0 1 0 .4 3.0" fill="none" stroke="#FFFFFF" stroke-width="1.15" stroke-linecap="round"/>',
                f'<path d="m{cx+1.4} {cy-2.0} 1.7.5-.6 1.6" fill="none" stroke="#FFFFFF" stroke-width="1.15" stroke-linecap="round" stroke-linejoin="round"/>',
            ]
        ),
    )


def split_badge(cx: float, cy: float, tone: str = "amber") -> str:
    return badge_circle(
        cx,
        cy,
        4.2,
        tone,
        join(
            [
                f'<path d="M{cx} {cy-2.1}v4.2" stroke="#FFFFFF" stroke-width="1.2" stroke-dasharray="1.1 1.1" stroke-linecap="round"/>',
                f'<path d="M{cx-2.0} {cy-1.4}h1.2M{cx+0.8} {cy+1.4}h1.2" stroke="#FFFFFF" stroke-width="1.1" stroke-linecap="round"/>',
            ]
        ),
    )


def brush_badge(cx: float, cy: float, tone: str = "teal") -> str:
    return badge_circle(
        cx,
        cy,
        4.2,
        tone,
        join(
            [
                f'<path d="M{cx-1.8} {cy+1.6}c.3-1.3 1.1-2.3 2.1-3.4l1.8 1.8c-1.0 1-2.1 1.7-3.4 2.1z" fill="#FFFFFF"/>',
                f'<path d="M{cx+1.2} {cy-2.0}l1.1-1.1 1.2 1.2-1.1 1.1" fill="#D8FFF5"/>',
            ]
        ),
    )


def info_badge(cx: float, cy: float, tone: str = "blue") -> str:
    return badge_circle(
        cx,
        cy,
        5.2,
        tone,
        join(
            [
                f'<circle cx="{cx}" cy="{cy-2.0}" r=".8" fill="#FFFFFF"/>',
                f'<path d="M{cx} {cy-0.4}v4.0" stroke="#FFFFFF" stroke-width="1.6" stroke-linecap="round"/>',
            ]
        ),
    )


def spark(center_x: float, center_y: float, scale: float = 1.0) -> str:
    return f'<path d="M{center_x} {center_y-2.6*scale}l.8 1.8 1.8.8-1.8.8-.8 1.8-.8-1.8-1.8-.8 1.8-.8z" fill="#FFF4B8" stroke="#D8A72E" stroke-width=".6" stroke-linejoin="round"/>'


def globe_icon(cx: float, cy: float, r: float = 5.0) -> str:
    return join(
        [
            f'<circle cx="{cx}" cy="{cy}" r="{r}" fill="url(#blueGrad)" stroke="{BLUE}" stroke-width="1"/>',
            f'<path d="M{cx-r+1.0} {cy}h{2*r-2.0}M{cx-r+1.4} {cy-2.0}h{2*r-2.8}M{cx-r+1.4} {cy+2.0}h{2*r-2.8}" stroke="#EFF8FF" stroke-width=".8" stroke-linecap="round"/>',
            f'<path d="M{cx} {cy-r+1.0}v{2*r-2.0}M{cx-2.2} {cy-r+1.4}c1.6 2 1.6 6.2 0 8.4M{cx+2.2} {cy-r+1.4}c-1.6 2-1.6 6.2 0 8.4" stroke="#EFF8FF" stroke-width=".8" stroke-linecap="round"/>',
        ]
    )


def line_nodes(points: list[tuple[float, float]], color: str = BLUE, start_fill: str = "#FFE9A7") -> str:
    d = " ".join(("M" if i == 0 else "L") + f"{x} {y}" for i, (x, y) in enumerate(points))
    parts = [f'<path d="{d}" fill="none" stroke="{color}" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"/>']
    for idx, (x, y) in enumerate(points):
        fill = start_fill if idx == 0 else "#FFFFFF"
        parts.append(f'<circle cx="{x}" cy="{y}" r=".9" fill="{fill}" stroke="{color}" stroke-width=".8"/>')
    return join(parts)


def ruler(x: float, y: float, w: float, h: float) -> str:
    marks = []
    for idx in range(5):
        mx = x + 1.2 + idx * 1.5
        mh = 1.8 if idx % 2 == 0 else 1.1
        marks.append(f'<path d="M{mx} {y+h-mh}v{mh}" stroke="#FFFDF0" stroke-width=".75" stroke-linecap="round"/>')
    return join([f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="1.2" fill="{tone_fill("amber")}" stroke="{AMBER}" stroke-width=".8"/>', "".join(marks)])


def gear(cx: float, cy: float, r: float = 4.6) -> str:
    spokes = []
    for dx, dy in ((0, -r), (0, r), (-r, 0), (r, 0), (-3.1, -3.1), (3.1, 3.1), (-3.1, 3.1), (3.1, -3.1)):
        spokes.append(f'<path d="M{cx+dx*.62} {cy+dy*.62}l{dx*.35} {dy*.35}" stroke="{BLUE}" stroke-width="1.3" stroke-linecap="round"/>')
    return join(
        [
            "".join(spokes),
            f'<circle cx="{cx}" cy="{cy}" r="{r}" fill="url(#blueGrad)" stroke="{BLUE}" stroke-width="1"/>',
            f'<circle cx="{cx}" cy="{cy}" r="2.0" fill="#FFFFFF" opacity=".95"/>',
            f'<circle cx="{cx}" cy="{cy}" r="1.1" fill="{BLUE}"/>',
        ]
    )


def shield_lock(cx: float, cy: float) -> str:
    return join(
        [
            f'<path d="M{cx} {cy-6.0} 6 2.2v4.2c0 4.2-3 6.2-6 7.8-3-1.6-6-3.6-6-7.8v-4.2z" fill="url(#blueGrad)" stroke="{BLUE}" stroke-width="1"/>',
            f'<rect x="{cx-2.1}" y="{cy+1.2}" width="4.2" height="3.8" rx="1" fill="#FFFFFF"/>',
            f'<path d="M{cx-1.3} {cy+1.2}v-1.0a1.3 1.3 0 0 1 2.6 0v1.0" fill="none" stroke="{BLUE}" stroke-width="1"/>',
        ]
    )


def toolbox_case() -> str:
    return join(
        [
            '<path d="M7 12.5h18l2 2.2v8.8l-2 2H7l-2-2v-8.8z" fill="url(#amberGrad)" stroke="#9A6A22" stroke-width="1"/>',
            '<path d="M11 10h10l1.4 1.8v1.8H9.6v-1.8z" fill="#FFF4D8" stroke="#9A6A22" stroke-width="1"/>',
            '<path d="M5 17.1h22" stroke="#FFF2D2" stroke-width="1"/>',
            '<rect x="13.2" y="16.2" width="5.6" height="2.2" rx=".8" fill="#FFFFFF" opacity=".95"/>',
            '<path d="M10 21.2h3.2M18.8 21.2H22" stroke="#FFF2D2" stroke-width=".9" stroke-linecap="round"/>',
        ]
    )


def window_panel() -> str:
    return join(
        [
            '<rect x="5" y="7" width="22" height="18" rx="2.4" fill="#FFFFFF" stroke="#5E7E9C" stroke-width="1"/>',
            '<path d="M5 7h22v3.3H5z" fill="#EAF4FF"/>',
            '<circle cx="7.2" cy="8.7" r=".6" fill="#97BDE6"/><circle cx="9.0" cy="8.7" r=".6" fill="#97BDE6"/><circle cx="10.8" cy="8.7" r=".6" fill="#97BDE6"/>',
        ]
    )


def chat_bubble() -> str:
    return join(
        [
            '<path d="M7 10.5h14l2 2v7.7l-2 2H14l-3.6 3v-3H7l-2-2v-7.7z" fill="url(#blueGrad)" stroke="#4E86CC" stroke-width="1"/>',
            '<path d="M10.4 15h9.1M10.4 17.5h6.8" stroke="#FFFFFF" stroke-width="1" stroke-linecap="round"/>',
        ]
    )


def pin(cx: float, cy: float) -> str:
    return join(
        [
            f'<path d="M{cx} {cy+4.3}c2.2-2.6 3.5-4.7 3.5-6.2a3.5 3.5 0 1 0-7 0c0 1.5 1.3 3.6 3.5 6.2z" fill="url(#redGrad)" stroke="{RED}" stroke-width=".9"/>',
            f'<circle cx="{cx}" cy="{cy-1.7}" r="1.1" fill="#FFFFFF"/>',
        ]
    )


def crop_corners() -> str:
    return '<path d="M19.1 8.8h5.2M19.1 8.8v2.4M24.3 19.2v-2.4M24.3 19.2h-5.2M12.1 12.0h-2.4M9.7 12.0v5.0M12.1 17.0h-2.4M21.8 17.0v2.4" fill="none" stroke="#2F6FB5" stroke-width="1.4" stroke-linecap="round"/>'


def clipboard(x: float, y: float, w: float, h: float) -> str:
    return join(
        [
            f'<rect x="{x}" y="{y+1.2}" width="{w}" height="{h}" rx="1.5" fill="#FFFFFF" stroke="{STROKE}" stroke-width="1"/>',
            f'<rect x="{x+2.4}" y="{y}" width="{w-4.8}" height="2.4" rx="1.0" fill="#E8EEF5" stroke="{STROKE}" stroke-width=".8"/>',
            f'<path d="M{x+2} {y+5.0}h{w-4}M{x+2} {y+7.5}h{w-5.2}M{x+2} {y+10.0}h{w-4}" stroke="#A8BDD3" stroke-width=".8" stroke-linecap="round"/>',
        ]
    )


def scan_brackets() -> str:
    return '<path d="M7 10.5V8.4h2.1M25 10.5V8.4h-2.1M7 21.5v2.1h2.1M25 21.5v2.1h-2.1" fill="none" stroke="#4E86CC" stroke-width="1.3" stroke-linecap="round"/>'


def scene_convert(left: str, right: str, arrow_y: float = 15.0, extra: str = "") -> str:
    return join([glass_base(), left, arrow_right(14.0, arrow_y, 19.0), right, extra])


def scene_page_to_page(left_tone: str, left_detail: str, right_tone: str, right_detail: str, extra: str = "") -> str:
    left = page_card(4.0, 9.0, 9.5, 13.0, tone=left_tone, detail=left_detail, corner=True)
    right = page_card(19.0, 9.0, 9.0, 13.0, tone=right_tone, detail=right_detail, corner=True)
    return scene_convert(left, right, 15.0, extra)


def scene_layer_to_page(right_tone: str, right_detail: str, extra: str = "") -> str:
    left = join(
        [
            page_card(4.0, 11.0, 9.2, 10.2, tone="blue", detail="map", corner=False),
            page_card(6.0, 8.3, 9.2, 10.2, tone="green", detail="lines", corner=False),
            '<path d="M7.3 11.0h5.8M7.3 13.4h6.7" stroke="#F7FFF5" stroke-width=".9" stroke-linecap="round"/>',
        ]
    )
    right = page_card(19.0, 9.0, 9.0, 13.0, tone=right_tone, detail=right_detail, corner=True)
    return scene_convert(left, right, 15.0, extra)


def scene_database_to_page(right_tone: str, right_detail: str, extra: str = "") -> str:
    left = database_icon(4.0, 8.2, 1.0)
    right = page_card(19.0, 9.0, 9.0, 13.0, tone=right_tone, detail=right_detail, corner=True)
    return scene_convert(left, right, 14.5, extra)


def scene_page_to_database(left_tone: str, left_detail: str, extra: str = "") -> str:
    left = page_card(4.0, 9.0, 9.4, 13.0, tone=left_tone, detail=left_detail, corner=True)
    right = database_icon(18.5, 8.2, 0.98)
    return scene_convert(left, right, 14.5, extra)


def scene_single(body: str, extra: str = "") -> str:
    return join([glass_base(), body, extra])


def scene_layers_plus() -> str:
    return scene_single(
        join(
            [
                page_card(6.0, 10.4, 11.0, 8.4, tone="blue", detail="lines", corner=False),
                page_card(8.6, 7.8, 11.0, 8.4, tone="green", detail="lines", corner=False),
                page_card(11.2, 5.2, 11.0, 8.4, tone="amber", detail="lines", corner=False),
                plus_badge(24.5, 21.5, "blue"),
            ]
        )
    )


def scene_batch_add_data() -> str:
    return scene_single(
        join(
            [
                page_card(5.5, 11.0, 9.6, 9.2, tone="blue", detail="table", corner=False),
                page_card(8.3, 8.3, 9.6, 9.2, tone="green", detail="lines", corner=False),
                page_card(11.1, 5.6, 9.6, 9.2, tone="amber", detail="map", corner=False),
                plus_badge(23.8, 20.8, "green"),
            ]
        )
    )


def scene_quick_add_data() -> str:
    return scene_single(
        join(
            [
                window_panel(),
                page_card(7.0, 12.1, 8.2, 6.7, tone="green", detail="lines", corner=False, header=False),
                page_card(10.2, 9.5, 8.2, 6.7, tone="blue", detail="table", corner=False, header=False),
                page_card(13.4, 12.1, 8.2, 6.7, tone="amber", detail="map", corner=False, header=False),
                plus_badge(24.0, 21.0, "green"),
            ]
        )
    )


def scene_area_calculator() -> str:
    return join(
        [
            glass_base(),
            polygon_icon("8 12 18 9 25 14 22 23 11 24", tone="amber", nodes=True),
            '<path d="M11.2 16.0h8.5M10.2 19.0h10.3" stroke="#FFF3D5" stroke-width=".9" stroke-linecap="round"/>',
            ruler(17.2, 18.0, 8.5, 4.6),
        ]
    )


def scene_area_split() -> str:
    return join(
        [
            glass_base(),
            polygon_icon("8 11.8 18 9.2 25 14.5 22 23 11 24", tone="amber", nodes=True),
            '<path d="M16.2 11.2v12.0" stroke="#FFFFFF" stroke-width="1.1" stroke-dasharray="1.3 1.3" stroke-linecap="round"/>',
            split_badge(24.2, 9.2, "amber"),
        ]
    )


def scene_layer_clip() -> str:
    return join(
        [
            glass_base(),
            page_card(7.0, 9.5, 11.0, 8.4, tone="blue", detail="lines", corner=False),
            page_card(10.0, 6.6, 11.0, 8.4, tone="green", detail="lines", corner=False),
            crop_corners(),
            polygon_icon("22.5 13.2 27 15.1 24.8 23 18.7 24.8 16.2 20.7 18.4 14.6", tone="amber", stroke="#8E5F1A", nodes=True),
        ]
    )


def scene_range_clip() -> str:
    return join(
        [
            glass_base(),
            page_card(6.2, 8.6, 11.5, 8.8, tone="blue", detail="map", corner=False),
            page_card(8.8, 11.2, 11.5, 8.8, tone="green", detail="lines", corner=False),
            '<rect x="17.8" y="10.0" width="7.4" height="8.5" rx="1.2" fill="none" stroke="#2F6FB5" stroke-width="1.4" stroke-dasharray="1.4 1.2"/>',
            '<path d="M18.5 18.5h5.6" stroke="#2F6FB5" stroke-width="1.1" stroke-linecap="round"/>',
        ]
    )


def scene_polygon_check(kind: str) -> str:
    badges = {
        "gap": warning_badge(24.2, 9.2, "amber"),
        "overlap": magnifier_badge(24.2, 9.2, "blue"),
        "hole": check_badge(24.2, 9.2, "green"),
    }
    overlay = {
        "gap": '<path d="M14.9 14.1l2.6 2.2-2.6 2.4" fill="#FFF5DE" stroke="#D49B3A" stroke-width=".8" stroke-linejoin="round"/>',
        "overlap": '<path d="M13.0 13.0 18.6 11.6 22.0 15.4 18.6 19.4 13.0 18.0 10.3 14.9z" fill="url(#redGrad)" opacity=".72"/>',
        "hole": '<circle cx="18.8" cy="18.0" r="1.9" fill="#FFFFFF" opacity=".9"/><circle cx="18.8" cy="18.0" r="1.2" fill="#FFD9D9"/>',
    }
    return join([glass_base(), polygon_icon("8.1 12.0 17.8 9.8 24.0 14.7 21.9 23.0 11.0 23.8", tone="amber", nodes=True), overlay[kind], badges[kind]])


def scene_view_area() -> str:
    return join([glass_base(), polygon_icon("8.0 12.0 18.1 9.5 24.2 14.2 22.0 23.0 11.0 24.0", tone="amber", nodes=True), magnifier_badge(24.0, 9.0, "blue")])


def scene_boundary(kind: str) -> str:
    if kind == "point":
        return join([glass_base(), polygon_icon("8.2 11.7 18.4 9.3 24.4 14.0 22.1 22.6 11.0 23.7", tone="amber", nodes=True), plus_badge(24.0, 9.0, "green")])
    if kind == "line":
        return join([glass_base(), line_nodes([(7.5, 19.5), (11.4, 12.2), (16.2, 14.8), (21.0, 10.2), (24.7, 15.8)], color=BLUE), '<path d="M9.0 22.5h14.5" stroke="#CFE1F4" stroke-width=".9" stroke-linecap="round"/>', plus_badge(24.0, 9.0, "green")])
    if kind == "mapline":
        return join([glass_base(), page_card(5.0, 8.8, 15.8, 12.0, tone="blue", detail="map", corner=False), line_nodes([(8.0, 19.0), (11.6, 13.2), (15.0, 15.4), (18.4, 11.8)], color=TEAL, start_fill="#FFF3D5"), plus_badge(24.3, 9.2, "green")])
    if kind == "viewstart":
        return join([glass_base(), line_nodes([(8.2, 18.6), (12.0, 12.5), (16.0, 15.0), (21.5, 10.8)], color=BLUE, start_fill="#FFE9A7"), magnifier_badge(24.0, 9.0, "blue")])
    return join([glass_base(), line_nodes([(8.2, 18.6), (12.0, 12.5), (16.0, 15.0), (21.5, 10.8)], color=BLUE, start_fill="#FFE9A7"), pencil_badge(24.0, 9.0, "amber")])


def scene_intersections(kind: str) -> str:
    overlay = '<path d="M14.2 12.0 18.5 11.0 21.2 14.3 18.6 18.0 14.1 16.8 11.9 13.8z" fill="url(#redGrad)" opacity=".78"/>' if kind == "intersect" else '<path d="M13.6 11.7 17.2 10.8 20.0 13.0 19.2 17.2 15.1 18.1 11.8 15.8z" fill="url(#redGrad)" opacity=".64"/><path d="M15.4 14.3 19.4 13.4 22.2 15.8 20.6 20.0 16.2 20.2 13.4 17.9z" fill="url(#tealGrad)" opacity=".58"/>'
    return join([glass_base(), polygon_icon("8.0 12.2 14.0 10.8 18.0 14.0 15.5 18.7 9.2 17.8", tone="blue"), polygon_icon("14.0 13.0 20.0 11.5 24.5 15.2 22.2 20.8 15.7 20.3", tone="green"), overlay, page_card(20.3, 18.2, 7.0, 6.5, tone="gray", detail="table", corner=False, header=False)])


def scene_data_pivot() -> str:
    return join([glass_base(), page_card(4.6, 8.9, 10.0, 12.6, tone="gray", detail="table", corner=True), '<path d="M15.0 13.0h5.0" stroke="#4E86CC" stroke-width="1.4" stroke-linecap="round"/>', '<path d="m18.6 11.0 2.6 2-2.6 2" fill="none" stroke="#4E86CC" stroke-width="1.4" stroke-linecap="round" stroke-linejoin="round"/>', '<path d="M19.4 17.0h-4.8" stroke="#2E8E85" stroke-width="1.4" stroke-linecap="round"/>', '<path d="m15.2 15.0 -2.4 2 2.4 2" fill="none" stroke="#2E8E85" stroke-width="1.4" stroke-linecap="round" stroke-linejoin="round"/>', page_card(20.0, 10.2, 7.4, 9.6, tone="blue", detail="grid", corner=False)])


def scene_projection() -> str:
    return join([glass_base(), globe_icon(10.4, 15.5, 5.2), '<path d="M16.3 15.5h5.1" stroke="#4E86CC" stroke-width="1.6" stroke-linecap="round"/>', '<path d="m19.6 13.4 3.2 2.1-3.2 2.1" fill="none" stroke="#4E86CC" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round"/>', page_card(20.2, 10.0, 7.4, 11.2, tone="gray", detail="map", corner=False)])


def scene_geometry_repair() -> str:
    return join([glass_base(), polygon_icon("8.0 12.0 18.2 9.4 24.0 14.5 21.8 23.0 11.0 24.0", tone="amber", nodes=True), check_badge(24.2, 9.2, "green"), '<path d="M12.0 19.5l2.0-1.6 1.2 1.1 1.7-2.4" fill="none" stroke="#FFF6DA" stroke-width="1.0" stroke-linecap="round" stroke-linejoin="round"/>'])


def scene_numbering(kind: str) -> str:
    seal = "#D6655E" if kind == "chinese" else "#4E86CC"
    marker = pin(24.4, 11.4) if kind == "chinese" else plus_badge(24.4, 11.2, "blue")
    return join([glass_base(), page_card(5.8, 8.8, 13.2, 13.8, tone="gray", detail="lines", corner=True), f'<circle cx="9.2" cy="13.0" r="1.3" fill="{seal}"/><circle cx="9.2" cy="16.4" r="1.3" fill="{seal}"/><circle cx="9.2" cy="19.8" r="1.3" fill="{seal}"/>', '<path d="M11.8 13.0h5.8M11.8 16.4h5.8M11.8 19.8h4.4" stroke="#A8BDD3" stroke-width=".9" stroke-linecap="round"/>', marker])


def scene_attribute_transfer(fields: bool = False) -> str:
    left = page_card(4.0, 9.0, 9.0, 13.0, tone="blue", detail="table", corner=True)
    right = page_card(19.0, 9.0, 9.0, 13.0, tone="green", detail="table" if fields else "lines", corner=True)
    extra = '<path d="M7.2 13.0h4.8M22.0 13.0h4.8" stroke="#EDF7EC" stroke-width=".75" stroke-linecap="round"/>' if fields else ""
    return scene_convert(left, right, 15.0, extra)


def scene_symbology(paste: bool) -> str:
    if paste:
        return join([glass_base(), clipboard(5.0, 8.8, 10.2, 12.0), brush_badge(14.4, 19.8, "teal"), page_card(19.0, 10.0, 8.5, 10.8, tone="blue", detail="map", corner=False)])
    return join([glass_base(), page_card(4.6, 10.2, 8.4, 10.4, tone="blue", detail="map", corner=False), page_card(19.0, 10.2, 8.4, 10.4, tone="green", detail="map", corner=False), brush_badge(16.0, 9.5, "teal"), arrow_right(12.6, 16.0, 19.0, BLUE)])


def scene_special_coordinate_transform() -> str:
    return join([glass_base(), '<path d="M8.0 21.8V10.5M8.0 21.8h11.2" stroke="#607D94" stroke-width="1.1" stroke-linecap="round"/>', '<circle cx="11.5" cy="17.0" r="1.2" fill="#4E86CC"/><circle cx="17.5" cy="12.8" r="1.2" fill="#2E8E85"/>', '<path d="M12.7 16.0l3.6-2.6" stroke="#4E86CC" stroke-width="1.2" stroke-linecap="round"/>', '<path d="M18.8 11.8h4.8" stroke="#4E86CC" stroke-width="1.4" stroke-linecap="round"/>', '<path d="m22.0 9.8 3.0 2-3.0 2" fill="none" stroke="#4E86CC" stroke-width="1.4" stroke-linecap="round" stroke-linejoin="round"/>', '<path d="M24.2 19.4h-4.8" stroke="#2E8E85" stroke-width="1.4" stroke-linecap="round"/>', '<path d="m20.6 17.4 -3.0 2 3.0 2" fill="none" stroke="#2E8E85" stroke-width="1.4" stroke-linecap="round" stroke-linejoin="round"/>'])


def scene_internet_tile_download() -> str:
    return join(
        [
            glass_base(),
            page_card(4.8, 8.2, 14.6, 14.6, tone="blue", detail="map", corner=False),
            '<path d="M9.2 10.8h8.8M9.2 14.4h8.8M9.2 18.0h8.8M12.1 10.0v10.9M15.0 10.0v10.9" stroke="#EAF5FF" stroke-width=".95" stroke-linecap="round"/>',
            globe_icon(23.1, 12.2, 3.8),
            download_badge(24.0, 22.1, "blue"),
        ]
    )


def scene_map_sheet_assign(kind: str) -> str:
    cols = 2 if kind == "large" else 4
    rows = 2 if kind == "large" else 4
    x = 6.0
    y = 7.2
    w = 19.0
    h = 17.0
    cell_w = w / cols
    cell_h = h / rows
    lines = []
    for i in range(1, cols):
        px = x + i * cell_w
        lines.append(f'<path d="M{px:.2f} {y+1.0}v{h-2.0}" stroke="#A6BED7" stroke-width=".8"/>')
    for i in range(1, rows):
        py = y + i * cell_h
        lines.append(f'<path d="M{x+1.0} {py:.2f}h{w-2.0}" stroke="#A6BED7" stroke-width=".8"/>')
    focus_w = cell_w - 2.8 if kind == "large" else cell_w + 0.1
    focus_h = cell_h - 2.8 if kind == "large" else cell_h + 0.1
    focus_x = x + 1.4
    focus_y = y + 1.4 if kind == "large" else y + cell_h + 0.9
    return join(
        [
            glass_base(),
            page_card(x, y, w, h, tone="blue", detail="map", corner=False),
            f'<rect x="{focus_x:.2f}" y="{focus_y:.2f}" width="{focus_w:.2f}" height="{focus_h:.2f}" rx="1.2" fill="#FFF3D7" opacity=".88" stroke="#D39A34" stroke-width=".9"/>',
            "".join(lines),
            pencil_badge(24.0, 24.0, "amber"),
        ]
    )


def scene_map_sheets(kind: str) -> str:
    cols = 2 if kind == "large" else 4
    rows = 2 if kind == "large" else 4
    x = 6.0
    y = 7.2
    w = 19.0
    h = 17.0
    cell_w = w / cols
    cell_h = h / rows
    lines = []
    for i in range(1, cols):
        px = x + i * cell_w
        lines.append(f'<path d="M{px:.2f} {y+1.0}v{h-2.0}" stroke="#A6BED7" stroke-width=".8"/>')
    for i in range(1, rows):
        py = y + i * cell_h
        lines.append(f'<path d="M{x+1.0} {py:.2f}h{w-2.0}" stroke="#A6BED7" stroke-width=".8"/>')
    return join([glass_base(), page_card(x, y, w, h, tone="blue", detail="map", corner=False), "".join(lines), plus_badge(24.0, 24.0, "green")])


def scene_map_series_export() -> str:
    return join([glass_base(), page_card(5.2, 7.8, 10.8, 13.2, tone="blue", detail="layout", corner=True), page_card(8.2, 10.6, 10.8, 13.2, tone="green", detail="layout", corner=True), arrow_right(18.0, 16.2, 27.0, BLUE)])


def scene_database_mirror() -> str:
    return join([glass_base(), database_icon(4.0, 8.3, 0.95), database_icon(18.0, 8.3, 0.95), double_arrow(13.0, 15.5, 18.0)])


def scene_batch_merge_shp() -> str:
    return join([glass_base(), polygon_icon("5.4 12.5 10.0 11.3 12.8 14.3 11.2 18.6 6.0 18.0", tone="blue"), polygon_icon("14.6 13.0 19.2 11.9 22.0 14.8 20.6 19.1 15.2 18.7", tone="green"), arrow_right(11.7, 22.0, 19.8, BLUE), polygon_icon("20.0 19.0 25.5 17.8 28.0 20.6 26.6 24.6 21.0 24.4 18.8 21.6", tone="amber")])


def scene_layout_coordinate_table(ocr: bool = False) -> str:
    return join([glass_base(), page_card(4.8, 8.0, 11.2, 13.8, tone="blue", detail="layout", corner=True), page_card(18.2, 10.2, 9.0, 11.0, tone="gray", detail="table", corner=False), scan_brackets() if ocr else ""])


def scene_doc_replace() -> str:
    return join([glass_base(), page_card(4.0, 9.0, 9.0, 13.0, tone="blue", detail="text", corner=True), page_card(19.0, 9.0, 9.0, 13.0, tone="blue", detail="text", corner=True), arrow_right(13.6, 15.0, 19.2, TEAL), '<path d="M22.3 13.1h2.9M22.3 16.2h2.9" stroke="#EEF5FB" stroke-width=".9" stroke-linecap="round"/>'])


def scene_doc_export_layout() -> str:
    return join([glass_base(), page_card(6.0, 7.8, 12.0, 15.0, tone="blue", detail="layout", corner=True), arrow_right(18.0, 15.5, 26.5, BLUE), page_card(20.4, 12.5, 7.0, 8.6, tone="gray", detail="lines", corner=False)])


def scene_overture() -> str:
    return join([glass_base(), globe_icon(10.4, 15.4, 5.2), '<rect x="17.2" y="10.8" width="9.0" height="10.4" rx="1.6" fill="url(#tealGrad)" stroke="#2E8E85" stroke-width="1"/>', '<path d="M18.8 18.8h5.8M20.0 16.0v2.8M22.6 14.2v4.6" stroke="#EFFDF9" stroke-width=".9" stroke-linecap="round"/>', plus_badge(23.8, 23.2, "green")])


def scene_toolbox() -> str:
    return scene_single(toolbox_case())


def scene_about() -> str:
    return scene_single(info_badge(16.0, 16.2, "blue"))


def scene_settings() -> str:
    return scene_single(gear(16.0, 16.0))


def scene_ai() -> str:
    return scene_single(join([chat_bubble(), spark(22.6, 11.0, 1.1)]))


def scene_auth() -> str:
    return scene_single(shield_lock(16.0, 15.0))


def scene_addin_desktop() -> str:
    return scene_single(join([window_panel(), '<rect x="8.0" y="12.6" width="4.2" height="4.2" rx=".8" fill="url(#blueGrad)"/>', '<rect x="13.9" y="12.6" width="4.2" height="4.2" rx=".8" fill="url(#greenGrad)"/>', '<rect x="19.8" y="12.6" width="4.2" height="4.2" rx=".8" fill="url(#amberGrad)"/>', '<path d="M8.0 19.8h16.0" stroke="#A8BDD3" stroke-width=".9" stroke-linecap="round"/>']))


def scene_globe() -> str:
    return scene_single(globe_icon(16.0, 16.0, 6.0))


def scene_plugin_update() -> str:
    return scene_single(join([window_panel(), rotate_badge(23.2, 20.5, "blue")]))


def scene_historical(download: bool = False, online: bool = False) -> str:
    if online:
        return join([glass_base(), globe_icon(12.0, 15.4, 5.0), page_card(17.8, 10.0, 8.7, 10.6, tone="blue", detail="image", corner=False), download_badge(23.8, 22.4, "blue")])
    body = [glass_base(), page_card(6.0, 8.0, 14.0, 14.8, tone="blue", detail="image", corner=True), clock_badge(24.2, 9.0, "violet")]
    if download:
        body.append(download_badge(24.0, 22.2, "blue"))
    return join(body)


def scene_doc_convert(name: str) -> str:
    if name == "WordToPdf":
        return scene_page_to_page("blue", "text", "red", "text")
    if name == "ExcelToPdf":
        return scene_page_to_page("green", "grid", "red", "text")
    if name == "PdfToImages":
        return scene_page_to_page("red", "text", "blue", "image")
    if name == "ImagesToPdf":
        return scene_page_to_page("blue", "image", "red", "text")
    if name == "FeatureToTxt":
        return scene_layer_to_page("gray", "text")
    if name == "TxtToFeature":
        return scene_convert(page_card(4.0, 9.0, 9.4, 13.0, tone="gray", detail="text", corner=True), page_card(18.8, 10.2, 9.2, 10.6, tone="blue", detail="map", corner=False))
    if name == "ExportExcel":
        return scene_layer_to_page("green", "grid")
    if name == "ExportCAD":
        return scene_layer_to_page("gray", "cad")
    if name == "ExportToKml":
        return join([glass_base(), page_card(4.2, 10.0, 9.8, 10.8, tone="blue", detail="map", corner=False), arrow_right(13.6, 15.4, 19.5), globe_icon(22.8, 15.2, 4.2), pin(23.0, 16.4)])
    if name in {"PolygonToDwgWithFill", "PolygonToDxfWithFill"}:
        return join([glass_base(), polygon_icon("6.4 12.5 13.5 10.6 18.4 14.0 16.2 21.4 8.0 22.2", tone="amber", nodes=True), arrow_right(17.4, 16.0, 20.8), page_card(20.0, 10.0, 8.0, 11.2, tone="gray", detail="cad", corner=False)])
    return scene_doc_export_layout()


def scene_database_flow(name: str) -> str:
    if name == "DatabaseBuilder":
        return scene_page_to_database("gray", "table")
    if name == "ShapefileBuilder":
        return join([glass_base(), page_card(4.0, 9.0, 9.0, 13.0, tone="gray", detail="table", corner=True), arrow_right(13.6, 15.0, 19.0), polygon_icon("20.0 12.0 25.6 10.8 28.2 14.4 26.5 20.4 20.6 21.0 18.6 16.4", tone="amber", nodes=True)])
    if name == "ExportShpFieldTable":
        return join([glass_base(), polygon_icon("5.5 12.0 11.2 10.7 14.0 14.3 12.3 20.2 6.5 20.8 4.5 16.1", tone="amber", nodes=True), arrow_right(13.8, 15.2, 19.0), page_card(19.0, 9.0, 9.0, 13.0, tone="gray", detail="table", corner=True)])
    if name == "BatchMergeShp":
        return scene_batch_merge_shp()
    if name == "MdbBatchToGdb":
        return scene_database_to_page("blue", "grid")
    if name == "MirrorDatabase":
        return scene_database_mirror()
    return scene_database_to_page("gray", "schema")


def scene_docs(name: str) -> str:
    if name == "DocumentBatchReplace":
        return scene_doc_replace()
    if name == "ExportLayout":
        return scene_doc_export_layout()
    if name == "LayoutTextReplace":
        return join([glass_base(), page_card(5.5, 8.0, 13.0, 15.0, tone="blue", detail="layout", corner=True), arrow_right(18.0, 15.2, 25.5, TEAL), page_card(20.6, 11.8, 6.8, 8.0, tone="gray", detail="text", corner=False)])
    if name == "LayoutCoordinateTable":
        return scene_layout_coordinate_table(False)
    return scene_layout_coordinate_table(True)


def scene_analysis(name: str) -> str:
    if name == "IntersectSummary":
        return scene_intersections("intersect")
    if name == "MultiOverlaySummary":
        return scene_intersections("multi")
    if name == "DataPivot":
        return scene_data_pivot()
    if name == "GapCheck":
        return scene_polygon_check("gap")
    if name == "OverlapCheck":
        return scene_polygon_check("overlap")
    if name == "ExtractPolygonHoles":
        return scene_polygon_check("hole")
    if name in {"NodeDistanceCheck", "NodeDistanceChecker"}:
        return join([glass_base(), line_nodes([(7.6, 19.6), (11.6, 13.0), (16.0, 15.0), (21.0, 10.8)], color=BLUE), ruler(19.5, 19.0, 7.4, 4.2)])
    if name == "DevZoneCheck":
        return join([glass_base(), page_card(5.2, 8.6, 13.4, 13.8, tone="blue", detail="map", corner=True), polygon_icon("12.4 11.8 18.0 10.7 20.8 13.6 19.6 18.8 13.7 19.3 11.4 16.2", tone="green"), check_badge(24.0, 9.1, "green")])
    return scene_view_area()


def scene_misc(name: str) -> str:
    if name == "Toolbox":
        return scene_toolbox()
    if name == "About":
        return scene_about()
    if name == "Settings":
        return scene_settings()
    if name == "Authorization":
        return scene_auth()
    if name == "AIAssistant":
        return scene_ai()
    if name == "AddInDesktop":
        return scene_addin_desktop()
    if name == "Globe":
        return scene_globe()
    if name in {"Overture", "OvertureLoader"}:
        return scene_overture()
    return scene_plugin_update()


def build_icon(name: str) -> str:
    if name == "PresetLayers":
        return scene_layers_plus()
    if name == "BatchAddData":
        return scene_batch_add_data()
    if name == "QuickAddData":
        return scene_quick_add_data()
    if name == "AreaCalculator":
        return scene_area_calculator()
    if name == "AreaSplit":
        return scene_area_split()
    if name == "BatchLayerClip":
        return scene_layer_clip()
    if name == "RangeClipTool":
        return scene_range_clip()
    if name == "ViewArea":
        return scene_view_area()
    if name == "BoundaryPointGenerator":
        return scene_boundary("point")
    if name == "BoundaryPointLineGenerator":
        return scene_boundary("line")
    if name == "MapBoundaryPointLineGenerator":
        return scene_boundary("mapline")
    if name == "ViewStartPoint":
        return scene_boundary("viewstart")
    if name == "ModifyStartPoint":
        return scene_boundary("modifystart")
    if name == "RotateGeometry":
        return join([glass_base(), polygon_icon("8.2 12.0 18.2 9.6 24.2 14.4 21.8 23.2 11.0 24.0", tone="amber", nodes=True), rotate_badge(24.0, 9.0, "blue")])
    if name == "ProtocolLineExtract":
        return join([glass_base(), polygon_icon("7.8 12.0 18.2 9.6 24.0 14.5 21.8 23.1 10.8 24.0", tone="amber"), '<path d="M10.8 20.0 16.0 15.2 21.0 17.0" fill="none" stroke="#FFFFFF" stroke-width="1.1" stroke-linecap="round" stroke-linejoin="round"/>', arrow_right(17.0, 13.4, 26.0, TEAL)])
    if name == "BatchProjectionDefinition":
        return scene_projection()
    if name == "BatchGeometryRepair":
        return scene_geometry_repair()
    if name == "HistoricalImagery":
        return scene_historical(False, False)
    if name == "DownloadHistoricalImagery":
        return scene_historical(True, False)
    if name == "DownloadOnlineImagery":
        return scene_historical(False, True)
    if name == "InternetTileDownload":
        return scene_internet_tile_download()
    if name in {"WordToPdf", "ExcelToPdf", "PdfToImages", "ImagesToPdf", "FeatureToTxt", "TxtToFeature", "ExportExcel", "ExportCAD", "ExportToKml", "PolygonToDwgWithFill", "PolygonToDxfWithFill"}:
        return scene_doc_convert(name)
    if name in {"DocumentBatchReplace", "ExportLayout", "LayoutTextReplace", "LayoutCoordinateTable", "OCRCoordinateTable"}:
        return scene_docs(name)
    if name in {"IntersectSummary", "MultiOverlaySummary", "DataPivot", "GapCheck", "OverlapCheck", "ExtractPolygonHoles", "NodeDistanceCheck", "NodeDistanceChecker", "DevZoneCheck"}:
        return scene_analysis(name)
    if name in {"DatabaseBuilder", "ShapefileBuilder", "ExportShpFieldTable", "BatchMergeShp", "MdbBatchToGdb", "MirrorDatabase", "ExportDatabaseSchema"}:
        return scene_database_flow(name)
    if name == "MapSeriesExport":
        return scene_map_series_export()
    if name == "MapSheetsLarge":
        return scene_map_sheets("large")
    if name == "MapSheetsSmall":
        return scene_map_sheets("small")
    if name == "MapSheetsLargeAssign":
        return scene_map_sheet_assign("large")
    if name == "MapSheetsSmallAssign":
        return scene_map_sheet_assign("small")
    if name == "AttributeTransfer":
        return scene_attribute_transfer(False)
    if name == "AttributeTransferFields":
        return scene_attribute_transfer(True)
    if name == "PasteSymbology":
        return scene_symbology(True)
    if name == "MatchSymbology":
        return scene_symbology(False)
    if name == "FieldCopyTool":
        return scene_convert(page_card(4.0, 9.0, 9.0, 13.0, tone="gray", detail="table", corner=True), page_card(19.0, 9.0, 9.0, 13.0, tone="blue", detail="table", corner=True))
    if name == "GroupNumbering":
        return scene_numbering("group")
    if name == "ChineseNumbering":
        return scene_numbering("chinese")
    if name == "SpecialCoordinateTransform":
        return scene_special_coordinate_transform()
    return scene_misc(name)


ICON_NAMES = [
    "About", "AddInDesktop", "AIAssistant", "AreaCalculator", "AreaSplit", "AttributeTransfer",
    "AttributeTransferFields", "Authorization", "BatchAddData", "BatchGeometryRepair", "BatchLayerClip",
    "BatchMergeShp", "BatchProjectionDefinition", "BoundaryPointGenerator", "BoundaryPointLineGenerator",
    "ChineseNumbering", "DatabaseBuilder", "DataPivot", "DevZoneCheck", "DocumentBatchReplace",
    "DownloadHistoricalImagery", "DownloadOnlineImagery", "ExcelToPdf", "ExportCAD", "ExportDatabaseSchema",
    "ExportExcel", "ExportLayout", "ExportShpFieldTable", "ExportToKml", "ExtractPolygonHoles", "FeatureToTxt",
    "FieldCopyTool", "GapCheck", "Globe", "GroupNumbering", "HistoricalImagery", "ImagesToPdf",
    "IntersectSummary", "InternetTileDownload", "LayoutCoordinateTable", "LayoutTextReplace", "MapBoundaryPointLineGenerator",
    "MapSeriesExport", "MapSheetsLarge", "MapSheetsLargeAssign", "MapSheetsSmall", "MapSheetsSmallAssign", "MatchSymbology", "MdbBatchToGdb", "MirrorDatabase",
    "ModifyStartPoint", "MultiOverlaySummary", "NodeDistanceCheck", "NodeDistanceChecker", "OCRCoordinateTable",
    "Overture", "OvertureLoader", "PasteSymbology", "PdfToImages", "PluginUpdate", "PolygonToDwgWithFill",
    "PolygonToDxfWithFill", "PresetLayers", "ProtocolLineExtract", "RangeClipTool", "RotateGeometry", "Settings",
    "QuickAddData", "ShapefileBuilder", "SpecialCoordinateTransform", "Toolbox", "TxtToFeature", "ViewArea", "ViewStartPoint",
    "WordToPdf",
]


def write_catalog(names: list[str]) -> None:
    cards = []
    for name in names:
        cards.append(f'''      <section class="card"><div class="preview"><img src="./Icons/{name}.svg" alt="{name}"></div><h2>{name}</h2></section>\n''')
    html = f"""<!DOCTYPE html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>Icon Catalog</title><style>
body{{margin:0;font-family:"Segoe UI","Microsoft YaHei UI",sans-serif;background:radial-gradient(circle at top left,rgba(100,167,236,.16),transparent 24%),linear-gradient(180deg,#f7fafc,#eef3f7);color:#1f3449}}
.wrap{{max-width:1400px;margin:0 auto;padding:28px}}.grid{{display:grid;grid-template-columns:repeat(6,minmax(0,1fr));gap:16px}}
.card{{background:#fff;border:1px solid rgba(31,52,73,.08);border-radius:16px;padding:12px;box-shadow:0 10px 24px rgba(31,52,73,.06)}}
.preview{{display:grid;place-items:center;min-height:110px;border-radius:12px;background:linear-gradient(180deg,#fff,#f4f7fa);border:1px solid rgba(31,52,73,.06)}}
img{{width:72px;height:72px}}h2{{margin:10px 0 0;font-size:13px;font-weight:600;word-break:break-word}}
</style></head><body><div class="wrap"><div class="grid">{''.join(cards)}</div></div></body></html>"""
    CATALOG_FILE.write_text(html, encoding="utf-8")


def main() -> None:
    ICON_DIR.mkdir(parents=True, exist_ok=True)
    for name in ICON_NAMES:
        save(name, build_icon(name))
    write_catalog(ICON_NAMES)
    print(f"generated={len(ICON_NAMES)}")


if __name__ == "__main__":
    main()
