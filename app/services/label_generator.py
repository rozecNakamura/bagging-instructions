from datetime import datetime, timedelta
from typing import List
from app.schemas.label import LabelItem
from app.models.item import Item


def calculate_expiry_date(delvedt: str, days_after: int = 3) -> str:
    """
    賞味期限の計算（納品日の3日後など）

    Args:
        delvedt: 納品日(YYYYMMDD形式)
        days_after: 追加日数

    Returns:
        賞味期限(YYYYMMDD形式)
    """
    try:
        date_obj = datetime.strptime(delvedt, "%Y%m%d")
        expiry = date_obj + timedelta(days=days_after)
        return expiry.strftime("%Y%m%d")
    except:
        return ""


def generate_standard_labels(
    item: Item, delvedt: str, shptm: str, standard_bags: int
) -> List[LabelItem]:
    """
    規格品ラベルを生成

    Args:
        item: 品目マスタ
        delvedt: 納品日(YYYYMMDD)
        shptm: 出荷時刻
        standard_bags: 規格袋数

    Returns:
        規格品ラベルリスト
    """
    if standard_bags <= 0:
        return []

    # TODO: itemのkikunip（規格量）を使用
    kikunip = getattr(item, "kikunip", None)

    return [
        LabelItem(
            label_type="standard",
            delvedt=delvedt,
            shptm=shptm,
            itemcd=item.itemcd or "",
            itemnm=item.itemnm or "",
            expiry_date=calculate_expiry_date(delvedt),
            strtemp=item.strtemp if hasattr(item, "strtemp") else None,
            kikunip=float(kikunip) if kikunip else None,
            count=standard_bags,
        )
    ]


def generate_irregular_labels(
    item: Item, delvedt: str, shptm: str, shpctrnm: str, irregular_quantity: float
) -> List[LabelItem]:
    """
    端数ラベルを生成

    Args:
        item: 品目マスタ
        delvedt: 納品日(YYYYMMDD)
        shptm: 出荷時刻
        shpctrnm: 納入場所名称
        irregular_quantity: 端数量

    Returns:
        端数ラベルリスト
    """
    if irregular_quantity <= 0:
        return []

    return [
        LabelItem(
            label_type="irregular",
            delvedt=delvedt,
            shptm=shptm,
            itemcd=item.itemcd or "",
            itemnm=item.itemnm or "",
            expiry_date=calculate_expiry_date(delvedt),
            strtemp=item.strtemp if hasattr(item, "strtemp") else None,
            shpctrnm=shpctrnm,
            irregular_quantity=irregular_quantity,
            count=1,
        )
    ]
