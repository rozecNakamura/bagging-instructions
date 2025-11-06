from sqlalchemy import Column, String, BigInteger, Numeric, DateTime
from sqlalchemy import UniqueConstraint, Index
from app.core.database import Base


class Workc(Base):
    """工程マスタ（WORKC）- 全39カラム定義"""

    __tablename__ = "workc"

    # プライマリキー
    prkey = Column(BigInteger, primary_key=True, comment="")

    # 基本情報
    fctcd = Column(String(50), default="", comment="")
    wccd = Column(String(50), default="", comment="")
    wcnm = Column(String(150), default="", comment="")

    # 能力・レート
    stdcap = Column(Numeric, default=0, comment="")
    manrate = Column(Numeric, default=0, comment="")

    # システム情報
    deldt = Column(DateTime, comment="")
    ludate = Column(DateTime, comment="")
    uuser = Column(String(20), default="", comment="")
    udate = Column(DateTime, comment="")

    # メモ・テンプレート
    outmemo = Column(String(500), default="", comment="")
    exceltempfile = Column(String(50), default="", comment="")
    crtempfile = Column(String(50), default="", comment="")

    # リードタイム・表示
    ordlt = Column(Numeric, default=0, comment="")
    crdispcnt = Column(Numeric, default=0, comment="")
    dispno = Column(Numeric, default=0, comment="")

    # 工程情報名
    wcinfnm = Column(String(150), default="", comment="")
    wcwhcd = Column(String(50), default="", comment="")

    # オーダー・作業設定
    ordoprnodisptyp = Column(String(1), default="", comment="")
    workprcptncd = Column(String(50), default="", comment="")
    workcgrpcd = Column(String(50), default="", comment="")

    # 時刻設定
    statm = Column(String(4), default="", comment="")
    endtm = Column(String(4), default="", comment="")

    # 能力レート・テンプレート2
    caprate = Column(Numeric, default=0, comment="")
    crtempfile2 = Column(String(50), default="", comment="")
    crdispcnt2 = Column(Numeric, default=0, comment="")

    # 工数・人数の境界値
    wktmbdr = Column(Numeric, default=0, comment="")
    personbdr = Column(Numeric, default=0, comment="")

    # 能力・部門
    capacity = Column(Numeric, comment="")
    deptcd = Column(String, comment="")

    # 月次作業計画表示
    monthlyworkplan_display = Column(String(1), default="1", comment="")

    # テンプレート3-4
    exceltempfile2 = Column(String(50), default="", comment="")
    crtempfile3 = Column(String(50), default="", comment="")
    crdispcnt3 = Column(Numeric, default=0, comment="")

    # 工程条件
    workcjor = Column(String(1000), default="", comment="")
    crtempfile4 = Column(String(50), default="", comment="")
    crdispcnt4 = Column(Numeric, default=0, comment="")

    # 作業時間タイプ・製造サイズ
    worktmtyp = Column(String(1), default="", comment="")
    prdsiz = Column(Numeric, default=0, comment="")

    # 制約定義
    __table_args__ = (
        UniqueConstraint("fctcd", "wccd", "deldt", name="workc_fctcd_wccd_deldt"),
        UniqueConstraint("fctcd", "wccd", name="workc_fctcd_wccd_key"),
        UniqueConstraint("fctcd", "wccd", name="workc_fctcd_wccd_unique"),
        Index(
            "workc_dept_display_idx",
            "fctcd",
            "deptcd",
            "monthlyworkplan_display",
        ),
        Index("workc_pkey", "prkey", unique=True),
    )

    # ========================================
    # リレーション定義
    # ========================================
    # 今後必要に応じてリレーションを追加

    def __repr__(self):
        return f"<Workc(prkey={self.prkey}, fctcd={self.fctcd}, wccd={self.wccd}, wcnm={self.wcnm})>"
