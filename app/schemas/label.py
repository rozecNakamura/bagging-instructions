from pydantic import BaseModel
from datetime import date
from typing import List, Literal

class LabelItem(BaseModel):
    """ラベル項目"""
    label_type: Literal["standard", "irregular"]
    eating_date: date
    eating_time: str
    product_code: str
    product_name: str
    expiry_date: date | None
    sterilization_temp: float | None
    standard_quantity: float | None = None
    facility_name: str | None = None
    irregular_quantity: float | None = None
    count: int = 1  # ラベル枚数

class LabelResponse(BaseModel):
    """ラベルレスポンス"""
    items: List[LabelItem]

