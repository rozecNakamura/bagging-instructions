from sqlalchemy import Column, String, BigInteger, Numeric, DateTime
from sqlalchemy import UniqueConstraint
from app.core.database import Base


class Cusmcd(Base):
    """得意先品目変換マスタ（CUSMCD）- 全75カラム定義"""

    __tablename__ = "cusmcd"

    # プライマリキー
    prkey = Column(BigInteger, primary_key=True, comment="プライマリキー")

    # キー情報
    merfctcd = Column(String(50), nullable=False, default="", comment="")
    cuscd = Column(String(50), nullable=False, default="", comment="得意先コード")
    cusitemcd = Column(String(50), nullable=False, default="", comment="得意先品目コード")
    cusitemnm = Column(String(150), default="", comment="得意先品目名称")

    # 自社品目情報
    fctcd = Column(String(50), default="", comment="工場コード")
    deptcd = Column(String(50), default="", comment="部門コード")
    itemgr = Column(String(1), default="", comment="品目グループ")
    itemcd = Column(String(50), default="", comment="品目コード")

    # システム情報
    uuser = Column(String(20), default="", comment="更新ユーザー")
    udate = Column(DateTime, comment="更新日時")

    # 価格情報
    salprc0 = Column(Numeric, default=0, comment="販売価格0")
    salprc1 = Column(Numeric, default=0, comment="販売価格1")
    salprc2 = Column(Numeric, default=0, comment="販売価格2")
    salprc3 = Column(Numeric, default=0, comment="販売価格3")

    # 出荷ブロック
    shpblock = Column(String(1), default="", comment="")
    shpblockqun = Column(Numeric, default=0, comment="")

    # 販売メモ
    salmemo1 = Column(String(300), default="", comment="")
    salmemo2 = Column(String(300), default="", comment="")
    salmemo3 = Column(String(300), default="", comment="")
    salmemo4 = Column(String(300), default="", comment="")

    # 店舗価格
    shopprcexc0 = Column(Numeric, default=0, comment="")
    shopprcinc0 = Column(Numeric, default=0, comment="")
    shopprcexc1 = Column(Numeric, default=0, comment="")
    shopprcexc2 = Column(Numeric, default=0, comment="")
    shopprcexc3 = Column(Numeric, default=0, comment="")
    shopprcinc1 = Column(Numeric, default=0, comment="")
    shopprcinc2 = Column(Numeric, default=0, comment="")
    shopprcinc3 = Column(Numeric, default=0, comment="")

    # 廃止フラグ
    disusecusitem = Column(String(1), default="", comment="")

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

    # カナ
    cusitemkanna = Column(String(150), default="", comment="")

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

    # 表示順
    dispno = Column(Numeric, default=0, comment="")

    # オプション1-4
    cusitemoption01 = Column(String(50), default="", comment="")
    cusitemoption02 = Column(String(50), default="", comment="")
    cusitemoption03 = Column(String(50), default="", comment="")
    cusitemoption04 = Column(String(50), default="", comment="")

    # 重量・日数
    cusitemwei = Column(Numeric, default=0, comment="")
    cusitembbdtadddays = Column(Numeric, default=0, comment="")
    tempcusitemcd = Column(String(1), default="0", comment="")

    # 規格・数量
    fullmeasure = Column(String(150), default="", comment="")
    car = Column(Numeric, default=0, comment="")
    nwei = Column(Numeric, default=0, comment="")
    contqun = Column(Numeric, default=0, comment="")

    # 価格・単位
    cusitemretailprcper = Column(Numeric, default=0, comment="")
    jouni = Column(String(50), default="", comment="")

    # 異名コード
    cusitemanocd1 = Column(String(50), default="", comment="")
    cusitemanocd2 = Column(String(50), default="", comment="")

    # サンプル・マージン
    samplequn = Column(Numeric, default=0, comment="")
    cusitemmargin = Column(Numeric, default=0, comment="")
    margintyp = Column(String(1), default="", comment="")

    # 制約定義
    __table_args__ = (
        UniqueConstraint(
            "merfctcd",
            "cuscd",
            "cusitemcd",
            "cusitemkanna",
            name="cusmcd_merfctcd_cuscd_cusitemcd_cusitemkanna",
        ),
    )

    def __repr__(self):
        return f"<Cusmcd(prkey={self.prkey}, cuscd={self.cuscd}, cusitemcd={self.cusitemcd})>"
