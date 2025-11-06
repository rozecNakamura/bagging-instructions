from sqlalchemy import Column, String, BigInteger, Numeric, DateTime
from sqlalchemy import UniqueConstraint, Index
from sqlalchemy.orm import relationship
from app.core.database import Base


class Mbom(Base):
    """レシピマスタ/BOM（MBOM）- 全33カラム定義"""

    __tablename__ = "mbom"

    # プライマリキー
    prkey = Column(BigInteger, primary_key=True, comment="プライマリキー")

    # 親品目情報
    pfctcd = Column(String(50), nullable=False, default="", comment="親工場コード")
    pdeptcd = Column(String(50), nullable=False, default="", comment="親部門コード")
    pitemgr = Column(String(1), nullable=False, default="", comment="親品目グループ")
    pitemcd = Column(String(50), nullable=False, default="", comment="親品目コード")
    proutno = Column(Numeric, nullable=False, default=0, comment="")
    pcd1 = Column(Numeric, nullable=False, default=0, comment="")
    pcd2 = Column(Numeric, nullable=False, default=0, comment="")

    # 子品目情報
    cfctcd = Column(String(50), nullable=False, default="", comment="子工場コード")
    cdeptcd = Column(String(50), nullable=False, default="", comment="子部門コード")
    citemgr = Column(String(1), nullable=False, default="", comment="子品目グループ")
    citemcd = Column(String(50), nullable=False, default="", comment="子品目コード")

    # 数量・タイプ
    amu = Column(Numeric, default=0, comment="")
    otp = Column(Numeric, default=0, comment="")
    partyp = Column(String(1), default="", comment="")
    par = Column(Numeric, default=0, comment="")
    prvtyp = Column(String(1), default="", comment="")

    # 発行条件
    issjor = Column(String(150), default="", comment="")
    memo = Column(String(150), default="", comment="メモ")
    mbompic = Column(String(50), default="", comment="")

    # 期間
    stadt = Column(String(8), nullable=False, default="", comment="開始日")
    enddt = Column(String(8), nullable=False, default="", comment="終了日")

    # システム情報
    uuser = Column(String(20), default="", comment="更新ユーザー")
    udate = Column(DateTime, comment="更新日時")

    # 設定フラグ
    unset = Column(String(1), default="", comment="")
    bbdtset = Column(String(1), default="0", comment="")
    planruntgtflg = Column(String(1), nullable=False, default="0", comment="")

    # 重量・グループ
    weighqun = Column(Numeric, default=0, comment="")
    throwngrpno = Column(Numeric, default=0, comment="")
    ratio = Column(Numeric, default=0, comment="")
    weightyp = Column(String(1), default="", comment="")

    # 追加情報
    addfst = Column(Numeric, default=0, comment="")
    mbomlt = Column(Numeric, default=0, comment="")

    # 制約定義
    __table_args__ = (
        UniqueConstraint(
            "pfctcd",
            "pdeptcd",
            "pitemgr",
            "pitemcd",
            "proutno",
            "pcd1",
            "pcd2",
            "cfctcd",
            "cdeptcd",
            "citemgr",
            "citemcd",
            "stadt",
            "enddt",
            name="mbom_pfctcd_pdeptcd_pitemgr_pitemcd_proutno_pcd1_pcd2_cfctc_key",
        ),
        Index("mbom_mbompic", "mbompic"),
        Index(
            "mbom_pfctcd_pdeptcd_pitemgr_pitemcd_stadt_enddt",
            "pfctcd",
            "pdeptcd",
            "pitemgr",
            "pitemcd",
            "stadt",
            "enddt",
        ),
    )

    # ========================================
    # リレーション定義
    # ========================================

    # 子品目マスタ（Item）
    child_item = relationship(
        "Item",
        primaryjoin=(
            "and_("
            "foreign(Mbom.cfctcd) == Item.fctcd, "
            "foreign(Mbom.cdeptcd) == Item.deptcd, "
            "foreign(Mbom.citemgr) == Item.itemgr, "
            "foreign(Mbom.citemcd) == Item.itemcd"
            ")"
        ),
        viewonly=True,
        lazy="select",
    )

    def __repr__(self):
        return f"<Mbom(prkey={self.prkey}, pitemcd={self.pitemcd}, citemcd={self.citemcd})>"
