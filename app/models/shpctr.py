from sqlalchemy import Column, String, BigInteger, Numeric, DateTime
from sqlalchemy import UniqueConstraint
from app.core.database import Base


class Shpctr(Base):
    """納入場所マスタ/店舗（SHPCTR）- 全78カラム定義"""

    __tablename__ = "shpctr"

    # プライマリキー
    prkey = Column(BigInteger, primary_key=True, comment="プライマリキー")

    # 基本情報
    fctcd = Column(String(50), nullable=False, default="", comment="工場コード")
    cuscd = Column(String(50), nullable=False, default="", comment="得意先コード")
    shpctrcd = Column(String(50), nullable=False, default="", comment="納入場所コード")
    shpctrnm = Column(String(150), default="", comment="納入場所名称")
    shpctrabb = Column(String(150), default="", comment="納入場所略称")

    # 住所情報
    zip = Column(String(20), default="", comment="郵便番号")
    add1 = Column(String(150), default="", comment="住所1")
    add2 = Column(String(150), default="", comment="住所2")

    # 連絡先
    email = Column(String(100), default="", comment="メールアドレス")
    tel = Column(String(20), default="", comment="電話番号")
    fax = Column(String(20), default="", comment="FAX番号")

    # 取引期間
    stadeal = Column(String(8), default="", comment="取引開始日")
    enddeal = Column(String(8), default="", comment="取引終了日")

    # 表示
    dispno = Column(Numeric, default=0, comment="表示順")

    # システム情報
    deldt = Column(DateTime, comment="削除日時")
    ludate = Column(DateTime, comment="最終更新日時")
    uuser = Column(String(20), default="", comment="更新ユーザー")
    udate = Column(DateTime, comment="更新日時")

    # カラー・カナ名
    shpctrcolor = Column(String(10), comment="色コード")
    shpctrcannm = Column(String(100), default="", comment="カナ名称")

    # 配送センター・配送番号
    distctrcd = Column(String(50), default="", comment="配送センターコード")
    distno = Column(Numeric, default=0, comment="配送番号")

    # カテゴリ・配送
    shpctgcd = Column(String(50), default="", comment="")
    prfarcd = Column(String(50), default="", comment="")
    trcd = Column(String(50), default="", comment="運送業者コード")

    # 異名コード1-2
    shpctranocd1 = Column(String(50), default="", comment="")
    shpctranocd2 = Column(String(50), default="", comment="")

    # グループ・CSV
    shpctrgrp = Column(String(50), default="", comment="")
    csvplandelvcd = Column(String(50), default="", comment="")
    spectyp01 = Column(String(50), default="", comment="")

    # 名称・印刷名
    shpctrkanna = Column(String(150), default="", comment="")
    shpctrprntnm = Column(String(150), default="", comment="")

    # ソース会社・名称2
    srccompcd = Column(String(50), default="", comment="")
    shpctrnm2 = Column(String(150), default="", comment="")

    # 条件・追加情報
    shpctrjor = Column(String(1000), default="", comment="")
    shpctraddinfo01 = Column(String(300), default="", comment="")
    shpctraddinfo02 = Column(String(300), default="", comment="")
    shpctraddinfo03 = Column(String(300), default="", comment="")
    shpctraddinfo04 = Column(String(300), default="", comment="")
    shpctraddinfo05 = Column(String(300), default="", comment="")
    shpctraddinfo06 = Column(String(300), default="", comment="")
    shpctraddinfo07 = Column(String(300), default="", comment="")
    shpctraddinfo08 = Column(String(300), default="", comment="")
    shpctraddinfo09 = Column(String(300), default="", comment="")
    shpctraddinfo10 = Column(String(300), default="", comment="")

    # サブ配送センター・在庫グループ
    subdistctrcd = Column(String(50), default="", comment="")
    rsvstocgrpcd = Column(String(50), default="", comment="")

    # 印刷タイプ
    delvedtprnttyp = Column(String(2), default="0", comment="")
    delvmemoprnttyp = Column(String(2), default="0", comment="")

    # 単価グループ・課金グループ
    unitprcgrpcd = Column(String(50), default="", comment="")
    crgengrpcd01 = Column(String(50), default="", comment="")

    # ライン・担当
    linecd = Column(String(50), default="", comment="")
    shpctrpiccd = Column(String(50), default="", comment="")

    # 追加情報11-20
    shpctraddinfo11 = Column(String(300), default="", comment="")
    shpctraddinfo12 = Column(String(300), default="", comment="")
    shpctraddinfo13 = Column(String(300), default="", comment="")
    shpctraddinfo14 = Column(String(300), default="", comment="")
    shpctraddinfo15 = Column(String(300), default="", comment="")
    shpctraddinfo16 = Column(String(300), default="", comment="")
    shpctraddinfo17 = Column(String(300), default="", comment="")
    shpctraddinfo18 = Column(String(300), default="", comment="")
    shpctraddinfo19 = Column(String(300), default="", comment="")
    shpctraddinfo20 = Column(String(300), default="", comment="")

    # オプション情報1-10
    shpctroptinfo01 = Column(String(50), default="", comment="")
    shpctroptinfo02 = Column(String(50), default="", comment="")
    shpctroptinfo03 = Column(String(50), default="", comment="")
    shpctroptinfo04 = Column(String(50), default="", comment="")
    shpctroptinfo05 = Column(String(50), default="", comment="")
    shpctroptinfo06 = Column(String(50), default="", comment="")
    shpctroptinfo07 = Column(String(50), default="", comment="")
    shpctroptinfo08 = Column(String(50), default="", comment="")
    shpctroptinfo09 = Column(String(50), default="", comment="")
    shpctroptinfo10 = Column(String(50), default="", comment="")

    # 販売日タイプ・廃止フラグ
    shpctrsaldttyp = Column(String(1), default="", comment="")
    disuseshpctr = Column(String(1), default="0", comment="")

    # メール件名・本文
    emailsub = Column(String(100), comment="")
    emailbody = Column(String(1000), comment="")

    # 制約定義
    __table_args__ = (
        UniqueConstraint(
            "fctcd", "cuscd", "shpctrcd", name="shpctr_fctcd_cuscd_shpctrcd_key"
        ),
    )

    def __repr__(self):
        return f"<Shpctr(prkey={self.prkey}, shpctrcd={self.shpctrcd}, shpctrnm={self.shpctrnm})>"
