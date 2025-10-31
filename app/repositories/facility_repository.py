from sqlalchemy.orm import Session
from typing import Optional, List
from app.models.facility import Facility

def find_by_code(db: Session, facility_code: str) -> Optional[Facility]:
    """施設コードで施設マスタを取得"""
    return db.query(Facility).filter(Facility.facility_code == facility_code).first()

def find_by_codes(db: Session, facility_codes: List[str]) -> List[Facility]:
    """施設コード配列で施設マスタを取得"""
    return db.query(Facility).filter(Facility.facility_code.in_(facility_codes)).all()

