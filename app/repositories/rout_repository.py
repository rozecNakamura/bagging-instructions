from sqlalchemy.orm import Session
from sqlalchemy import and_
from typing import List, Optional
from app.models.rout import Rout


def find_by_item(
    db: Session, fctcd: str, deptcd: str, itemgr: str, itemcd: str
) -> List[Rout]:
    """品目の工程手順を取得"""
    return (
        db.query(Rout)
        .filter(
            and_(
                Rout.fctcd == fctcd,
                Rout.deptcd == deptcd,
                Rout.itemgr == itemgr,
                Rout.itemcd == itemcd,
                Rout.deldt.is_(None),
            )
        )
        .order_by(Rout.routno)
        .all()
    )


def find_by_item_and_line(
    db: Session, fctcd: str, deptcd: str, itemgr: str, itemcd: str, linecd: str
) -> List[Rout]:
    """品目とラインで工程手順を取得"""
    return (
        db.query(Rout)
        .filter(
            and_(
                Rout.fctcd == fctcd,
                Rout.deptcd == deptcd,
                Rout.itemgr == itemgr,
                Rout.itemcd == itemcd,
                Rout.linecd == linecd,
                Rout.deldt.is_(None),
            )
        )
        .order_by(Rout.routno)
        .all()
    )


def find_by_route_no(
    db: Session,
    fctcd: str,
    deptcd: str,
    itemgr: str,
    itemcd: str,
    linecd: str,
    routno: int,
) -> Optional[Rout]:
    """工程番号で工程手順を取得"""
    return (
        db.query(Rout)
        .filter(
            and_(
                Rout.fctcd == fctcd,
                Rout.deptcd == deptcd,
                Rout.itemgr == itemgr,
                Rout.itemcd == itemcd,
                Rout.linecd == linecd,
                Rout.routno == routno,
                Rout.deldt.is_(None),
            )
        )
        .first()
    )


def find_by_process(db: Session, prccd: str) -> List[Rout]:
    """工程コードで工程手順を取得"""
    return db.query(Rout).filter(and_(Rout.prccd == prccd, Rout.deldt.is_(None))).all()


def find_active_routes(
    db: Session, fctcd: str, deptcd: str, itemgr: str, itemcd: str
) -> List[Rout]:
    """有効な工程手順を取得（削除されていない、有効フラグあり）"""
    return (
        db.query(Rout)
        .filter(
            and_(
                Rout.fctcd == fctcd,
                Rout.deptcd == deptcd,
                Rout.itemgr == itemgr,
                Rout.itemcd == itemcd,
                Rout.enrout == "1",
                Rout.deldt.is_(None),
            )
        )
        .order_by(Rout.routno)
        .all()
    )
