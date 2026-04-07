"""
Insert USEQUNSUM Data field after FILLQUNSUM on the main-material total row
(Y=4592133 がんも, Y=4365361 ホイコーロー). Bumps DrawSeq on following Labels on that row.

Preserves UTF-16 LE XML. Run from repo root:
  python scripts/insert_usequnsum_main_row.py
"""
from __future__ import annotations

import xml.etree.ElementTree as ET
from pathlib import Path


def local_name(tag: str) -> str:
    if "}" in tag:
        return tag.split("}", 1)[1]
    return tag


def elem_name(el: ET.Element) -> str | None:
    for e in el.iter():
        if local_name(e.tag) == "Name" and e.text:
            return e.text.strip()
    return None


def start_y(el: ET.Element) -> str | None:
    for e in el.iter():
        if local_name(e.tag) == "Start":
            return e.get("Y")
    return None


def parent_and_index(root: ET.Element, target: ET.Element) -> tuple[ET.Element, int] | None:
    for p in root.iter():
        children = list(p)
        for i, c in enumerate(children):
            if c is target:
                return p, i
    return None


def bump_drawseq_on_row(objects_el: ET.Element, row_y: str, min_seq: int, delta: int) -> None:
    for el in objects_el.iter():
        if local_name(el.tag) not in ("Text", "Data"):
            continue
        start = None
        for sub in el.iter():
            if local_name(sub.tag) == "Start":
                start = sub
                break
        if start is None or start.get("Y") != row_y:
            continue
        for sub in el.iter():
            if local_name(sub.tag) == "DrawSeq" and sub.text and sub.text.isdigit():
                n = int(sub.text)
                if n >= min_seq:
                    sub.text = str(n + delta)


