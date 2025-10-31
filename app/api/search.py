from fastapi import APIRouter, Depends, HTTPException, Query
from sqlalchemy.orm import Session
from datetime import date
from app.api.deps import get_db
from app.services import search_service
from app.schemas.search import SearchResponse, JobordItem

router = APIRouter()

@router.get("/search", response_model=SearchResponse)
def search_orders(
    production_date: date = Query(..., description="製造日"),
    product_code: str = Query(..., description="品目コード"),
    db: Session = Depends(get_db)
):
    """
    受注明細を検索
    
    Args:
        production_date: 製造日
        product_code: 品目コード
        db: DBセッション
    
    Returns:
        SearchResponse: 検索結果
    """
    try:
        items = search_service.search(db, production_date, product_code)
        return SearchResponse(total=len(items), items=items)
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"検索エラー: {str(e)}")

