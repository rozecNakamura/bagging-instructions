from datetime import date, timedelta
from typing import List
from app.schemas.label import LabelItem
from app.models.product import Product

def calculate_expiry_date(eating_date: date, days_before: int = 3) -> date:
    """賞味期限の計算（喫食日の3日後など）"""
    return eating_date + timedelta(days=days_before)

def generate_standard_labels(
    product: Product,
    eating_date: date,
    eating_time: str,
    standard_bags: int
) -> List[LabelItem]:
    """
    規格品ラベルを生成
    
    Args:
        product: 品目マスタ
        eating_date: 喫食日
        eating_time: 喫食時間
        standard_bags: 規格袋数
    
    Returns:
        規格品ラベルリスト
    """
    if standard_bags <= 0:
        return []
    
    return [LabelItem(
        label_type="standard",
        eating_date=eating_date,
        eating_time=eating_time,
        product_code=product.product_code,
        product_name=product.product_name,
        expiry_date=calculate_expiry_date(eating_date),
        sterilization_temp=product.sterilization_temp,
        standard_quantity=product.standard_quantity,
        count=standard_bags
    )]

def generate_irregular_labels(
    product: Product,
    eating_date: date,
    eating_time: str,
    facility_name: str,
    irregular_quantity: float
) -> List[LabelItem]:
    """
    端数ラベルを生成
    
    Args:
        product: 品目マスタ
        eating_date: 喫食日
        eating_time: 喫食時間
        facility_name: 施設名
        irregular_quantity: 端数量
    
    Returns:
        端数ラベルリスト
    """
    if irregular_quantity <= 0:
        return []
    
    return [LabelItem(
        label_type="irregular",
        eating_date=eating_date,
        eating_time=eating_time,
        product_code=product.product_code,
        product_name=product.product_name,
        expiry_date=calculate_expiry_date(eating_date),
        sterilization_temp=product.sterilization_temp,
        facility_name=facility_name,
        irregular_quantity=irregular_quantity,
        count=1
    )]

