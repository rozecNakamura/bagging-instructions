from sqlalchemy import Column, String, BigInteger, Numeric, DateTime
from sqlalchemy import UniqueConstraint, Index
from app.core.database import Base


class Rout(Base):
    """工程手順マスタ（ROUT）- 全48カラム定義"""

    __tablename__ = "rout"

    # プライマリキー
    prkey = Column(BigInteger, primary_key=True, comment="プライマリキー")

    # 品目情報
    fctcd = Column(String(50), nullable=False, comment="工場コード")
    deptcd = Column(String(50), nullable=False, comment="部門コード")
    itemgr = Column(String(1), nullable=False, comment="品目グループ")
    itemcd = Column(String(50), nullable=False, comment="品目コード")
    linecd = Column(String(50), nullable=False, default="", comment="ラインコード")
    routno = Column(Numeric, nullable=False, comment="工程番号")

    # 倉庫・MOS
    whcd = Column(String(50), comment="倉庫コード")
    mos = Column(String(1), comment="")

    # 工程情報
    prccd = Column(String(50), comment="工程コード")
    prclt = Column(Numeric, comment="工程リードタイム")
    prccap = Column(Numeric, comment="工程能力")
    isptyp = Column(String(2), comment="")

    # 時間情報
    arngtm = Column(Numeric, comment="段取時間")
    proctm = Column(Numeric, comment="加工時間")

    # アカウント・条件
    actcd = Column(String(50), comment="")
    manjor = Column(String(300), default="", comment="")
    routstdcos = Column(Numeric, comment="")

    # ロケーション・単価
    loccd = Column(String(50), comment="")
    unitprice = Column(Numeric, comment="単価")
    uniptyp = Column(String(1), comment="")
    ccptyp = Column(String(4), comment="")

    # 発注形式・バース
    ordfm = Column(String(150), comment="")
    berthcd = Column(String(50), comment="")

    # 曜日有効フラグ
    ensun = Column(String(1), comment="日曜有効")
    enmon = Column(String(1), comment="月曜有効")
    entue = Column(String(1), comment="火曜有効")
    enwed = Column(String(1), comment="水曜有効")
    enthu = Column(String(1), comment="木曜有効")
    enfri = Column(String(1), comment="金曜有効")
    ensat = Column(String(1), comment="土曜有効")

    # 仕向地・有効フラグ
    dest = Column(String(50), comment="")
    enrout = Column(String(1), comment="")

    # システム情報
    deldt = Column(DateTime, comment="削除日時")
    ludate = Column(DateTime, comment="最終更新日時")
    uuser = Column(String(20), comment="更新ユーザー")
    udate = Column(DateTime, comment="更新日時")

    # 作業順序・比率
    ordoprno = Column(Numeric, default=0, comment="")
    prcratio = Column(Numeric, default=0, comment="")
    prcprnttyp = Column(String(2), default="", comment="")

    # 工程・時間
    metwccd = Column(String(50), default="", comment="")
    rmvltm = Column(Numeric, default=0, comment="")

    # 単価1-3
    unitprice1 = Column(Numeric, default=0, comment="")
    unitprice2 = Column(Numeric, default=0, comment="")
    unitprice3 = Column(Numeric, default=0, comment="")

    # 工程人員・段取メモ
    prcpes = Column(Numeric, default=0, comment="")
    defpresetupmemo = Column(String(300), default="", comment="")
    prcitvlt = Column(Numeric, default=0, comment="")

    # 制約定義
    __table_args__ = (
        UniqueConstraint(
            "fctcd",
            "deptcd",
            "itemgr",
            "itemcd",
            "linecd",
            "routno",
            name="rout_fctcd_deptcd_itemgr_itemcd_linecd_routno_key",
        ),
        Index(
            "rout_fctcd_deptcd_itemgr_itemcd_linecd_routno_deldt",
            "fctcd",
            "deptcd",
            "itemgr",
            "itemcd",
            "linecd",
            "routno",
            "deldt",
            unique=True,
        ),
    )

    def __repr__(self):
        return f"<Rout(prkey={self.prkey}, itemcd={self.itemcd}, routno={self.routno})>"
