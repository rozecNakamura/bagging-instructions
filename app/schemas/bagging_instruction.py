from pydantic import BaseModel, Field
from datetime import date
from typing import List, Literal, Optional, Any
from app.schemas.search import (
    ItemDetail,
    ShpctrDetail,
    RoutDetail,
    MbomDetail,
    CusmcdDetail,
)


class CalculateRequest(BaseModel):
    """計算リクエスト"""

    jobord_prkeys: List[int] = Field(..., description="選択された受注明細プライマリキー配列")
    print_type: Literal["instruction", "label"] = Field(..., description="印刷タイプ")


class SeasoningAmount(BaseModel):
    """調味液情報"""

    citemcd: str = Field(..., description="子品目コード")
    citemgr: Optional[str] = Field(None, description="子品目グループ")
    cfctcd: Optional[str] = Field(None, description="子工場コード")
    cdeptcd: Optional[str] = Field(None, description="子部門コード")
    amu: float = Field(..., description="子部品の必要量")
    otp: float = Field(..., description="生産量")
    calculated_amount: float = Field(..., description="計算結果（調味液必要量）")
    child_item: Optional[Any] = Field(None, description="子品目マスタ情報")


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
    prddt: str | None = Field(None, description="製造日")

    # 在庫数
    current_stock: float = Field(default=0.0, description="現在庫数")

    # 新規追加: 調味液情報
    seasoning_amounts: List[SeasoningAmount] = Field(
        default_factory=list, description="調味液必要量リスト"
    )

    # リレーションデータ（項目対応表に対応）
    item: Optional[ItemDetail] = None
    shpctr: Optional[ShpctrDetail] = None
    mboms: List[MbomDetail] = []
    cusmcd: Optional[CusmcdDetail] = None

    # 追加の基本情報
    jobordno: Optional[str] = None  # 注番
    jobordmernm: Optional[str] = None  # 受注商品名称


class BaggingInstructionResponse(BaseModel):
    """袋詰指示書レスポンス"""

    items: List[BaggingInstructionItem]
