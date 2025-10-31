from sqlalchemy.orm import Session
from sqlalchemy import and_
from typing import List
from app.models.mbom import Mbom


def find_by_parent_item(
    db: Session, pfctcd: str, pdeptcd: str, pitemgr: str, pitemcd: str
) -> List[Mbom]:
    """親品目からBOMを取得"""
    return (
        db.query(Mbom)
        .filter(
            and_(
                Mbom.pfctcd == pfctcd,
                Mbom.pdeptcd == pdeptcd,
                Mbom.pitemgr == pitemgr,
                Mbom.pitemcd == pitemcd,
            )
        )
        .all()
    )


def find_by_parent_item_with_date(
    db: Session, pfctcd: str, pdeptcd: str, pitemgr: str, pitemcd: str, target_date: str
) -> List[Mbom]:
    """親品目と日付でBOMを取得（有効期間内）"""
    return (
        db.query(Mbom)
        .filter(
            and_(
                Mbom.pfctcd == pfctcd,
                Mbom.pdeptcd == pdeptcd,
                Mbom.pitemgr == pitemgr,
                Mbom.pitemcd == pitemcd,
                Mbom.stadt <= target_date,
                Mbom.enddt >= target_date,
            )
        )
        .all()
    )


def find_by_child_item(
    db: Session, cfctcd: str, cdeptcd: str, citemgr: str, citemcd: str
) -> List[Mbom]:
    """子品目からBOMを取得（どの親品目で使われているか）"""
    return (
        db.query(Mbom)
        .filter(
            and_(
                Mbom.cfctcd == cfctcd,
                Mbom.cdeptcd == cdeptcd,
                Mbom.citemgr == citemgr,
                Mbom.citemcd == citemcd,
            )
        )
        .all()
    )


def find_by_parent_with_route(
    db: Session, pfctcd: str, pdeptcd: str, pitemgr: str, pitemcd: str, proutno: int
) -> List[Mbom]:
    """親品目と工程番号でBOMを取得"""
    return (
        db.query(Mbom)
        .filter(
            and_(
                Mbom.pfctcd == pfctcd,
                Mbom.pdeptcd == pdeptcd,
                Mbom.pitemgr == pitemgr,
                Mbom.pitemcd == pitemcd,
                Mbom.proutno == proutno,
            )
        )
        .all()
    )


def find_active_recipes(
    db: Session, pfctcd: str, pdeptcd: str, pitemgr: str, pitemcd: str, target_date: str
) -> List[Mbom]:
    """有効なレシピを取得（計画実行対象かつ有効期間内）"""
    return (
        db.query(Mbom)
        .filter(
            and_(
                Mbom.pfctcd == pfctcd,
                Mbom.pdeptcd == pdeptcd,
                Mbom.pitemgr == pitemgr,
                Mbom.pitemcd == pitemcd,
                Mbom.planruntgtflg == "1",
                Mbom.stadt <= target_date,
                Mbom.enddt >= target_date,
            )
        )
        .all()
    )
