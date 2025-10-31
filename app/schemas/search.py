from pydantic import BaseModel, Field
from datetime import date
from typing import List

class SearchRequest(BaseModel):
    """検索リクエスト"""
    production_date: date = Field(..., description="製造日")
    product_code: str = Field(..., description="品目コード", min_length=1)

class JobordItem(BaseModel):
    """受注明細項目"""
    id: int
    production_date: date
    eating_date: date
    eating_time: str
    customer_code: str
    facility_code: str
    facility_name: str | None = None
    product_code: str
    product_name: str | None = None
    order_quantity: float
    
    class Config:
        from_attributes = True

class SearchResponse(BaseModel):
    """検索レスポンス"""
    total: int
    items: List[JobordItem]

