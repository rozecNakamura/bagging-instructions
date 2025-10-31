from sqlalchemy.orm import Session
from sqlalchemy import and_
from typing import Optional, List
from app.models.shpctr import Shpctr


def find_by_code(
    db: Session, fctcd: str, cuscd: str, shpctrcd: str
) -> Optional[Shpctr]:
    """納入場所コードで納入場所マスタを取得"""
    return (
        db.query(Shpctr)
        .filter(
            and_(
                Shpctr.fctcd == fctcd,
                Shpctr.cuscd == cuscd,
                Shpctr.shpctrcd == shpctrcd,
            )
        )
        .first()
    )


def find_by_codes(
    db: Session, fctcd: str, cuscd: str, shpctrcds: List[str]
) -> List[Shpctr]:
    """納入場所コード配列で納入場所マスタを取得"""
    return (
        db.query(Shpctr)
        .filter(
            and_(
                Shpctr.fctcd == fctcd,
                Shpctr.cuscd == cuscd,
                Shpctr.shpctrcd.in_(shpctrcds),
            )
        )
        .all()
    )


def find_by_customer(db: Session, fctcd: str, cuscd: str) -> List[Shpctr]:
    """得意先コードで納入場所を取得"""
    return (
        db.query(Shpctr)
        .filter(and_(Shpctr.fctcd == fctcd, Shpctr.cuscd == cuscd))
        .order_by(Shpctr.dispno)
        .all()
    )


def find_active(db: Session, fctcd: str, cuscd: str) -> List[Shpctr]:
    """有効な納入場所を取得（削除されていない、廃止フラグなし）"""
    return (
        db.query(Shpctr)
        .filter(
            and_(
                Shpctr.fctcd == fctcd,
                Shpctr.cuscd == cuscd,
                Shpctr.deldt.is_(None),
                Shpctr.disuseshpctr != "1",
            )
        )
        .order_by(Shpctr.dispno)
        .all()
    )


def find_by_line(db: Session, fctcd: str, linecd: str) -> List[Shpctr]:
    """ラインコードで納入場所を取得"""
    return (
        db.query(Shpctr)
        .filter(and_(Shpctr.fctcd == fctcd, Shpctr.linecd == linecd))
        .all()
    )


def find_all(db: Session, fctcd: str) -> List[Shpctr]:
    """全納入場所マスタを取得"""
    return db.query(Shpctr).filter(Shpctr.fctcd == fctcd).order_by(Shpctr.dispno).all()
