from sqlalchemy.orm import Session
from sqlalchemy import and_
from typing import Optional
from datetime import date
from app.models.production_input import ProductionInput

def find_by_date_and_product(
    db: Session,
    production_date: date,
    product_code: str
) -> Optional[ProductionInput]:
    """製造日と品目コードで完成量を取得"""
    return db.query(ProductionInput).filter(
        and_(
            ProductionInput.production_date == production_date,
            ProductionInput.product_code == product_code
        )
    ).first()

