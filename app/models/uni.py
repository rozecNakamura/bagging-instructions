from sqlalchemy import Column, String, BigInteger, Numeric, DateTime
from sqlalchemy import UniqueConstraint, Index
from app.core.database import Base


class Uni(Base):
    """単位マスタ（UNI）- 全9カラム定義"""

    __tablename__ = "uni"

    # プライマリキー
    prkey = Column(BigInteger, primary_key=True, comment="")

    # 基本情報
    unicd = Column(String(50), default="", comment="")
    uninm = Column(String(150), default="", comment="")

    # システム情報
    deldt = Column(DateTime, comment="")
    ludate = Column(DateTime, comment="")
    uuser = Column(String(20), default="", comment="")
    udate = Column(DateTime, comment="")

    # 表示・追加情報
    dispno = Column(Numeric, default=0, comment="")
    uniinfnm = Column(String(150), default="", comment="")

    # 制約定義
    __table_args__ = (
        UniqueConstraint("unicd", "deldt", name="uni_unicd_deldt"),
        UniqueConstraint("unicd", name="uni_unique1"),
        Index("uni_pkey", "prkey", unique=True),
    )

    # ========================================
    # リレーション定義
    # ========================================
    # 今後必要に応じてリレーションを追加

    def __repr__(self):
        return f"<Uni(prkey={self.prkey}, unicd={self.unicd}, uninm={self.uninm})>"
