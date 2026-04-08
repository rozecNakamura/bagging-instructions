"""C# BaggingBagCountResolver / ItemCodeKind に揃えた袋数上書き条件（参考実装）。"""

from __future__ import annotations

_COUNT_SUBSTRINGS = ("個", "ケ", "ヶ", "箇", "コ")


def is_liquid_item(itemcd: str | None) -> bool:
    return bool(itemcd and str(itemcd).startswith("55"))


def finished_good_uses_count_rounding(item) -> bool:
    if item is None:
        return True
    uni = getattr(getattr(item, "uni", None), "uninm", None)
    u = (uni or "").strip()
    if not u:
        return True
    return any(t in u for t in _COUNT_SUBSTRINGS)


def use_floor_bags_from_car0(item) -> bool:
    if item is None:
        return False
    cd = getattr(item, "itemcd", None) or ""
    if is_liquid_item(cd):
        return False
    if finished_good_uses_count_rounding(item):
        return False
    return True
