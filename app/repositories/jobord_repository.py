from sqlalchemy.orm import Session
from sqlalchemy import and_, or_
from typing import Optional, List
from datetime import datetime
from app.models.jobord import Jobord


def find_by_order_no(
    db: Session, merfctcd: str, jobordno: str, jobordsno: int
) -> Optional[Jobord]:
    """受注番号・明細番号で受注明細を取得"""
    return (
        db.query(Jobord)
        .filter(
            and_(
                Jobord.merfctcd == merfctcd,
                Jobord.jobordno == jobordno,
                Jobord.jobordsno == jobordsno,
            )
        )
        .first()
    )


def find_by_customer(db: Session, merfctcd: str, cuscd: str) -> List[Jobord]:
    """得意先の受注明細を取得"""
    return (
        db.query(Jobord)
        .filter(and_(Jobord.merfctcd == merfctcd, Jobord.cuscd == cuscd))
        .all()
    )


def find_by_delivery_date(db: Session, merfctcd: str, delvedt: str) -> List[Jobord]:
    """納品日で受注明細を取得"""
    return (
        db.query(Jobord)
        .filter(and_(Jobord.merfctcd == merfctcd, Jobord.delvedt == delvedt))
        .all()
    )


def find_by_production_date(
    db: Session, fctcd: str, deptcd: str, itemgr: str, prddt: str
) -> List[Jobord]:
    """製造日で受注明細を取得"""
    return (
        db.query(Jobord)
        .filter(
            and_(
                Jobord.fctcd == fctcd,
                Jobord.deptcd == deptcd,
                Jobord.itemgr == itemgr,
                Jobord.prddt == prddt,
            )
        )
        .all()
    )


def find_by_item(
    db: Session, fctcd: str, deptcd: str, itemgr: str, itemcd: str
) -> List[Jobord]:
    """品目の受注明細を取得"""
    return (
        db.query(Jobord)
        .filter(
            and_(
                Jobord.fctcd == fctcd,
                Jobord.deptcd == deptcd,
                Jobord.itemgr == itemgr,
                Jobord.itemcd == itemcd,
            )
        )
        .all()
    )


def find_unshipped(db: Session, merfctcd: str) -> List[Jobord]:
    """未出荷の受注明細を取得"""
    return (
        db.query(Jobord)
        .filter(
            and_(
                Jobord.merfctcd == merfctcd,
                or_(Jobord.shpsts == "", Jobord.shpsts == "0", Jobord.shpsts.is_(None)),
            )
        )
        .all()
    )


def find_by_scheduled_ship_date(
    db: Session, merfctcd: str, schdshpdt: str
) -> List[Jobord]:
    """予定出荷日で受注明細を取得"""
    return (
        db.query(Jobord)
        .filter(and_(Jobord.merfctcd == merfctcd, Jobord.schdshpdt == schdshpdt))
        .all()
    )


def find_by_date_range(
    db: Session, merfctcd: str, start_date: str, end_date: str
) -> List[Jobord]:
    """日付範囲で受注明細を取得"""
    return (
        db.query(Jobord)
        .filter(
            and_(
                Jobord.merfctcd == merfctcd,
                Jobord.delvedt >= start_date,
                Jobord.delvedt <= end_date,
            )
        )
        .order_by(Jobord.delvedt, Jobord.cuscd, Jobord.jobordno)
        .all()
    )


def find_by_prkeys(db: Session, prkeys: List[int]) -> List[Jobord]:
    """プライマリキー配列で受注明細を取得"""
    return db.query(Jobord).filter(Jobord.prkey.in_(prkeys)).all()


def find_by_prddt_and_itemcd(
    db: Session,
    prddt: str = None,
    itemcd: str = None,
    fctcd: str = None,
    deptcd: str = None,
    itemgr: str = None,
) -> List[Jobord]:
    """
    製造日と品目コードで受注明細を取得（どちらか片方でも可）

    Args:
        db: DBセッション
        prddt: 製造日(YYYYMMDD形式) - オプション
        itemcd: 品目コード - オプション
        fctcd: 工場コード（オプション）
        deptcd: 部門コード（オプション）
        itemgr: 品目グループ（オプション）

    Returns:
        受注明細リスト
    """
    filters = []

    # 製造日が指定されている場合
    if prddt:
        filters.append(Jobord.prddt == prddt)

    # 品目コードが指定されている場合
    if itemcd:
        filters.append(Jobord.itemcd == itemcd)

    # その他のオプションフィルター
    if fctcd:
        filters.append(Jobord.fctcd == fctcd)
    if deptcd:
        filters.append(Jobord.deptcd == deptcd)
    if itemgr:
        filters.append(Jobord.itemgr == itemgr)

    # フィルターが1つもない場合は空リストを返す
    if not filters:
        return []

    return db.query(Jobord).filter(and_(*filters)).all()
