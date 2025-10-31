from sqlalchemy import Column, Integer, String, Float, Boolean
from app.core.database import Base

class Product(Base):
    """品目マスタ"""
    __tablename__ = "products"
    
    id = Column(Integer, primary_key=True, index=True)
    product_code = Column(String(20), unique=True, nullable=False, index=True, comment="品目コード")
    product_name = Column(String(100), nullable=False, comment="品目名称")
    standard_quantity = Column(Float, comment="規格量（1袋あたりの量）")
    is_count_unit = Column(Boolean, default=False, comment="個数単位フラグ")
    sterilization_temp = Column(Float, comment="殺菌温度")
    seasoning_rate = Column(Float, comment="調味液比率")
    
    def __repr__(self):
        return f"<Product(code={self.product_code}, name={self.product_name})>"

