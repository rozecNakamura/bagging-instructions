from sqlalchemy import Column, Integer, String
from app.core.database import Base

class Customer(Base):
    """得意先マスタ"""
    __tablename__ = "customers"
    
    id = Column(Integer, primary_key=True, index=True)
    customer_code = Column(String(20), unique=True, nullable=False, index=True, comment="得意先コード")
    customer_name = Column(String(100), nullable=False, comment="得意先名称")
    aggregation_rule = Column(String(50), comment="集計ルール（by_facility/by_catering）")
    
    def __repr__(self):
        return f"<Customer(code={self.customer_code}, name={self.customer_name})>"