def make_usequnsum_data(draw_seq: int, y: str) -> ET.Element:
    """Minimal clone of USEQUN00-style Data block."""
    ns = ""
    d = ET.Element(f"{ns}Data")
    fmt = ET.SubElement(d, f"{ns}Format")
    fmt.text = "XXXXXX"
    for tag in ("ReplaceCharacter", "BeforeString", "AfterString"):
        t = ET.SubElement(d, f"{ns}{tag}")
        t.text = ""
    comma = ET.SubElement(d, f"{ns}Comma")
    comma.text = "false"
    tf = ET.SubElement(d, f"{ns}TextField")
    txt = ET.SubElement(tf, f"{ns}Text")
    obj = ET.SubElement(txt, f"{ns}Object")
    ET.SubElement(obj, f"{ns}Name").text = "USEQUNSUM"
    ET.SubElement(obj, f"{ns}SeqNo").text = "0"
    ET.SubElement(obj, f"{ns}Transparent").text = "false"
    ET.SubElement(obj, f"{ns}BackColor").text = "ffffffff"
    ET.SubElement(obj, f"{ns}DrawSeq").text = str(draw_seq)
    ET.SubElement(obj, f"{ns}Various").text = "0"
    ET.SubElement(obj, f"{ns}Visible").text = "true"
    ET.SubElement(obj, f"{ns}EndCap").text = "0"
    ET.SubElement(obj, f"{ns}Join").text = "0"
    ET.SubElement(obj, f"{ns}Lock").text = "false"
    st = ET.SubElement(obj, f"{ns}Start")
    st.set("X", "7249204")
    st.set("Y", y)
    for tag, val in [
        ("Direction", "1"),
        ("Angle", "0"),
        ("Alignment", "2"),
        ("LinePitch", "0"),
        ("Wordwrap", "false"),
        ("Kinsoku", "false"),
    ]:
        ET.SubElement(txt, f"{ns}{tag}").text = val
    sz = ET.SubElement(txt, f"{ns}Size")
    sz.set("Width", "1020474")
    sz.set("Height", "283465")
    fh = ET.SubElement(txt, f"{ns}FontHankaku")
    ET.SubElement(fh, f"{ns}FontNo").text = "0"
    fs = ET.SubElement(fh, f"{ns}FontSize")
    fs.set("Width", "0")
    fs.set("Height", "1100")
    ET.SubElement(fh, f"{ns}FontPitch").text = "110000"
    fz = ET.SubElement(txt, f"{ns}FontZenkaku")
    ET.SubElement(fz, f"{ns}FontNo").text = "0"
    fs2 = ET.SubElement(fz, f"{ns}FontSize")
    fs2.set("Width", "0")
    fs2.set("Height", "1100")
    ET.SubElement(fz, f"{ns}FontPitch").text = "220000"
    for tag, val in [
        ("CharacterRatio", "0"),
        ("Bold", "false"),
        ("Italic", "false"),
        ("TextColor", "ff000000"),
        ("UnderLine", "0"),
        ("UnderLineColor", "ff000000"),
        ("DeleteLine", "0"),
        ("DeleteLineColor", "ff000000"),
        ("FillPattern", "0"),
        ("FillColor", "ff000000"),
        ("LineStyle", "1"),
        ("LineWidth", "14173"),
        ("LineColor", "ff000000"),
        ("Frame", "true"),
    ]:
        ET.SubElement(txt, f"{ns}{tag}").text = val
    mg = ET.SubElement(txt, f"{ns}Margin")
    mg.set("Left", "56693")
    mg.set("Top", "56693")
    mg.set("Right", "56693")
    mg.set("Bottom", "56693")
    ET.SubElement(txt, f"{ns}TextData").text = ""
    ET.SubElement(txt, f"{ns}DefaultPitch").text = "true"
    ET.SubElement(txt, f"{ns}Reverse").text = "false"
    ET.SubElement(txt, f"{ns}AutoLineFeed").text = "false"
    ET.SubElement(txt, f"{ns}AlignVertical").text = "2"
    ET.SubElement(txt, f"{ns}TextEffects").text = "0"
    ET.SubElement(txt, f"{ns}ShrinkToFit").text = "true"
    fld = ET.SubElement(tf, f"{ns}Field")
    ET.SubElement(fld, f"{ns}FieldNo").text = "0"
    ET.SubElement(fld, f"{ns}URL").text = ""
    ET.SubElement(fld, f"{ns}Expire").text = "0"
    return d


def patch_file(path: Path, row_y: str, fill_draw_seq: int) -> bool:
    tree = ET.parse(path)
    root = tree.getroot()
    fill_el = None
    for data in root.iter():
        if local_name(data.tag) != "Data":
            continue
        if elem_name(data) != "FILLQUNSUM":
            continue
        if start_y(data) != row_y:
            continue
        fill_el = data
        break
    if fill_el is None:
        print(f"SKIP: no FILLQUNSUM at Y={row_y} in {path.name}")
        return False
    pin = parent_and_index(root, fill_el)
    if pin is None:
        return False
    parent, idx = pin
    # Already patched?
    for c in list(parent):
        if local_name(c.tag) == "Data" and elem_name(c) == "USEQUNSUM":
            sy = start_y(c)
            if sy == row_y:
                print(f"OK (already): {path.name}")
                return False
    objects_el = parent
    use_seq = fill_draw_seq + 1
    bump_drawseq_on_row(objects_el, row_y, use_seq, 1)
    new_data = make_usequnsum_data(use_seq, row_y)
    parent.insert(idx + 1, new_data)
    tree.write(path, encoding="utf-16", xml_declaration=True)
    print(f"Patched: {path.name}")
    return True


def main() -> None:
    root = Path(__file__).resolve().parents[1]
    patch_file(root / "static" / "templates" / "生産指示書_がんもの炊き合わせ.rxz", "4592133", 88)
    patch_file(root / "static" / "templates" / "生産指示書_ホイコーロー.rxz", "4365361", 116)


if __name__ == "__main__":
    main()
