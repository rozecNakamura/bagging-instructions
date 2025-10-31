from sqlalchemy.orm import Session
from sqlalchemy import and_
from typing import Optional, List
from app.models.cusmcd import Cusmcd


def find_by_code(
    db: Session, merfctcd: str, cuscd: str, cusitemcd: str
) -> Optional[Cusmcd]:
    """得意先品目変換マスタを取得"""
    return (
        db.query(Cusmcd)
        .filter(
            and_(
                Cusmcd.merfctcd == merfctcd,
                Cusmcd.cuscd == cuscd,
                Cusmcd.cusitemcd == cusitemcd,
            )
        )
        .first()
    )


def find_by_customer(db: Session, merfctcd: str, cuscd: str) -> List[Cusmcd]:
    """得意先の品目変換マスタを全て取得"""
    return (
        db.query(Cusmcd)
        .filter(and_(Cusmcd.merfctcd == merfctcd, Cusmcd.cuscd == cuscd))
        .all()
    )


def find_by_item(
    db: Session, fctcd: str, deptcd: str, itemgr: str, itemcd: str
) -> List[Cusmcd]:
    """自社品目から得意先品目変換マスタを取得"""
    return (
        db.query(Cusmcd)
        .filter(
            and_(
                Cusmcd.fctcd == fctcd,
                Cusmcd.deptcd == deptcd,
                Cusmcd.itemgr == itemgr,
                Cusmcd.itemcd == itemcd,
            )
        )
        .all()
    )


def find_active(db: Session, merfctcd: str, cuscd: str) -> List[Cusmcd]:
    """有効な得意先品目変換マスタを取得（廃止フラグが立っていない）"""
    return (
        db.query(Cusmcd)
        .filter(
            and_(
                Cusmcd.merfctcd == merfctcd,
                Cusmcd.cuscd == cuscd,
                Cusmcd.disusecusitem != "1",
            )
        )
        .all()
    )
