from sqlalchemy import Column, Integer, String
from app.core.database import Base

class Facility(Base):
    """施設（店舗）マスタ"""
    __tablename__ = "facilities"
    
    id = Column(Integer, primary_key=True, index=True)
    facility_code = Column(String(20), unique=True, nullable=False, index=True, comment="施設コード")
    facility_name = Column(String(100), nullable=False, comment="施設名")
    
    def __repr__(self):
        return f"<Facility(code={self.facility_code}, name={self.facility_name})>"

