from sqlalchemy.orm import Session
from datetime import date
from typing import List
from app.repositories import jobord_repository, product_repository, facility_repository
from app.schemas.search import JobordItem

def search(
    db: Session,
    production_date: date,
    product_code: str
) -> List[JobordItem]:
    """
    受注明細を検索
    
    Args:
        db: DBセッション
        production_date: 製造日
        product_code: 品目コード
    
    Returns:
        受注明細リスト
    """
    # 受注明細を取得
    jobords = jobord_repository.find_by_production_date_and_product(
        db, production_date, product_code
    )
    
    if not jobords:
        return []
    
    # 品目マスタと施設マスタを取得
    product_codes = list(set(j.product_code for j in jobords))
    facility_codes = list(set(j.facility_code for j in jobords))
    
    products = {p.product_code: p for p in product_repository.find_by_codes(db, product_codes)}
    facilities = {f.facility_code: f for f in facility_repository.find_by_codes(db, facility_codes)}
    
    # レスポンス作成
    items = []
    for jobord in jobords:
        product = products.get(jobord.product_code)
        facility = facilities.get(jobord.facility_code)
        
        items.append(JobordItem(
            id=jobord.id,
            production_date=jobord.production_date,
            eating_date=jobord.eating_date,
            eating_time=jobord.eating_time,
            customer_code=jobord.customer_code,
            facility_code=jobord.facility_code,
            facility_name=facility.facility_name if facility else None,
            product_code=jobord.product_code,
            product_name=product.product_name if product else None,
            order_quantity=jobord.order_quantity
        ))
    
    return items

