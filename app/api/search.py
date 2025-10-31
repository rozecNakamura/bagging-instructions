from fastapi import APIRouter, Depends, HTTPException, Query
from sqlalchemy.orm import Session
from app.api.deps import get_db
from app.services import search_service
from app.schemas.search import SearchResponse

router = APIRouter()


@router.get("/search", response_model=SearchResponse)
def search_orders(
    prddt: str = Query(None, description="製造日(YYYYMMDD形式)"),
    itemcd: str = Query(None, description="品目コード"),
    db: Session = Depends(get_db),
):
    """
    受注明細を検索

    Args:
        prddt: 製造日(YYYYMMDD形式) - オプション
        itemcd: 品目コード - オプション
        db: DBセッション

    Returns:
        SearchResponse: 検索結果
    """
    # 少なくとも1つのパラメータが必要
    if not prddt and not itemcd:
        raise HTTPException(
            status_code=400,
            detail="製造日（prddt）または品目コード（itemcd）のいずれかを指定してください"
        )

    try:
        items = search_service.search(db, prddt, itemcd)
        return SearchResponse(total=len(items), items=items)
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"検索エラー: {str(e)}")
