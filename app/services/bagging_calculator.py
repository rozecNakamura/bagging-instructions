from sqlalchemy.orm import Session
from typing import List
from app.repositories import (
    jobord_repository,
    item_repository,
    shpctr_repository,
)
from app.schemas.bagging_instruction import BaggingInstructionItem
from app.services import rounding, aggregation_rule, allocation


def calculate(db: Session, jobord_prkeys: List[int]) -> List[BaggingInstructionItem]:
    """
    袋詰指示書を計算

    Args:
        db: DBセッション
        jobord_prkeys: 選択された受注明細プライマリキー配列

    Returns:
        袋詰指示書項目リスト
    """
    # 受注明細を取得
    jobords = jobord_repository.find_by_prkeys(db, jobord_prkeys)

    if not jobords:
        return []

    # マスタデータを取得するための情報収集
    # TODO: 複合キー対応が必要
    itemcds = list(set(j.itemcd for j in jobords if j.itemcd))
    shpctrcds = list(set(j.shpctrcd for j in jobords if j.shpctrcd))

    # 仮実装: マスタ取得は後で実装
    items = {}
    shpctrs = {}

    # 集計ルールを適用してグルーピング
    grouped = aggregation_rule.apply_aggregation_rule(jobords)

    results = []

    for key, group_jobords in grouped.items():
        first_jobord = group_jobords[0]
        item = items.get(first_jobord.itemcd)

        # 受注量の合計
        total_order = sum(float(j.jobordqun or 0) for j in group_jobords)

        # 切り上げ処理（個数単位の品目のみ）
        adjusted_quantity = rounding.round_up_quantity(total_order, item)

        # 規格袋数と端数を計算
        # TODO: itemのkikunip（規格量）を使用
        kikunip = 1.0  # 仮値
        standard_bags = allocation.calculate_standard_bags(adjusted_quantity, kikunip)
        irregular_quantity = allocation.calculate_irregular_quantity(
            adjusted_quantity, kikunip
        )

        # 納入場所名の決定
        if "_CATERING_" in key:
            shpctrnm = "ケータリング"
            shpctrcd = None
        else:
            shpctr = shpctrs.get(first_jobord.shpctrcd)
            shpctrnm = shpctr.shpctrnm if shpctr else first_jobord.shpctrcd or "不明"
            shpctrcd = first_jobord.shpctrcd

        results.append(
            BaggingInstructionItem(
                shpctrcd=shpctrcd,
                shpctrnm=shpctrnm,
                itemcd=first_jobord.itemcd or "",
                itemnm=item.itemnm if item else first_jobord.itemcd or "",
                delvedt=first_jobord.delvedt or "",
                shptm=first_jobord.shptm,
                planned_quantity=total_order,
                adjusted_quantity=adjusted_quantity,
                standard_bags=standard_bags,
                irregular_quantity=irregular_quantity,
            )
        )

    return results
