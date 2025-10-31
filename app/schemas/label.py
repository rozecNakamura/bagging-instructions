from pydantic import BaseModel, Field
from datetime import date
from typing import List, Literal


class LabelItem(BaseModel):
    """ラベル項目"""

    label_type: Literal["standard", "irregular"] = Field(..., description="ラベルタイプ")
    delvedt: str = Field(..., description="納品日")
    shptm: str | None = Field(None, description="出荷時刻")
    itemcd: str = Field(..., description="品目コード")
    itemnm: str = Field(..., description="品目名称")
    expiry_date: str | None = Field(None, description="賞味期限")
    strtemp: str | None = Field(None, description="殺菌温度")
    kikunip: float | None = Field(None, description="規格量")
    shpctrnm: str | None = Field(None, description="納入場所名称")
    irregular_quantity: float | None = Field(None, description="端数量")
    count: int = Field(1, description="ラベル枚数")


class LabelResponse(BaseModel):
    """ラベルレスポンス"""

    items: List[LabelItem]
