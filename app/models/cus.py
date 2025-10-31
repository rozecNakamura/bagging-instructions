from sqlalchemy import Column, String, BigInteger, Numeric, DateTime
from sqlalchemy import UniqueConstraint, Index
from app.core.database import Base


class Cus(Base):
    """得意先マスタ（CUS）- 全110カラム定義"""

    __tablename__ = "cus"

    # プライマリキー
    prkey = Column(BigInteger, primary_key=True, comment="プライマリキー")

    # 基本情報
    fctcd = Column(String(50), nullable=False, default="", comment="工場コード")
    cuscd = Column(String(50), nullable=False, default="", comment="得意先コード")
    cusnm = Column(String(150), default="", comment="得意先名称")
    cusinfnm = Column(String(150), default="", comment="")
    cuscannm = Column(String(150), default="", comment="")

    # 住所情報
    zip = Column(String(20), default="", comment="郵便番号")
    add1 = Column(String(150), default="", comment="住所1")
    add2 = Column(String(150), default="", comment="住所2")

    # 連絡先
    email = Column(String(100), default="", comment="メールアドレス")
    tel = Column(String(20), default="", comment="電話番号")
    fax = Column(String(20), default="", comment="FAX番号")

    # 担当者
    cuspicnm = Column(String(50), default="", comment="担当者名")
    piccd = Column(String(50), default="", comment="担当コード")

    # 業種・期間
    indtyp = Column(String(50), default="", comment="業種タイプ")
    salstadt = Column(String(8), default="", comment="販売開始日")
    salenddt = Column(String(8), default="", comment="販売終了日")

    # 取引条件
    amosiz = Column(String(1), default="", comment="")
    uniptaxtyp = Column(String(1), default="", comment="")
    taxacc = Column(String(1), default="", comment="")
    taxrod = Column(String(1), default="", comment="")

    # 請求・帳票
    biladrcd = Column(String(50), default="", comment="請求先コード")
    exceltempfile = Column(String(50), default="", comment="Excelテンプレートファイル")

    # テンプレートファイル1-10
    crtempfile1 = Column(String(50), default="", comment="")
    crdispcnt1 = Column(Numeric, default=0, comment="")
    crtempfile2 = Column(String(50), comment="")
    crtempfile3 = Column(String(50), comment="")
    crdispcnt2 = Column(Numeric, default=0, comment="")
    crdispcnt3 = Column(Numeric, default=0, comment="")
    crtempfile4 = Column(String(50), default="", comment="")
    crdispcnt4 = Column(Numeric, default=0, comment="")
    crtempfile5 = Column(String(50), default="", comment="")
    crdispcnt5 = Column(Numeric, default=0, comment="")
    crtempfile6 = Column(String(50), default="", comment="")
    crdispcnt6 = Column(Numeric, default=0, comment="")
    crtempfile7 = Column(String(50), default="", comment="")
    crdispcnt7 = Column(Numeric, default=0, comment="")
    crtempfile8 = Column(String(50), default="", comment="")
    crdispcnt8 = Column(Numeric, default=0, comment="")
    crtempfile9 = Column(String(50), default="", comment="")
    crdispcnt9 = Column(Numeric, default=0, comment="")
    crtempfile10 = Column(String(50), default="", comment="")
    crdispcnt10 = Column(Numeric, default=0, comment="")

    # 受注関連
    jobordcnt = Column(Numeric, default=0, comment="")
    cuscolor = Column(String(10), comment="")
    dispno = Column(Numeric, default=0, comment="表示順")

    # システム情報
    deldt = Column(DateTime, comment="削除日時")
    ludate = Column(DateTime, comment="最終更新日時")
    uuser = Column(String(20), default="", comment="更新ユーザー")
    udate = Column(DateTime, comment="更新日時")

    # 日数・期間
    saldays = Column(Numeric, default=0, comment="販売日数")
    cusprdgrpcd = Column(String(50), default="", comment="")

    # 表示カラム数1-10
    crdispcolcnt1 = Column(Numeric, default=0, comment="")
    crdispcolcnt2 = Column(Numeric, default=0, comment="")
    crdispcolcnt3 = Column(Numeric, default=0, comment="")
    crdispcolcnt4 = Column(Numeric, default=0, comment="")
    crdispcolcnt5 = Column(Numeric, default=0, comment="")
    crdispcolcnt6 = Column(Numeric, default=0, comment="")
    crdispcolcnt7 = Column(Numeric, default=0, comment="")
    crdispcolcnt8 = Column(Numeric, default=0, comment="")
    crdispcolcnt9 = Column(Numeric, default=0, comment="")
    crdispcolcnt10 = Column(Numeric, default=0, comment="")

    # 配送ルート
    prfarcd = Column(String(50), default="", comment="")
    cusgrpcd = Column(String(50), default="", comment="得意先グループコード")
    delvroucd1 = Column(String(50), default="", comment="配送ルートコード1")
    delvroucd2 = Column(String(50), default="", comment="配送ルートコード2")

    # 異名コード・印刷名
    cusanocd1 = Column(String(50), default="", comment="")
    cusprntnm = Column(String(150), default="", comment="")

    # 日数情報
    shpdays = Column(Numeric, default=0, comment="出荷日数")
    prddays = Column(Numeric, default=0, comment="生産日数")

    # チャネル・価格
    cuschnlcd = Column(String(50), default="", comment="")
    cusretailprcper = Column(Numeric, default=0, comment="")

    # 曜日別スコア
    jobordscoresun = Column(Numeric, default=0, comment="日曜スコア")
    jobordscoremon = Column(Numeric, default=0, comment="月曜スコア")
    jobordscoretue = Column(Numeric, default=0, comment="火曜スコア")
    jobordscorewed = Column(Numeric, default=0, comment="水曜スコア")
    jobordscorethu = Column(Numeric, default=0, comment="木曜スコア")
    jobordscorefri = Column(Numeric, default=0, comment="金曜スコア")
    jobordscoresat = Column(Numeric, default=0, comment="土曜スコア")

    # 追加情報1-10
    cusaddinfo01 = Column(String(50), default="", comment="")
    cusaddinfo02 = Column(String(50), default="", comment="")
    cusaddinfo03 = Column(String(50), default="", comment="")
    cusaddinfo04 = Column(String(50), default="", comment="")
    cusaddinfo05 = Column(String(50), default="", comment="")
    cusaddinfo06 = Column(String(50), default="", comment="")
    cusaddinfo07 = Column(String(50), default="", comment="")
    cusaddinfo08 = Column(String(50), default="", comment="")
    cusaddinfo09 = Column(String(50), default="", comment="")
    cusaddinfo10 = Column(String(50), default="", comment="")

    # 名称2・条件
    cusnm2 = Column(String(150), default="", comment="")
    cusjor = Column(String(1000), default="", comment="")

    # 曜日別出荷日数
    shpdayssun = Column(Numeric, default=0, comment="日曜出荷日数")
    shpdaysmon = Column(Numeric, default=0, comment="月曜出荷日数")
    shpdaystue = Column(Numeric, default=0, comment="火曜出荷日数")
    shpdayswed = Column(Numeric, default=0, comment="水曜出荷日数")
    shpdaysthu = Column(Numeric, default=0, comment="木曜出荷日数")
    shpdaysfri = Column(Numeric, default=0, comment="金曜出荷日数")
    shpdayssat = Column(Numeric, default=0, comment="土曜出荷日数")

    # 在庫・請求
    cusrsvstocdays = Column(Numeric, default=0, comment="")
    bildttyp = Column(String(1), default="0", comment="")
    custyp = Column(String(1), default="0", comment="")

    # グループコード
    cuswidgrpcd = Column(String(50), default="", comment="")
    cusmidgrpcd = Column(String(50), default="", comment="")
    cusnargrpcd = Column(String(50), default="", comment="")

    # 受注明細・単価
    joborddtlnocd = Column(String(50), default="", comment="")
    unitprcgrpcd = Column(String(50), default="", comment="")

    # ライン・配送
    linecd = Column(String(50), default="", comment="")
    delvprtcnt = Column(Numeric, default=1, comment="")

    # 販売日タイプ
    cussaldttyp = Column(String(1), default="", comment="")
    cussalrectyp = Column(String(1), default="", comment="")

    # 制約定義
    __table_args__ = (
        UniqueConstraint("fctcd", "cuscd", name="cus_fctcd_cuscd_key"),
        Index("cus_biladrcd", "biladrcd"),
    )

    def __repr__(self):
        return f"<Cus(prkey={self.prkey}, cuscd={self.cuscd}, cusnm={self.cusnm})>"
