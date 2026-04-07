"""
Relabel static Text 'kg' cells for USEQUN**/SUBUSEQUN** rows to
USEQUNUNIT**/SUBUSEQUNUNIT** so JuicePdf can inject the ingredient unit.

Match rule: same row Y as the quantity Data field, and kg label X is to the
right of the quantity field (avoids wrong pairing when XML order differs).

Run from repo root:
  python scripts/relabel_rxz_kg_unit_columns.py
"""
from __future__ import annotations

import re
import xml.etree.ElementTree as ET
from pathlib import Path

RE_USEQUN = re.compile(r"^USEQUN(\d{2})$")
RE_SUBUSEQUN = re.compile(r"^SUBUSEQUN(\d{2})$")


def _find(el: ET.Element, path: str) -> ET.Element | None:
    r = el.find(path)
    if r is not None:
        return r
    return el.find(path.replace("/", "/{*}"))


def data_quantity_name(data_el: ET.Element) -> str | None:
    name_el = _find(data_el, ".//Object/Name")
    if name_el is None or not name_el.text:
        return None
    return name_el.text.strip()


def start_xy(container: ET.Element) -> tuple[int, int] | None:
    start = _find(container, ".//Object/Start")
    if start is None:
        return None
    try:
        return int(start.get("X", "0")), int(start.get("Y", "0"))
    except ValueError:
        return None


def text_kg_cell(text_el: ET.Element) -> bool:
    td = text_el.find("TextData")
    if td is None:
        td = text_el.find("{*}TextData")
    if td is None or td.text is None:
        return False
    return td.text.strip() == "kg"


def set_text_name_and_clear_kg(text_el: ET.Element, new_name: str) -> None:
    obj = text_el.find("Object")
    if obj is None:
        obj = text_el.find("{*}Object")
    if obj is None:
        return
    name_el = obj.find("Name")
    if name_el is None:
        name_el = obj.find("{*}Name")
    if name_el is not None:
        name_el.text = new_name
    td = text_el.find("TextData")
    if td is None:
        td = text_el.find("{*}TextData")
    if td is not None:
        td.text = ""


def collect_kg_text_cells(root: ET.Element) -> list[tuple[int, int, ET.Element]]:
    out: list[tuple[int, int, ET.Element]] = []
    for text_el in root.iter():
        if not text_el.tag.endswith("Text"):
            continue
        if not text_kg_cell(text_el):
            continue
        xy = start_xy(text_el)
        if xy is None:
            continue
        x, y = xy
        out.append((y, x, text_el))
    return out


def pick_kg_for_quantity_row(
    kg_cells: list[tuple[int, int, ET.Element]],
    qty_x: int,
    qty_y: int,
    assigned: set[int],
) -> ET.Element | None:
    """kg on same Y, strictly to the right of quantity field, not yet used."""
    candidates = [
        (kx, el)
        for ky, kx, el in kg_cells
        if ky == qty_y and kx > qty_x and id(el) not in assigned
    ]
    if not candidates:
        return None
    # Prefer closest X to the quantity column (leftmost kg right of qty)
    kx, el = min(candidates, key=lambda t: t[0])
    return el


def patch_file(path: Path) -> int:
    tree = ET.parse(path)
    root = tree.getroot()
    kg_cells = collect_kg_text_cells(root)
    assigned: set[int] = set()
    changed = 0

    for data_el in root.iter():
        if not data_el.tag.endswith("Data"):
            continue
        name = data_quantity_name(data_el)
        if not name:
            continue
        m_use = RE_USEQUN.match(name)
        m_sub = RE_SUBUSEQUN.match(name)
        if not m_use and not m_sub:
            continue
        xy = start_xy(data_el)
        if xy is None:
            continue
        qty_x, qty_y = xy
        kg_text = pick_kg_for_quantity_row(kg_cells, qty_x, qty_y, assigned)
        if kg_text is None:
            continue
        if m_use:
            new_name = f"USEQUNUNIT{m_use.group(1)}"
        else:
            new_name = f"SUBUSEQUNUNIT{m_sub.group(1)}"
        set_text_name_and_clear_kg(kg_text, new_name)
        assigned.add(id(kg_text))
        changed += 1

    if changed:
        tree.write(path, encoding="utf-16", xml_declaration=True)
    return changed


def main() -> None:
    root = Path(__file__).resolve().parents[1]
    files = [
        root / "static" / "templates" / "生産指示書_キャベツとウィンナーのソティ.rxz",
        root / "static" / "templates" / "生産指示書_がんもの炊き合わせ.rxz",
        root / "static" / "templates" / "生産指示書_ホイコーロー.rxz",
    ]
    for f in files:
        n = patch_file(f)
        print(f"OK: {n} cell(s) -> {f.name}")


if __name__ == "__main__":
    main()
