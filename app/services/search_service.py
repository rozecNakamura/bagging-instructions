from sqlalchemy.orm import Session
from sqlalchemy import and_
from typing import List
from app.models.jobord import Jobord
from app.utils.query_loader import load_relations
from app.schemas.search import JobordItem


def search(db: Session, prddt: str, itemcd: str = None) -> List[JobordItem]:
    """
    受注明細を検索（基本情報のみ）

    Args:
        db: DBセッション
        prddt: 製造日(YYYYMMDD形式) - 必須
        itemcd: 品目コード - オプション

    Returns:
        受注明細リスト
    """
    # 製造日は必須なので常にフィルタに含める
    filters = [Jobord.prddt == prddt]
    
    # 品目コードが指定されている場合のみ追加
    if itemcd:
        filters.append(Jobord.itemcd == itemcd)

    query = db.query(Jobord).filter(and_(*filters))
    jobords = query.all()

    if not jobords:
        return []

    # 基本情報のみ返す
    result_items = []
    for jobord in jobords:
        result_items.append(
            JobordItem(
                prkey=jobord.prkey,
                prddt=jobord.prddt,
                delvedt=jobord.delvedt,
                shptm=jobord.shptm,
                cuscd=jobord.cuscd,
                shpctrcd=jobord.shpctrcd,
                itemcd=jobord.itemcd,
                jobordmernm=jobord.jobordmernm,
                jobordqun=float(jobord.jobordqun) if jobord.jobordqun else 0.0,
            )
        )

    return result_items


def search_detail_by_prkeys(db: Session, prkeys: List[int]) -> List[Jobord]:
    """
    プライマリキーで受注明細を検索（全リレーションデータ含む）

    印刷処理で使用。処理1（切り上げ）の前に全マスタデータをロードする。

    Args:
        db: DBセッション
        prkeys: 受注明細プライマリキー配列

    Returns:
        Jobordオブジェクトのリスト（全リレーションロード済み）
    """
    if not prkeys:
        return []

    # クエリ構築
    query = db.query(Jobord).filter(Jobord.prkey.in_(prkeys))

    # 全リレーションをロード
    query = load_relations(
        query,
        Jobord.item,  # 品目マスタ（処理1で使用）
        Jobord.shpctr,  # 納入場所マスタ
        Jobord.cusmcd,  # 得意先品目変換マスタ
    )

    # ネストされたリレーションを明示的にロード
    from sqlalchemy.orm import selectinload
    from app.models.mbom import Mbom
    from app.models.item import Item
    from app.models.rout import Rout

    query = query.options(
        # Mbomとそのchild_item、さらにchild_itemのuni、routsをロード
        selectinload(Jobord.mboms).selectinload(Mbom.child_item).selectinload(Item.uni),
        selectinload(Jobord.mboms)
        .selectinload(Mbom.child_item)
        .selectinload(Item.routs)
        .selectinload(Rout.ware),
        selectinload(Jobord.mboms)
        .selectinload(Mbom.child_item)
        .selectinload(Item.routs)
        .selectinload(Rout.workc),
        # Itemのuni、routsをロード
        selectinload(Jobord.item).selectinload(Item.uni),
        selectinload(Jobord.item).selectinload(Item.routs).selectinload(Rout.ware),
        selectinload(Jobord.item).selectinload(Item.routs).selectinload(Rout.workc),
    )

    jobords = query.all()

    # SQLAlchemyのInstrumentedListを通常のlistに変換（Pydanticシリアライズ対応）
    for jobord in jobords:
        # mbomsリレーションをlistに変換
        if jobord.mboms is not None:
            # 単一オブジェクトの場合はリストでラップ（念のための防御的処理）
            if not isinstance(jobord.mboms, list):
                jobord.mboms = [jobord.mboms]
            else:
                jobord.mboms = list(jobord.mboms)
        else:
            jobord.mboms = []
    
    # prkey（プライマリキー）の昇順でソート
    jobords.sort(key=lambda j: j.prkey)

    return jobords
