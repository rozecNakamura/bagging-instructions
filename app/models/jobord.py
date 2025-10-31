from sqlalchemy import Column, String, BigInteger, Numeric, DateTime
from sqlalchemy import UniqueConstraint, Index
from app.core.database import Base


class Jobord(Base):
    """受注明細（JOBORD）- 全133カラム定義"""

    __tablename__ = "jobord"

    # プライマリキー
    prkey = Column(BigInteger, primary_key=True, comment="プライマリキー")

    # 基本情報
    merfctcd = Column(String(50), nullable=False, default="", comment="")
    jobordno = Column(String(20), nullable=False, default="", comment="受注番号")
    jobordsno = Column(Numeric, nullable=False, default=0, comment="受注明細番号")
    shpsts = Column(String(1), default="", comment="出荷ステータス")

    # 品目情報
    cusitemcd = Column(String(50), default="", comment="得意先品目コード")
    fctcd = Column(String(50), default="", comment="工場コード")
    deptcd = Column(String(50), default="", comment="部門コード")
    itemgr = Column(String(1), default="", comment="品目グループ")
    itemcd = Column(String(50), default="", comment="品目コード")

    # 受注情報
    joborddt = Column(String(50), default="", comment="受注日")
    jobordqun = Column(Numeric, default=0, comment="受注数量")
    joborduni = Column(String(50), default="", comment="受注単位")
    jobordcon = Column(Numeric, default=0, comment="")
    jobordunip = Column(Numeric, default=0, comment="受注単価")
    jobordvol = Column(Numeric, default=0, comment="受注金額")
    jobordtax = Column(Numeric, default=0, comment="受注税額")

    # 得意先情報
    cuscd = Column(String(50), default="", comment="得意先コード")
    shpctrcd = Column(String(50), default="", comment="納入場所コード")
    piccd = Column(String(50), default="", comment="担当コード")

    # タイプ・条件
    jobordtyp = Column(String(3), default="", comment="")
    jobordjor = Column(String(500), default="", comment="")
    stocprvqun = Column(Numeric, default=0, comment="")

    # 日付情報
    delvedt = Column(String(8), default="", comment="納品日")
    schdshpdt = Column(String(8), default="", comment="予定出荷日")
    shpdt = Column(String(8), default="", comment="出荷日")
    shptm = Column(String(4), default="", comment="出荷時刻")

    # 出荷数量
    shpqun = Column(Numeric, default=0, comment="出荷数量")
    kepshpqun = Column(Numeric, default=0, comment="")

    # 売上記録
    sasrecdt = Column(String(8), default="", comment="")
    sasjobordlstno = Column(String(9), default="", comment="")
    seltyp = Column(String(1), default="", comment="")
    shpdelvdt = Column(String(8), default="", comment="")

    # オプション情報1-4
    jobordoptinfo1 = Column(String(50), default="", comment="")
    jobordoptinfo2 = Column(String(50), default="", comment="")
    jobordoptinfo3 = Column(String(50), default="", comment="")
    jobordoptinfo4 = Column(String(50), default="", comment="")

    # 出荷ブロック
    shpblocksts = Column(String(1), default="", comment="")
    shpblockqun = Column(Numeric, default=0, comment="")

    # システム情報
    uuser = Column(String(20), default="", comment="更新ユーザー")
    udate = Column(DateTime, comment="更新日時")

    # 名称・日付
    jobordmernm = Column(String(150), comment="")
    prddt = Column(String(8), comment="製造日")

    # 配送メモ・注文番号
    delvmemo = Column(String(500), default="", comment="")
    cusitemordno = Column(String(50), default="", comment="")
    shopprc = Column(Numeric, comment="")
    hiddenjor = Column(String(500), default="", comment="")

    # 受注登録タイプ・計画
    jobordregtyp = Column(String(3), default="", comment="")
    plandelvcd = Column(String(50), default="", comment="")
    jobordkeptyp = Column(String(2), default="", comment="")
    shpctritemcd = Column(String(50), default="", comment="")

    # グループ・運送
    jobordgrp1 = Column(String(50), default="", comment="")
    trcd = Column(String(50), default="", comment="")
    jobordtaxinc = Column(Numeric, default=0, comment="")

    # 原産地・等級
    orgplacecd = Column(String(50), default="", comment="")
    gradecd = Column(String(50), default="", comment="")
    organic = Column(String(1), default="", comment="")

    # 在庫サブ情報1-10
    stocsubinfo01 = Column(String(50), default="", comment="")
    stocsubinfo02 = Column(String(50), default="", comment="")
    stocsubinfo03 = Column(String(50), default="", comment="")
    stocsubinfo04 = Column(String(50), default="", comment="")
    stocsubinfo05 = Column(String(50), default="", comment="")
    stocsubinfo06 = Column(String(50), default="", comment="")
    stocsubinfo07 = Column(String(50), default="", comment="")
    stocsubinfo08 = Column(String(50), default="", comment="")
    stocsubinfo09 = Column(String(50), default="", comment="")
    stocsubinfo10 = Column(String(50), default="", comment="")

    # 名称情報
    orgplacenm = Column(String(150), default="", comment="")
    gradenm = Column(String(150), default="", comment="")

    # 在庫サブ情報名称1-10
    stocsubinfonm01 = Column(String(150), default="", comment="")
    stocsubinfonm02 = Column(String(150), default="", comment="")
    stocsubinfonm03 = Column(String(150), default="", comment="")
    stocsubinfonm04 = Column(String(150), default="", comment="")
    stocsubinfonm05 = Column(String(150), default="", comment="")
    stocsubinfonm06 = Column(String(150), default="", comment="")
    stocsubinfonm07 = Column(String(150), default="", comment="")
    stocsubinfonm08 = Column(String(150), default="", comment="")
    stocsubinfonm09 = Column(String(150), default="", comment="")
    stocsubinfonm10 = Column(String(150), default="", comment="")

    # 数量情報
    carqun = Column(Numeric, default=0, comment="")
    casequn = Column(Numeric, default=0, comment="")
    jobordwei = Column(Numeric, default=0, comment="")

    # ロック・確認
    shplock = Column(String(1), default="", comment="")
    confshpqun = Column(Numeric, default=0, comment="")

    # CSV・伝票
    csvplandelvcd = Column(String(50), default="", comment="")
    nagcsslipno = Column(String(20), default="", comment="")
    nagcsslipsno = Column(Numeric, default=0, comment="")

    # 規格・カナ
    fullmeasure = Column(String(150), default="", comment="")
    jobordmerkanna = Column(String(150), default="", comment="")
    csvuninm = Column(String(150), default="", comment="")

    # 編集フラグ
    editflg = Column(String(1), default="", comment="")

    # 不規則オプション1-4
    irregularopt01 = Column(String(50), default="", comment="")
    irregularopt02 = Column(String(50), default="", comment="")
    irregularopt03 = Column(String(50), default="", comment="")
    irregularopt04 = Column(String(50), default="", comment="")

    # 予定時刻・サブ納入場所
    schdshptm = Column(String(4), default="", comment="")
    subshpctrcd = Column(String(50), default="", comment="")
    delvetm = Column(String(4), default="", comment="")

    # 予測注文
    fcstordno = Column(String(20), default="", comment="")
    fcstordsno = Column(Numeric, default=0, comment="")

    # 倉庫・オプション名
    whcd = Column(String(50), default="", comment="")
    cusoptnm = Column(String(150), default="", comment="")

    # 自動予約在庫
    atrsvstoctgtflg = Column(String(1), default="", comment="")
    boardqun = Column(Numeric, default=0, comment="")

    # その他伝票
    otherslipno1 = Column(String(20), default="", comment="")
    otherslipsno1 = Column(Numeric, default=0, comment="")

    # 印刷ステータス・継続
    prtsts = Column(String(1), default="0", comment="")
    contjobordno = Column(String(20), default="", comment="")

    # 配送・請求
    delvememo = Column(String(50), default="", comment="")
    delvtzcd = Column(String(50), default="", comment="")
    bildt = Column(String(8), default="", comment="")
    cusindcd = Column(String(50), default="", comment="")

    # 条件2-5
    jobordjor2 = Column(String(500), default="", comment="")
    jobordjor3 = Column(String(500), default="", comment="")
    jobordjor4 = Column(String(500), default="", comment="")
    jobordjor5 = Column(String(500), default="", comment="")

    # 明細番号・契約月
    joborddtlno = Column(String(10), default="", comment="")
    contmonth = Column(String(8), default="", comment="")
    joborddtlno2 = Column(String(20), default="", comment="")

    # 税率・保留
    jobordtaxrate = Column(Numeric, default=0, comment="")
    pendshpdtflg = Column(String(1), default="", comment="")

    # エラーフラグ1-4
    dataerrflg01 = Column(String(1), default="0", comment="")
    dataerrflg02 = Column(String(1), default="0", comment="")
    dataerrflg03 = Column(String(1), default="0", comment="")
    dataerrflg04 = Column(String(1), default="0", comment="")

    # ライン
    linecd = Column(String(50), default="", comment="")

    # 汎用印刷フラグ1-5
    genprtflg01 = Column(String(1), default="0", comment="")
    genprtflg02 = Column(String(1), default="0", comment="")
    genprtflg03 = Column(String(1), default="0", comment="")
    genprtflg04 = Column(String(1), default="0", comment="")
    genprtflg05 = Column(String(1), default="0", comment="")

    # コスト
    delvcost = Column(Numeric, default=0, comment="")
    delvothercost = Column(Numeric, default=0, comment="")

    # 制約定義
    __table_args__ = (
        UniqueConstraint(
            "merfctcd",
            "jobordno",
            "jobordsno",
            name="jobord_merfctcd_jobordno_jobordsno_key",
        ),
        Index(
            "jobord_index1",
            "merfctcd",
            "cuscd",
            "shpctrcd",
            "linecd",
            "schdshpdt",
            "delvedt",
            "prddt",
            "fctcd",
            "deptcd",
            "itemgr",
            "itemcd",
        ),
        Index("jobord_index2", "fctcd", "deptcd", "itemgr", "itemcd", "schdshpdt"),
        Index("jobord_index3", "fctcd", "deptcd", "itemgr", "itemcd", "delvedt"),
        Index("jobord_index4", "fctcd", "deptcd", "itemgr", "itemcd", "prddt"),
        Index("jobord_merfctcd_cusitemordno", "merfctcd", "cusitemordno"),
        Index(
            "jobord_merfctcd_shpsts_schdshpdt_delvedt",
            "merfctcd",
            "shpsts",
            "schdshpdt",
            "delvedt",
        ),
    )

    def __repr__(self):
        return f"<Jobord(prkey={self.prkey}, jobordno={self.jobordno}, jobordsno={self.jobordsno})>"
