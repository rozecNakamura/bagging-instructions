from sqlalchemy import Column, String, BigInteger, Numeric, DateTime
from sqlalchemy import Index
from app.core.database import Base


class Daystoc(Base):
    """日次在庫テーブル（DAYSTOC）- 全28カラム定義"""

    __tablename__ = "daystoc"

    # プライマリキー
    prkey = Column(BigInteger, primary_key=True, comment="プライマリキー")

    # 工場・部門情報
    fctcd = Column(String(50), nullable=False, default="", comment="工場コード")
    deptcd = Column(String(50), nullable=False, default="", comment="部門コード")

    # 品目情報
    itemgr = Column(String(1), nullable=False, default="", comment="品目グループ")
    itemcd = Column(String(50), nullable=False, default="", comment="品目コード")

    # ロケーション情報
    loctyp = Column(String(1), nullable=False, default="", comment="ロケーションタイプ")
    loccd = Column(String(50), nullable=False, default="", comment="ロケーションコード")

    # ロット・出荷情報
    lotno = Column(String(50), nullable=False, default="", comment="ロット番号")
    sheno = Column(String(50), nullable=False, default="", comment="出荷番号")

    # 日付情報
    bbdt = Column(String(8), nullable=False, default="", comment="賞味期限日")
    stocdt = Column(String(8), nullable=False, default="", comment="在庫日")

    # 在庫数量
    actstoc = Column(Numeric, nullable=False, default=0, comment="実在庫数量")
    stocuni0 = Column(Numeric, nullable=False, default=0, comment="在庫単位0")
    stocuni1 = Column(Numeric, nullable=False, default=0, comment="在庫単位1")
    stocuni2 = Column(Numeric, nullable=False, default=0, comment="在庫単位2")
    stocuni3 = Column(Numeric, nullable=False, default=0, comment="在庫単位3")

    # フラグ・ユーザー情報
    fixflg = Column(String(1), nullable=False, default="", comment="確定フラグ")
    uuser = Column(String(20), nullable=False, default="", comment="更新ユーザー")
    udate = Column(DateTime, comment="更新日時")

    # 単価・移動日時
    unitprice = Column(Numeric, nullable=False, default=0, comment="単価")
    movedt = Column(DateTime, comment="移動日時")

    # 受入出荷情報
    rcvdelvtyp = Column(String(2), nullable=False, default="", comment="受入出荷タイプ")
    rpkordno = Column(String(20), nullable=False, default="", comment="梱包指示番号")
    rpktyp = Column(String(2), nullable=False, default="", comment="梱包タイプ")

    # 生産日時・出荷ロック
    proddate = Column(DateTime, comment="生産日時")
    shplock = Column(String(1), nullable=False, default="", comment="出荷ロック")

    # 不良品情報
    irrstocuni = Column(Numeric, nullable=False, default=0, comment="不良在庫単位")
    irrcar = Column(Numeric, nullable=False, default=0, comment="不良運搬数")

    # インデックス定義
    __table_args__ = (
        Index(
            "daystoc_index1",
            "fctcd",
            "deptcd",
            "itemgr",
            "itemcd",
            "stocdt",
            "loccd",
            "lotno",
            "sheno",
            "bbdt",
        ),
        Index("daystoc_loccd_sheno", "loccd", "sheno"),
    )

    def __repr__(self):
        return f"<Daystoc(prkey={self.prkey}, itemcd={self.itemcd}, stocdt={self.stocdt}, actstoc={self.actstoc})>"
