from sqlalchemy.orm import Session
from typing import List
from app.repositories import jobord_repository, item_repository, shpctr_repository
from app.schemas.search import JobordItem


def search(db: Session, prddt: str = None, itemcd: str = None) -> List[JobordItem]:
    """
    受注明細を検索

    Args:
        db: DBセッション
        prddt: 製造日(YYYYMMDD形式) - オプション
        itemcd: 品目コード - オプション

    Returns:
        受注明細リスト
    """
    # 少なくとも1つのパラメータが必要
    if not prddt and not itemcd:
        return []

    # 受注明細を取得
    jobords = jobord_repository.find_by_prddt_and_itemcd(db, prddt, itemcd)

    if not jobords:
        return []

    # 品目マスタと納入場所マスタの取得用データ収集
    # 複合キー: (fctcd, deptcd, itemgr, itemcd)
    item_keys = list(
        set((j.fctcd, j.deptcd, j.itemgr, j.itemcd) for j in jobords if j.itemcd)
    )
    # 複合キー: (fctcd, cuscd, shpctrcd)
    shpctr_keys = list(
        set((j.fctcd, j.cuscd, j.shpctrcd) for j in jobords if j.shpctrcd)
    )

    # TODO: 複合キー対応のリポジトリメソッドが必要
    # 仮実装: 現時点ではマスタなしで返す
    items_dict = {}
    shpctrs_dict = {}

    # レスポンス作成
    result_items = []
    for jobord in jobords:
        # マスタから名称を取得（現時点では未実装のため、コードをそのまま表示）
        item_key = (jobord.fctcd, jobord.deptcd, jobord.itemgr, jobord.itemcd)
        shpctr_key = (jobord.fctcd, jobord.cuscd, jobord.shpctrcd)

        item = items_dict.get(item_key)
        shpctr = shpctrs_dict.get(shpctr_key)

        result_items.append(
            JobordItem(
                prkey=jobord.prkey,
                prddt=jobord.prddt,
                delvedt=jobord.delvedt,
                shptm=jobord.shptm,
                cuscd=jobord.cuscd,
                shpctrcd=jobord.shpctrcd,
                shpctrnm=shpctr.shpctrnm if shpctr else jobord.shpctrcd,
                itemcd=jobord.itemcd,
                itemnm=item.itemnm if item else jobord.itemcd,
                jobordqun=float(jobord.jobordqun) if jobord.jobordqun else 0.0,
            )
        )

    return result_items
