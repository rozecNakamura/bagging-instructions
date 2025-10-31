from sqlalchemy.orm import Session
from typing import Optional
from app.models.customer import Customer

def find_by_code(db: Session, customer_code: str) -> Optional[Customer]:
    """得意先コードで得意先マスタを取得"""
    return db.query(Customer).filter(Customer.customer_code == customer_code).first()

def find_all(db: Session) -> list[Customer]:
    """全得意先を取得"""
    return db.query(Customer).all()

