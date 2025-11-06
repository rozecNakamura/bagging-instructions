from fastapi import APIRouter, Depends, HTTPException, Query, Body
from sqlalchemy.orm import Session
from typing import List
from app.api.deps import get_db
from app.services import search_service
from app.schemas.search import SearchResponse, SearchDetailResponse

router = APIRouter()


@router.get("/search", response_model=SearchResponse)
def search_orders(
    prddt: str = Query(..., description="製造日(YYYYMMDD形式)"),
    itemcd: str = Query(None, description="品目コード"),
    db: Session = Depends(get_db),
):
    """
    受注明細を検索

    Args:
        prddt: 製造日(YYYYMMDD形式) - 必須
        itemcd: 品目コード - オプション
        db: DBセッション

    Returns:
        SearchResponse: 検索結果
    """
    try:
        items = search_service.search(db, prddt, itemcd)
        return SearchResponse(total=len(items), items=items)
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"検索エラー: {str(e)}")


@router.post("/search/detail", response_model=SearchDetailResponse)
def search_orders_detail(
    prkeys: List[int] = Body(..., embed=True, description="受注明細プライマリキー配列"),
    db: Session = Depends(get_db),
):
    """
    受注明細を検索（全リレーションデータ含む）

    印刷処理で使用。袋詰計算の処理1（切り上げ）実行前に、
    全てのマスタデータ（item、shpctr、routs、mboms、cusmcd）を
    リレーション経由で取得します。

    Args:
        prkeys: 受注明細プライマリキー配列
        db: DBセッション

    Returns:
        SearchDetailResponse: 全リレーションデータを含む検索結果
    """
    if not prkeys:
        raise HTTPException(status_code=400, detail="プライマリキーを指定してください")

    try:
        jobords = search_service.search_detail_by_prkeys(db, prkeys)
        return SearchDetailResponse(total=len(jobords), items=jobords)
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"検索エラー: {str(e)}")
