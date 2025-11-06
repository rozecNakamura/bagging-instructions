from sqlalchemy.orm import Session
from app.repositories import daystoc_repository


def get_item_stock(
    db: Session,
    fctcd: str,
    deptcd: str,
    itemgr: str,
    itemcd: str,
    base_date: str = None,
) -> float:
    """
    品目の在庫数を取得（サービス層）

    Args:
        db: データベースセッション
        fctcd: 工場コード
        deptcd: 部門コード
        itemgr: 品目グループ
        itemcd: 品目コード
        base_date: 基準日（YYYYMMDD形式）

    Returns:
        float: 在庫数量
    """
    return daystoc_repository.get_latest_stock(
        db, fctcd, deptcd, itemgr, itemcd, base_date
    )

