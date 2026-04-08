"""袋詰計算（参考実装）。本番の契約・挙動の正は C# API（BaggingInstructions.Api）。"""

from sqlalchemy.orm import Session
from typing import List
from app.services import search_service, rounding, aggregation_rule, allocation, stock_service
from app.schemas.bagging_instruction import BaggingInstructionItem, SeasoningAmount


# ============================================================
# 処理の有効/無効フラグ（C# BaggingCalculatorService と揃える）
# ============================================================
ENABLE_ROUNDING = True  # 処理1: 切り上げ処理
# C# は品目により AllocationService（floor 袋数）を rounding 後に適用。Python は未実装のため False のまま。
ENABLE_ALLOCATION = False  # 処理2: 按分処理（規格袋数計算）
ENABLE_AGGREGATION = True  # 処理3: 集計ルール適用（C# と同様に有効）

# 条件が決まったらここに条件判定関数を追加
# def should_enable_allocation(jobord) -> bool:
#     """按分処理を有効にする条件"""
#     # 例: 特定の品目コードや得意先の場合のみ按分
#     return jobord.itemcd.startswith("10")
#
# def should_enable_aggregation(jobord) -> bool:
#     """集計ルールを有効にする条件"""
#     # 例: 特定の得意先コードの場合のみ集計
#     return jobord.cuscd in ["200", "210", "240", "300", "310"]


def calculate(db: Session, jobord_prkeys: List[int]) -> List[BaggingInstructionItem]:
    """
    袋詰指示書を計算

    Args:
        db: DBセッション
        jobord_prkeys: 選択された受注明細プライマリキー配列

    Returns:
        袋詰指示書項目リスト（リレーションデータ含む）
    """
    # 受注明細を取得（全リレーションデータ含む）
    jobords = search_service.search_detail_by_prkeys(db, jobord_prkeys)

    if not jobords:
        return []

    # ============================================================
    # 処理3: 集計ルールを適用してグルーピング（現在無効化）
    # ============================================================
    if ENABLE_AGGREGATION:
        grouped = aggregation_rule.apply_aggregation_rule(jobords)
    else:
        # 集計ルール無効時: 個別に処理（グルーピングなし）
        grouped = {f"{j.prkey}": [j] for j in jobords}

    results = []

    for key, group_jobords in grouped.items():
        # グループの最初の要素を代表として使用（prkey昇順なので最小値）
        first_jobord = group_jobords[0]

        # 受注量の合計（グルーピング時は合算、無効時は個別値）
        total_order = sum(float(j.jobordqun or 0) for j in group_jobords)

        # ============================================================
        # 在庫数取得処理（切り上げ処理の前）
        # ============================================================
        current_stock = 0.0
        if first_jobord.item:
            # itemリレーションから品目情報を取得
            current_stock = stock_service.get_item_stock(
                db=db,
                fctcd=first_jobord.item.fctcd,
                deptcd=first_jobord.item.deptcd,
                itemgr=first_jobord.item.itemgr,
                itemcd=first_jobord.item.itemcd,
                base_date=first_jobord.prddt,  # 製造日を基準日とする
            )

        # ============================================================
        # 処理1: 切り上げ処理と調味液計算（個数単位の品目のみ）
        # ============================================================
        if ENABLE_ROUNDING:
            (
                standard_count,
                irregular_count,
                seasoning_list,
            ) = rounding.round_up_quantity_with_seasoning(
                total_order, first_jobord.item, first_jobord.mboms
            )
            # 合計調整後数量を計算
            adjusted_quantity = standard_count + irregular_count

            # 切り上げ処理の結果を standard_bags と irregular_quantity に反映
            standard_bags = int(standard_count)  # 整数部分（規格袋数）
            irregular_quantity = irregular_count  # 余り部分（切り上げられた値）

            # 調味液情報をPydanticモデルに変換
            seasoning_amounts = [SeasoningAmount(**s) for s in seasoning_list]
        else:
            adjusted_quantity = total_order
            standard_bags = 0
            irregular_quantity = 0
            seasoning_amounts = []

        # ============================================================
        # 処理2: 規格袋数と端数を計算（現在無効化）
        # ============================================================
        if ENABLE_ALLOCATION:
            # itemのkikunip（規格量）を使用
            kikunip = (
                first_jobord.item.kikunip
                if first_jobord.item and hasattr(first_jobord.item, "kikunip")
                else 1.0
            )
            standard_bags = allocation.calculate_standard_bags(
                adjusted_quantity, kikunip
            )
            irregular_quantity = allocation.calculate_irregular_quantity(
                adjusted_quantity, kikunip
            )

        # 納入場所名の決定
        if ENABLE_AGGREGATION and "_CATERING_" in key:
            # 集計ルール有効時のケータリング統合
            shpctrnm = "ケータリング"
            shpctrcd = None
        else:
            # リレーション経由で納入場所名を取得
            shpctrnm = (
                first_jobord.shpctr.shpctrnm
                if first_jobord.shpctr
                else first_jobord.shpctrcd or "不明"
            )
            shpctrcd = first_jobord.shpctrcd

        # 品目名の決定
        itemnm = (
            first_jobord.item.itemnm if first_jobord.item else first_jobord.itemcd or ""
        )

        results.append(
            BaggingInstructionItem(
                shpctrcd=shpctrcd,
                shpctrnm=shpctrnm,
                itemcd=first_jobord.itemcd or "",
                itemnm=itemnm,
                delvedt=first_jobord.delvedt or "",
                shptm=first_jobord.shptm,
                planned_quantity=total_order,
                adjusted_quantity=adjusted_quantity,
                standard_bags=standard_bags,
                irregular_quantity=irregular_quantity,
                prddt=first_jobord.prddt or "",
                current_stock=current_stock,  # 在庫数を追加
                seasoning_amounts=seasoning_amounts,  # 新規追加
                # リレーションデータを含める
                item=first_jobord.item,
                shpctr=first_jobord.shpctr,
                mboms=first_jobord.mboms,
                cusmcd=first_jobord.cusmcd,
                # 追加の基本情報
                jobordno=first_jobord.jobordno,
                jobordmernm=first_jobord.jobordmernm,
            )
        )

    # ============================================================
    # メインテーブル表示用に同じ品目・納入場所で合算
    # fctcd, cuscd, shpctrcd, itemcd が同じデータを合算
    # ============================================================
    aggregated_results = {}
    
    for result in results:
        # 結合キーを生成（fctcd, cuscd, shpctrcd, itemcd）
        fctcd = result.item.fctcd if result.item else ""
        cuscd = result.shpctr.cuscd if result.shpctr else ""
        key = f"{fctcd}_{cuscd}_{result.shpctrcd}_{result.itemcd}"
        
        if key not in aggregated_results:
            # 最初のアイテムをそのまま使用
            aggregated_results[key] = result
        else:
            # 既存のアイテムに合算
            existing = aggregated_results[key]
            
            # 数値データを合算
            existing.planned_quantity += result.planned_quantity
            existing.adjusted_quantity += result.adjusted_quantity
            existing.standard_bags += result.standard_bags
            existing.irregular_quantity += result.irregular_quantity
            
            # 調味液の量を合算
            if existing.seasoning_amounts and result.seasoning_amounts:
                for i, seasoning in enumerate(result.seasoning_amounts):
                    if i < len(existing.seasoning_amounts):
                        existing.seasoning_amounts[i].calculated_amount += seasoning.calculated_amount
            elif result.seasoning_amounts:
                existing.seasoning_amounts = result.seasoning_amounts
    
    # 合算結果をリストに変換
    final_results = list(aggregated_results.values())

    return final_results
