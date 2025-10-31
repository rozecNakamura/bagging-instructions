from pydantic import BaseModel, Field
from datetime import date
from typing import List, Literal

class CalculateRequest(BaseModel):
    """計算リクエスト"""
    jobord_ids: List[int] = Field(..., description="選択された受注明細ID配列")
    print_type: Literal["instruction", "label"] = Field(..., description="印刷タイプ")

class BaggingInstructionItem(BaseModel):
    """袋詰指示書項目"""
    facility_code: str | None
    facility_name: str
    product_code: str
    product_name: str
    eating_date: date
    eating_time: str
    planned_quantity: float
    adjusted_quantity: float
    standard_bags: int
    irregular_quantity: float
    component_details: List[dict] | None = None

class BaggingInstructionResponse(BaseModel):
    """袋詰指示書レスポンス"""
    items: List[BaggingInstructionItem]

