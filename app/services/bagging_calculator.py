from sqlalchemy.orm import Session
from typing import List
from app.repositories import (
    jobord_repository,
    product_repository,
    facility_repository,
    production_input_repository
)
from app.schemas.bagging_instruction import BaggingInstructionItem
from app.services import rounding, aggregation_rule, allocation

def calculate(
    db: Session,
    jobord_ids: List[int]
) -> List[BaggingInstructionItem]:
    """
    袋詰指示書を計算
    
    Args:
        db: DBセッション
        jobord_ids: 選択された受注明細ID配列
    
    Returns:
        袋詰指示書項目リスト
    """
    # 受注明細を取得
    jobords = jobord_repository.find_by_ids(db, jobord_ids)
    
    if not jobords:
        return []
    
    # マスタデータを取得
    product_codes = list(set(j.product_code for j in jobords))
    facility_codes = list(set(j.facility_code for j in jobords))
    
    products = {p.product_code: p for p in product_repository.find_by_codes(db, product_codes)}
    facilities = {f.facility_code: f for f in facility_repository.find_by_codes(db, facility_codes)}
    
    # 集計ルールを適用してグルーピング
    grouped = aggregation_rule.apply_aggregation_rule(jobords)
    
    results = []
    
    for key, group_jobords in grouped.items():
        first_jobord = group_jobords[0]
        product = products.get(first_jobord.product_code)
        
        if not product:
            continue
        
        # 受注量の合計
        total_order = sum(j.order_quantity for j in group_jobords)
        
        # 切り上げ処理
        adjusted_quantity = rounding.round_up_quantity(total_order, product)
        
        # 完成量を取得（あれば按分計算）
        production_input = production_input_repository.find_by_date_and_product(
            db, first_jobord.production_date, first_jobord.product_code
        )
        
        if production_input:
            # 按分計算
            allocated = allocation.allocate_production([first_jobord], production_input.actual_quantity, product)
            adjusted_quantity = allocated[0][1] if allocated else adjusted_quantity
        
        # 規格袋数と端数を計算
        standard_bags = allocation.calculate_standard_bags(adjusted_quantity, product.standard_quantity or 1)
        irregular_quantity = allocation.calculate_irregular_quantity(adjusted_quantity, product.standard_quantity or 1)
        
        # 施設名の決定
        if "_CATERING_" in key:
            facility_name = "ケータリング"
            facility_code = None
        else:
            facility = facilities.get(first_jobord.facility_code)
            facility_name = facility.facility_name if facility else first_jobord.facility_code
            facility_code = first_jobord.facility_code
        
        results.append(BaggingInstructionItem(
            facility_code=facility_code,
            facility_name=facility_name,
            product_code=product.product_code,
            product_name=product.product_name,
            eating_date=first_jobord.eating_date,
            eating_time=first_jobord.eating_time,
            planned_quantity=total_order,
            adjusted_quantity=adjusted_quantity,
            standard_bags=standard_bags,
            irregular_quantity=irregular_quantity
        ))
    
    return results

