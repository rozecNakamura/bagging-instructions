from sqlalchemy.orm import Session
from sqlalchemy import func
from typing import Optional
from datetime import date
from app.models.daystoc import Daystoc
import logging

logger = logging.getLogger(__name__)


def get_latest_stock(
    db: Session,
    fctcd: str,
    deptcd: str,
    itemgr: str,
    itemcd: str,
    base_date: str = None,
) -> float:
    """
    指定品目の最新在庫数を取得

    Args:
        db: データベースセッション
        fctcd: 工場コード
        deptcd: 部門コード
        itemgr: 品目グループ
        itemcd: 品目コード
        base_date: 基準日（YYYYMMDD形式）、Noneの場合は当日

    Returns:
        float: 合算された実在庫数量（該当データがない場合は0）
    """
    if base_date is None:
        base_date = date.today().strftime("%Y%m%d")

    # デバッグログ: 検索条件
    logger.info(
        f"[STOCK DEBUG] 在庫検索開始 - fctcd:{fctcd}, deptcd:{deptcd}, "
        f"itemgr:{itemgr}, itemcd:{itemcd}, base_date:{base_date}"
    )

    # 該当品目のデータが存在するか確認
    any_stock_count = (
        db.query(func.count(Daystoc.prkey))
        .filter(
            Daystoc.fctcd == fctcd,
            Daystoc.deptcd == deptcd,
            Daystoc.itemgr == itemgr,
            Daystoc.itemcd == itemcd,
        )
        .scalar()
    )

    logger.info(f"[STOCK DEBUG] 該当品目の全在庫レコード数: {any_stock_count}")

    # ステップ1: 最新の在庫日を取得
    latest_stocdt = (
        db.query(func.max(Daystoc.stocdt).label("max_stocdt"))
        .filter(
            Daystoc.fctcd == fctcd,
            Daystoc.deptcd == deptcd,
            Daystoc.itemgr == itemgr,
            Daystoc.itemcd == itemcd,
            Daystoc.stocdt <= base_date,
        )
        .scalar()
    )

    # デバッグログ: 最新在庫日
    logger.info(f"[STOCK DEBUG] 最新在庫日: {latest_stocdt}")

    if not latest_stocdt:
        logger.warning(
            f"[STOCK DEBUG] 在庫データが見つかりません - itemcd:{itemcd} "
            f"(全レコード数: {any_stock_count}, 基準日: {base_date})"
        )
        return 0.0

    # ステップ2: 該当日付の在庫数を合算
    total_stock = (
        db.query(func.sum(Daystoc.actstoc).label("total_stock"))
        .filter(
            Daystoc.fctcd == fctcd,
            Daystoc.deptcd == deptcd,
            Daystoc.itemgr == itemgr,
            Daystoc.itemcd == itemcd,
            Daystoc.stocdt == latest_stocdt,
        )
        .scalar()
    )

    result = float(total_stock) if total_stock else 0.0

    # デバッグログ: 最終結果
    logger.info(
        f"[STOCK DEBUG] 在庫数合算結果: {result} (total_stock: {total_stock})"
    )

    return result

