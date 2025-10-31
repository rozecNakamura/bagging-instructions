from sqlalchemy import Column, Integer, String, Date, Float, DateTime
from app.core.database import Base

class Jobord(Base):
    """受注明細テーブル"""
    __tablename__ = "JOBORD"
    
    id = Column(Integer, primary_key=True, index=True, autoincrement=True)
    production_date = Column(Date, nullable=False, index=True, comment="製造日")
    eating_date = Column(Date, nullable=False, comment="喫食日")
    eating_time = Column(String(10), nullable=False, comment="喫食時間（朝/昼/夕）")
    customer_code = Column(String(20), nullable=False, index=True, comment="得意先コード")
    facility_code = Column(String(20), nullable=False, index=True, comment="施設（店舗）コード")
    product_code = Column(String(20), nullable=False, index=True, comment="品目コード（キットコード）")
    order_quantity = Column(Float, nullable=False, comment="受注量")
    created_at = Column(DateTime, comment="作成日時")
    updated_at = Column(DateTime, comment="更新日時")
    
    def __repr__(self):
        return f"<Jobord(id={self.id}, product_code={self.product_code}, facility_code={self.facility_code})>"

