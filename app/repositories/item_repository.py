from sqlalchemy.orm import Session
from typing import Optional, List
from app.models.item import Item


def find_by_code(db: Session, item_cd: str) -> Optional[Item]:
    """品目コードで品目マスタを取得"""
    return db.query(Item).filter(Item.item_cd == item_cd).first()


def find_by_codes(db: Session, item_cds: List[str]) -> List[Item]:
    """品目コード配列で品目マスタを取得"""
    return db.query(Item).filter(Item.item_cd.in_(item_cds)).all()


def find_all(db: Session) -> List[Item]:
    """全品目を取得"""
    return db.query(Item).all()
