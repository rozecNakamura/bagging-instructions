from typing import List, Tuple
from app.models.jobord import Jobord
from app.models.product import Product

def allocate_production(
    jobords: List[Jobord],
    actual_quantity: float,
    product: Product
) -> List[Tuple[Jobord, float]]:
    """
    完成量を受注量に応じて按分
    
    Args:
        jobords: 受注明細リスト
        actual_quantity: 実際の完成量
        product: 品目マスタ
    
    Returns:
        (受注明細, 按分後の量) のタプルリスト
    """
    total_order = sum(j.order_quantity for j in jobords)
    
    if total_order == 0:
        return [(j, 0.0) for j in jobords]
    
    allocated = []
    for jobord in jobords:
        ratio = jobord.order_quantity / total_order
        allocated_qty = actual_quantity * ratio
        allocated.append((jobord, allocated_qty))
    
    return allocated

def calculate_standard_bags(quantity: float, standard_quantity: float) -> int:
    """
    規格袋数を計算
    
    Args:
        quantity: 総量
        standard_quantity: 規格量（1袋あたり）
    
    Returns:
        規格袋数
    """
    if standard_quantity <= 0:
        return 0
    return int(quantity // standard_quantity)

def calculate_irregular_quantity(quantity: float, standard_quantity: float) -> float:
    """
    規格外袋（端数）を計算
    
    Args:
        quantity: 総量
        standard_quantity: 規格量（1袋あたり）
    
    Returns:
        端数量
    """
    if standard_quantity <= 0:
        return quantity
    return quantity % standard_quantity

