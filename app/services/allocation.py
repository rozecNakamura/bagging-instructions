from typing import List, Tuple
from app.models.jobord import Jobord
from app.models.item import Item


def calculate_standard_bags(quantity: float, kikunip: float) -> int:
    """
    規格袋数を計算

    Args:
        quantity: 総量
        kikunip: 規格量（1袋あたり）

    Returns:
        規格袋数
    """
    if kikunip <= 0:
        return 0
    return int(quantity // kikunip)


def calculate_irregular_quantity(quantity: float, kikunip: float) -> float:
    """
    規格外袋（端数）を計算

    Args:
        quantity: 総量
        kikunip: 規格量（1袋あたり）

    Returns:
        端数量
    """
    if kikunip <= 0:
        return quantity
    return quantity % kikunip
