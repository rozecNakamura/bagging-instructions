"""
Rename static Text 'g' between FILLQUN** and USEQUN** (same row) to FILLQUNUNIT**,
and between SUBFILLQUN** and SUBUSEQUN** to SUBFILLQUNUNIT**.

Preserves UTF-16. Run from repo root:
  python scripts/relabel_rxz_fill_column_g_to_unit_tags.py
"""
from __future__ import annotations

import re
import xml.etree.ElementTree as ET
from pathlib import Path

RE_FILL = re.compile(r"^FILLQUN(\d{2})$")
RE_USE = re.compile(r"^USEQUN(\d{2})$")
RE_SUBF = re.compile(r"^SUBFILLQUN(\d{2})$")
RE_SUBU = re.compile(r"^SUBUSEQUN(\d{2})$")


def local(tag: str) -> str:
    return tag.split("}", 1)[1] if "}" in tag else tag


def data_object_name(data_el: ET.Element) -> str | None:
    for name_el in data_el.iter():
        if local(name_el.tag) == "Name" and name_el.text:
            return name_el.text.strip()
    return None


def data_start_xy(data_el: ET.Element) -> tuple[int, int] | None:
    for start in data_el.iter():
        if local(start.tag) != "Start":
            continue
        try:
            return int(start.get("X", "0")), int(start.get("Y", "0"))
        except ValueError:
            return None
    return None


def is_g_text(text_el: ET.Element) -> bool:
    if local(text_el.tag) != "Text":
        return False
    for td in text_el.iter():
        if local(td.tag) == "TextData" and td.text and td.text.strip() == "g":
            return True
    return False


def text_start_xy(text_el: ET.Element) -> tuple[int, int] | None:
    for start in text_el.iter():
        if local(start.tag) != "Start":
            continue
        try:
            return int(start.get("X", "0")), int(start.get("Y", "0"))
        except ValueError:
            return None
    return None


def set_text_name_clear(text_el: ET.Element, new_name: str) -> None:
    for obj in text_el.iter():
        if local(obj.tag) != "Object":
            continue
        for name_el in obj.iter():
            if local(name_el.tag) == "Name":
                name_el.text = new_name
                break
        break
    for td in text_el.iter():
        if local(td.tag) == "TextData":
            td.text = ""
            break


def collect_pairs(root: ET.Element, fill_re: re.Pattern, use_re: re.Pattern) -> dict[tuple[int, str], tuple[int, int]]:
    """(y, nn) -> (fill_x, use_x)"""
    fills: dict[tuple[int, str], int] = {}
    uses: dict[tuple[int, str], int] = {}
    for data in root.iter():
        if local(data.tag) != "Data":
            continue
        name = data_object_name(data)
        if not name:
            continue
        xy = data_start_xy(data)
        if xy is None:
            continue
        fx, fy = xy
        mf = fill_re.match(name)
        if mf:
            fills[(fy, mf.group(1))] = fx
        mu = use_re.match(name)
        if mu:
            uses[(fy, mu.group(1))] = fx
    out: dict[tuple[int, str], tuple[int, int]] = {}
    for key, fxx in fills.items():
        if key not in uses:
            continue
        out[key] = (fxx, uses[key])
    return out


def relabel_g_between(
    root: ET.Element,
    pairs: dict[tuple[int, str], tuple[int, int]],
    name_fmt: str,
    used: set[int],
) -> int:
    g_texts: list[tuple[int, int, ET.Element]] = []
    for text_el in root.iter():
        if not is_g_text(text_el):
            continue
        xy = text_start_xy(text_el)
        if xy is None:
            continue
        gx, gy = xy
        g_texts.append((gx, gy, text_el))

    changed = 0
    for (y, nn), (fill_x, use_x) in sorted(pairs.items()):
        candidates = [
            (gx, el)
            for gx, gy, el in g_texts
            if gy == y and fill_x < gx < use_x and id(el) not in used
        ]
        if not candidates:
            continue
        gx, el = min(candidates, key=lambda t: t[0])
        set_text_name_clear(el, name_fmt.format(nn=nn))
        used.add(id(el))
        changed += 1
    return changed


def patch_file(path: Path) -> int:
    tree = ET.parse(path)
    root = tree.getroot()
    used: set[int] = set()
    n = 0
    main_pairs = collect_pairs(root, RE_FILL, RE_USE)
    n += relabel_g_between(root, main_pairs, "FILLQUNUNIT{nn}", used)
    sub_pairs = collect_pairs(root, RE_SUBF, RE_SUBU)
    n += relabel_g_between(root, sub_pairs, "SUBFILLQUNUNIT{nn}", used)
    if n:
        tree.write(path, encoding="utf-16", xml_declaration=True)
    return n


def main() -> None:
    root = Path(__file__).resolve().parents[1]
    for name in (
        "生産指示書_キャベツとウィンナーのソティ.rxz",
        "生産指示書_がんもの炊き合わせ.rxz",
        "生産指示書_ホイコーロー.rxz",
    ):
        p = root / "static" / "templates" / name
        c = patch_file(p)
        print(f"{name}: {c} fill-column g -> FILLQUNUNIT/SUBFILLQUNUNIT")


if __name__ == "__main__":
    main()
