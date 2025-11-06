from sqlalchemy import Column, String, BigInteger, Numeric, DateTime
from sqlalchemy import UniqueConstraint, Index
from app.core.database import Base


class Ware(Base):
    """倉庫マスタ（WARE）- 全51カラム定義"""

    __tablename__ = "ware"

    # プライマリキー
    prkey = Column(BigInteger, primary_key=True, comment="")

    # 基本情報
    fctcd = Column(String(50), default="", comment="")
    whcd = Column(String(50), default="", comment="")
    whnm = Column(String(150), default="", comment="")

    # システム情報
    deldt = Column(DateTime, comment="")
    ludate = Column(DateTime, comment="")
    uuser = Column(String(20), default="", comment="")
    udate = Column(DateTime, comment="")

    # 表示情報
    dispno = Column(Numeric, default=0, comment="")
    whinfnm = Column(String(150), default="", comment="")

    # 住所・連絡先
    zip = Column(String(20), default="", comment="")
    add1 = Column(String(100), default="", comment="")
    add2 = Column(String(100), default="", comment="")
    email = Column(String(100), default="", comment="")
    tel = Column(String(20), default="", comment="")
    fax = Column(String(20), default="", comment="")
    whpicnm = Column(String(50), default="", comment="")

    # テンプレート・表示カウント
    trsrctempfile = Column(String(50), default="", comment="")
    trsrcdispcnt = Column(Numeric, default=0, comment="")
    trdesttempfile = Column(String(50), default="", comment="")
    trdestdispcnt = Column(Numeric, default=0, comment="")

    # グループ・タイプ
    whgrplcd = Column(String(50), default="", comment="")
    whgrpmcd = Column(String(50), default="", comment="")
    whtyp = Column(String(2), default="0", comment="")
    weicap = Column(Numeric, default=0, comment="")
    ocptyp = Column(String(2), default="", comment="")

    # フラグ
    isrecv = Column(String(2), default="", comment="")
    iscomp = Column(String(2), default="", comment="")
    iscalc = Column(String(2), default="", comment="")

    # 部門・印刷名
    deptcd = Column(String(50), default="", comment="")
    whprntnm = Column(String(150), default="", comment="")
    whprntnm2 = Column(String(150), default="", comment="")

    # 予約・在庫タイプ
    isrsv = Column(String(2), default="", comment="")
    isthrown = Column(String(2), default="", comment="")
    isvalue = Column(String(2), default="1", comment="")
    inorouttyp = Column(String(2), default="0", comment="")
    rsvstocgrpcd = Column(String(50), default="", comment="")

    # 受注グループ
    jobordgrp1 = Column(String(50), default="", comment="")
    disusewh = Column(String(1), default="0", comment="")

    # ブロック・配置情報
    blocknm = Column(String(50), default="", comment="")
    colnum = Column(Numeric, default=0, comment="")
    depthnum = Column(Numeric, default=0, comment="")
    stepnum = Column(Numeric, default=0, comment="")

    # 棚卸・ライン
    shemanatyp = Column(String(2), default="", comment="")
    linecd = Column(String(50), default="", comment="")

    # 追加情報1-5
    wareaddinfo01 = Column(String(50), default="", comment="")
    wareaddinfo02 = Column(String(50), default="", comment="")
    wareaddinfo03 = Column(String(50), default="", comment="")
    wareaddinfo04 = Column(String(50), default="", comment="")
    wareaddinfo05 = Column(String(50), default="", comment="")

    # 前倉庫
    prewhcd = Column(String(50), default="", comment="")

    # 制約定義
    __table_args__ = (
        UniqueConstraint("fctcd", "whcd", "deldt", name="ware_fctcd_whcd_deldt"),
        UniqueConstraint("fctcd", "whcd", name="ware_fctcd_whcd_key"),
        Index("ware_pkey", "prkey", unique=True),
    )

    # ========================================
    # リレーション定義
    # ========================================
    # 今後必要に応じてリレーションを追加

    def __repr__(self):
        return f"<Ware(prkey={self.prkey}, fctcd={self.fctcd}, whcd={self.whcd}, whnm={self.whnm})>"
