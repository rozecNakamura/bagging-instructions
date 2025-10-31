from sqlalchemy.orm import Session
from typing import Optional, List
from app.models.product import Product

def find_by_code(db: Session, product_code: str) -> Optional[Product]:
    """品目コードで品目マスタを取得"""
    return db.query(Product).filter(Product.product_code == product_code).first()

def find_by_codes(db: Session, product_codes: List[str]) -> List[Product]:
    """品目コード配列で品目マスタを取得"""
    return db.query(Product).filter(Product.product_code.in_(product_codes)).all()

