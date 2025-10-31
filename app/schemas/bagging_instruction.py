from pydantic import BaseModel, Field
from datetime import date
from typing import List, Literal


class CalculateRequest(BaseModel):
    """計算リクエスト"""

    jobord_prkeys: List[int] = Field(..., description="選択された受注明細プライマリキー配列")
    print_type: Literal["instruction", "label"] = Field(..., description="印刷タイプ")


class BaggingInstructionItem(BaseModel):
    """袋詰指示書項目"""

    shpctrcd: str | None = Field(None, description="納入場所コード")
    shpctrnm: str = Field(..., description="納入場所名称")
    itemcd: str = Field(..., description="品目コード")
    itemnm: str = Field(..., description="品目名称")
    delvedt: str = Field(..., description="納品日")
    shptm: str | None = Field(None, description="出荷時刻")
    planned_quantity: float = Field(..., description="計画数量")
    adjusted_quantity: float = Field(..., description="調整後数量")
    standard_bags: int = Field(..., description="規格袋数")
    irregular_quantity: float = Field(..., description="端数量")


class BaggingInstructionResponse(BaseModel):
    """袋詰指示書レスポンス"""

    items: List[BaggingInstructionItem]
