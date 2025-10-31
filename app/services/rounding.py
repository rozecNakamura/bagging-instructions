import math
from app.models.item import Item


def round_up_quantity(quantity: float, item: Item | None) -> float:
    """
    個数単位の品目を整数に切り上げ

    Args:
        quantity: 受注量
        item: 品目マスタ（Noneの場合は切り上げなし）

    Returns:
        切り上げ後の数量
    """
    if item is None:
        return quantity

    # TODO: itemに個数単位フラグがある場合は対応
    # 仮実装: jouniカラムが"個"の場合は切り上げ
    if hasattr(item, "jouni") and item.jouni == "個":
        return math.ceil(quantity)

    return quantity
