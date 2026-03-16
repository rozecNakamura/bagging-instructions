#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""cstmeat.csv から cstmeat テーブル用 INSERT 文を生成する"""

import csv

def escape_sql(s: str) -> str:
    """SQL 用にエスケープ（シングルクォートを二重に）"""
    if s is None:
        return "''"
    return "'" + str(s).replace("'", "''") + "'"

INPUT_CSV = "cstmeat.csv"
OUTPUT_SQL = "cstmeat_insert.sql"
COLS = 20  # info00 .. info19
BATCH = 500  # 1回のINSERTの行数

def main():
    rows = []
    for enc in ("cp932", "utf-8", "utf-8-sig"):
        try:
            with open(INPUT_CSV, "r", encoding=enc, newline="") as f:
                reader = csv.reader(f)
                for row in reader:
                    cells = [cell.strip() if cell else "" for cell in row[:COLS]]
                    while len(cells) < COLS:
                        cells.append("")
                    rows.append(cells)
            break
        except UnicodeDecodeError:
            rows = []
            continue
    if not rows:
        with open(INPUT_CSV, "r", encoding="utf-8", errors="replace", newline="") as f:
            reader = csv.reader(f)
            for row in reader:
                cells = [cell.strip() if cell else "" for cell in row[:COLS]]
                while len(cells) < COLS:
                    cells.append("")
                rows.append(cells)

    with open(OUTPUT_SQL, "w", encoding="utf-8") as out:
        out.write("-- cstmeat テーブルへの INSERT（cstmeat.csv の a〜t 列）\n\n")
        for i in range(0, len(rows), BATCH):
            batch_rows = rows[i : i + BATCH]
            values_list = []
            for cells in batch_rows:
                escaped = [escape_sql(c) for c in cells]
                values_list.append("(" + ",".join(escaped) + ")")
            out.write(
                "INSERT INTO cstmeat (info00, info01, info02, info03, info04, info05, info06, info07, info08, info09, info10, info11, info12, info13, info14, info15, info16, info17, info18, info19)\n"
            )
            out.write("VALUES\n")
            out.write(",\n".join(values_list))
            out.write(";\n\n")

    print(f"Generated {OUTPUT_SQL}: {len(rows)} rows in {(len(rows) + BATCH - 1) // BATCH} INSERT(s)")

if __name__ == "__main__":
    main()
