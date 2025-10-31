from pydantic import BaseModel, Field
from datetime import date
from typing import List


class SearchRequest(BaseModel):
    """検索リクエスト"""

    prddt: str = Field(..., description="製造日(YYYYMMDD)")
    itemcd: str = Field(..., description="品目コード", min_length=1)


class JobordItem(BaseModel):
    """受注明細項目"""

    prkey: int = Field(..., description="プライマリキー")
    prddt: str | None = Field(None, description="製造日")
    delvedt: str | None = Field(None, description="納品日")
    shptm: str | None = Field(None, description="出荷時刻")
    cuscd: str | None = Field(None, description="得意先コード")
    shpctrcd: str | None = Field(None, description="納入場所コード")
    shpctrnm: str | None = Field(None, description="納入場所名称")
    itemcd: str | None = Field(None, description="品目コード")
    itemnm: str | None = Field(None, description="品目名称")
    jobordqun: float | None = Field(None, description="受注数量")

    class Config:
        from_attributes = True


class SearchResponse(BaseModel):
    """検索レスポンス"""

    total: int
    items: List[JobordItem]
