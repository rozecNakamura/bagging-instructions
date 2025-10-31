from sqlalchemy.orm import Session
from sqlalchemy import and_
from typing import Optional, List
from app.models.cus import Cus


def find_by_code(db: Session, fctcd: str, cuscd: str) -> Optional[Cus]:
    """得意先コードで得意先マスタを取得"""
    return db.query(Cus).filter(and_(Cus.fctcd == fctcd, Cus.cuscd == cuscd)).first()


def find_by_codes(db: Session, fctcd: str, cuscds: List[str]) -> List[Cus]:
    """得意先コード配列で得意先マスタを取得"""
    return db.query(Cus).filter(and_(Cus.fctcd == fctcd, Cus.cuscd.in_(cuscds))).all()


def find_all(db: Session, fctcd: str) -> List[Cus]:
    """全得意先マスタを取得"""
    return db.query(Cus).filter(Cus.fctcd == fctcd).all()


def find_active(db: Session, fctcd: str) -> List[Cus]:
    """有効な得意先マスタを取得（削除日がnull）"""
    return db.query(Cus).filter(and_(Cus.fctcd == fctcd, Cus.deldt.is_(None))).all()
