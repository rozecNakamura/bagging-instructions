from sqlalchemy.orm import Session
from sqlalchemy import and_
from typing import List, Optional
from datetime import date
from app.models.jobord import Jobord

def find_by_production_date_and_product(
    db: Session,
    production_date: date,
    product_code: str
) -> List[Jobord]:
    """製造日と品目コードで受注明細を検索"""
    return db.query(Jobord).filter(
        and_(
            Jobord.production_date == production_date,
            Jobord.product_code == product_code
        )
    ).all()

def find_by_ids(db: Session, ids: List[int]) -> List[Jobord]:
    """ID配列で受注明細を取得"""
    return db.query(Jobord).filter(Jobord.id.in_(ids)).all()

def find_by_id(db: Session, jobord_id: int) -> Optional[Jobord]:
    """IDで受注明細を取得"""
    return db.query(Jobord).filter(Jobord.id == jobord_id).first()

