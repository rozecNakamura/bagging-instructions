"""
Turn static Text 'g' beside FILLQUNSUM / SUBFILLQUNSUM (充填量合計行) into
FILLQUNSUMUNIT / SUBFILLQUNSUMUNIT for Juice data merge.

Pairing: same row Y as the sum Data; static g with TextData 'g' strictly between
the fill-sum quantity field and the corresponding 現場使用量合計 quantity field
(rightmost such g on that row).

- FILLQUNSUM -> USEQUNSUM
- SUBFILLQUNSUM -> SUBUSEQUNSUM (がんも) or SUBUSEQUN11 (ホイコーロー)

Preserves UTF-16. Run from repo root:
  python scripts/relabel_rxz_fillqunsum_g_to_fillqunsumunit.py
"""
from __future__ import annotations

import xml.etree.ElementTree as ET
from pathlib import Path


def local(tag: str) -> str:
    return tag.split("}", 1)[1] if "}" in tag else tag


def data_object_name(data_el: ET.Element) -> str | None:
    for name_el in data_el.iter():
        if local(name_el.tag) == "Name" and name_el.text:
            return name_el.text.strip()
    return None


def data_qty_bounds(data_el: ET.Element) -> tuple[int, int, int] | None:
    """(x, y, width) from TextField/Text/Object Start + Text Size."""
    x = y = w = None
    for text in data_el.iter():
        if local(text.tag) != "Text":
            continue
        for obj in text.iter():
            if local(obj.tag) != "Object":
                continue
            start = None
            for ch in obj:
                if local(ch.tag) == "Start":
                    start = ch
                    break
            if start is None:
                continue
            try:
                x = int(start.get("X", "0"))
                y = int(start.get("Y", "0"))
            except ValueError:
                return None
            for sz in text:
                if local(sz.tag) == "Size":
                    try:
                        w = int(sz.get("Width", "0"))
                    except ValueError:
                        return None
                    break
            if w is not None:
                return x, y, w
    return None


def is_g_static_text(text_el: ET.Element) -> bool:
    if local(text_el.tag) != "Text":
        return False
    for td in text_el.iter():
        if local(td.tag) == "TextData" and td.text and td.text.strip() == "g":
            return True
    return False


def text_start_x(text_el: ET.Element) -> int | None:
    for start in text_el.iter():
        if local(start.tag) != "Start":
            continue
        try:
            return int(start.get("X", "0"))
        except ValueError:
            return None
    return None


def text_start_xy(text_el: ET.Element) -> tuple[int, int] | None:
    for start in text_el.iter():
        if local(start.tag) != "Start":
            continue
        try:
            return int(start.get("X", "0")), int(start.get("Y", "0"))
        except ValueError:
            return None
    return None


def set_text_object_name_clear_g(text_el: ET.Element, new_name: str) -> None:
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


def find_use_x(root: ET.Element, fill_name: str, fy: int) -> int | None:
    want: list[str]
    if fill_name == "FILLQUNSUM":
        want = ["USEQUNSUM"]
    elif fill_name == "SUBFILLQUNSUM":
        want = ["SUBUSEQUNSUM", "SUBUSEQUN11"]
    else:
        return None

    for target in want:
        for data in root.iter():
            if local(data.tag) != "Data":
                continue
            if data_object_name(data) != target:
                continue
            b = data_qty_bounds(data)
            if b is None:
                continue
            _, uy, _ = b
            if uy == fy:
                return b[0]
    return None


def patch_file(path: Path) -> int:
    tree = ET.parse(path)
    root = tree.getroot()
    changed = 0
    used_g: set[int] = set()

    g_cells: list[tuple[int, int, ET.Element]] = []
    for text_el in root.iter():
        if not is_g_static_text(text_el):
            continue
        xy = text_start_xy(text_el)
        if xy is None:
            continue
        g_cells.append((xy[0], xy[1], text_el))

    for data in root.iter():
        if local(data.tag) != "Data":
            continue
        name = data_object_name(data)
        if name not in ("FILLQUNSUM", "SUBFILLQUNSUM"):
            continue
        bounds = data_qty_bounds(data)
        if bounds is None:
            continue
        fx, fy, fw = bounds
        use_x = find_use_x(root, name, fy)
        if use_x is None:
            continue
        candidates = [
            (gx, el)
            for gx, gy, el in g_cells
            if gy == fy and fx < gx < use_x and id(el) not in used_g
        ]
        if not candidates:
            continue
        gx, el = max(candidates, key=lambda t: t[0])
        new_tag = "FILLQUNSUMUNIT" if name == "FILLQUNSUM" else "SUBFILLQUNSUMUNIT"
        set_text_object_name_clear_g(el, new_tag)
        used_g.add(id(el))
        changed += 1

    if changed:
        tree.write(path, encoding="utf-16", xml_declaration=True)
    return changed


def main() -> None:
    root = Path(__file__).resolve().parents[1]
    for name in (
        "生産指示書_キャベツとウィンナーのソティ.rxz",
        "生産指示書_がんもの炊き合わせ.rxz",
        "生産指示書_ホイコーロー.rxz",
    ):
        p = root / "static" / "templates" / name
        c = patch_file(p)
        print(f"{name}: {c} g -> FILLQUNSUMUNIT / SUBFILLQUNSUMUNIT")


if __name__ == "__main__":
    main()
