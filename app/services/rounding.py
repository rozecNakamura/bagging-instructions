import math
from app.models.product import Product

def round_up_quantity(quantity: float, product: Product) -> float:
    """
    個数単位の品目を整数に切り上げ
    
    Args:
        quantity: 受注量
        product: 品目マスタ
    
    Returns:
        切り上げ後の数量
    """
    if product.is_count_unit:
        return math.ceil(quantity)
    return quantity

def recalculate_seasoning(base_quantity: float, product: Product) -> float:
    """
    調味液量の再計算
    
    Args:
        base_quantity: 切り上げ後の基本量
        product: 品目マスタ
    
    Returns:
        再計算後の調味液量
    """
    if product.seasoning_rate:
        return base_quantity * product.seasoning_rate
    return 0.0

