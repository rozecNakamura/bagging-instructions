from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy.orm import Session
from app.api.deps import get_db
from app.services import bagging_calculator, label_generator
from app.schemas.bagging_instruction import CalculateRequest, BaggingInstructionResponse
from app.schemas.label import LabelResponse
from app.repositories import product_repository

router = APIRouter()

@router.post("/calculate", response_model=BaggingInstructionResponse | LabelResponse)
def calculate_bagging(
    request: CalculateRequest,
    db: Session = Depends(get_db)
):
    """
    袋詰指示書・ラベルデータを計算
    
    Args:
        request: 計算リクエスト（選択項目ID、印刷タイプ）
        db: DBセッション
    
    Returns:
        袋詰指示書データ or ラベルデータ
    """
    try:
        # 袋詰指示書を計算
        bagging_items = bagging_calculator.calculate(db, request.jobord_ids)
        
        if request.print_type == "instruction":
            # 袋詰指示書として返却
            return BaggingInstructionResponse(items=bagging_items)
        
        else:
            # ラベルデータを生成
            label_items = []
            
            for item in bagging_items:
                product = product_repository.find_by_code(db, item.product_code)
                
                if not product:
                    continue
                
                # 規格品ラベル
                standard_labels = label_generator.generate_standard_labels(
                    product,
                    item.eating_date,
                    item.eating_time,
                    item.standard_bags
                )
                label_items.extend(standard_labels)
                
                # 端数ラベル
                irregular_labels = label_generator.generate_irregular_labels(
                    product,
                    item.eating_date,
                    item.eating_time,
                    item.facility_name,
                    item.irregular_quantity
                )
                label_items.extend(irregular_labels)
            
            return LabelResponse(items=label_items)
    
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"計算エラー: {str(e)}")

