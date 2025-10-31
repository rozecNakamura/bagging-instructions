from sqlalchemy import Column, Integer, String, Date, Float
from app.core.database import Base

class ProductionInput(Base):
    """袋詰投入量（完成量）"""
    __tablename__ = "production_inputs"
    
    id = Column(Integer, primary_key=True, index=True)
    production_date = Column(Date, nullable=False, index=True, comment="製造日")
    product_code = Column(String(20), nullable=False, index=True, comment="品目コード")
    actual_quantity = Column(Float, nullable=False, comment="実際完成量")
    component_name = Column(String(100), comment="構成物名")
    component_quantity = Column(Float, comment="構成物量")
    
    def __repr__(self):
        return f"<ProductionInput(date={self.production_date}, product={self.product_code}, qty={self.actual_quantity})>"

