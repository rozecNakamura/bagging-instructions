from pydantic import BaseModel, Field
from datetime import date
from typing import List, Optional


class SearchRequest(BaseModel):
    """検索リクエスト"""

    prddt: str = Field(..., description="製造日(YYYYMMDD)")
    itemcd: Optional[str] = Field(None, description="品目コード")


class JobordItem(BaseModel):
    """受注明細項目"""

    prkey: int = Field(..., description="プライマリキー")
    prddt: str | None = Field(None, description="製造日")
    delvedt: str | None = Field(None, description="納品日")
    shptm: str | None = Field(None, description="出荷時刻")
    cuscd: str | None = Field(None, description="得意先コード")
    shpctrcd: str | None = Field(None, description="納入場所コード")
    itemcd: str | None = Field(None, description="品目コード")
    jobordmernm: str | None = Field(None, description="受注商品名称")
    jobordqun: float | None = Field(None, description="受注数量")

    class Config:
        from_attributes = True


class SearchResponse(BaseModel):
    """検索レスポンス"""

    total: int
    items: List[JobordItem]


# ============================================================
# 詳細情報用スキーマ（全リレーションデータ）
# ============================================================


class UniDetail(BaseModel):
    """単位マスタ詳細"""

    prkey: int
    unicd: Optional[str] = None  # 単位コード
    uninm: Optional[str] = None  # 単位名称
    uniinfnm: Optional[str] = None  # 単位情報名称
    dispno: Optional[float] = None  # 表示順

    class Config:
        from_attributes = True


class WareDetail(BaseModel):
    """倉庫マスタ詳細"""

    prkey: int
    fctcd: Optional[str] = None  # 工場コード
    whcd: Optional[str] = None  # 倉庫コード
    whnm: Optional[str] = None  # 倉庫名称
    whinfnm: Optional[str] = None  # 倉庫情報名称
    # 住所・連絡先
    zip: Optional[str] = None
    add1: Optional[str] = None
    add2: Optional[str] = None
    email: Optional[str] = None
    tel: Optional[str] = None
    fax: Optional[str] = None
    # その他主要情報
    whpicnm: Optional[str] = None  # 担当者名
    deptcd: Optional[str] = None  # 部門コード
    linecd: Optional[str] = None  # ラインコード

    class Config:
        from_attributes = True


class WorkcDetail(BaseModel):
    """工程マスタ詳細"""

    prkey: int
    fctcd: Optional[str] = None  # 工場コード
    wccd: Optional[str] = None  # 工程コード
    wcnm: Optional[str] = None  # 工程名称
    wcinfnm: Optional[str] = None  # 工程情報名称
    # 能力関連
    stdcap: Optional[float] = None  # 標準能力
    manrate: Optional[float] = None  # 人員レート
    capacity: Optional[float] = None  # 能力
    caprate: Optional[float] = None  # 能力レート
    # 時刻設定
    statm: Optional[str] = None  # 開始時刻
    endtm: Optional[str] = None  # 終了時刻
    # その他
    deptcd: Optional[str] = None  # 部門コード
    wcwhcd: Optional[str] = None  # 倉庫コード

    class Config:
        from_attributes = True


class RoutDetail(BaseModel):
    """工程手順マスタ詳細"""

    prkey: int
    fctcd: Optional[str] = None
    deptcd: Optional[str] = None
    itemgr: Optional[str] = None
    itemcd: Optional[str] = None
    linecd: Optional[str] = None
    routno: int
    # 倉庫・場所
    whcd: Optional[str] = None  # 倉庫コード
    loccd: Optional[str] = None  # 場所コード
    # 工程情報
    prccd: Optional[str] = None  # 工程コード
    prclt: Optional[float] = None  # 工程リードタイム
    prccap: Optional[float] = None  # 工程能力
    # 時間情報
    arngtm: Optional[float] = None  # 段取時間
    proctm: Optional[float] = None  # 加工時間
    prctm: Optional[float] = None  # 処理時間
    # 単価
    unitprice: Optional[float] = None
    unitprice1: Optional[float] = None
    unitprice2: Optional[float] = None
    unitprice3: Optional[float] = None
    # その他
    actcd: Optional[str] = None
    manjor: Optional[str] = None
    routstdcos: Optional[float] = None
    berthcd: Optional[str] = None
    prcpes: Optional[float] = None  # 工程人員
    defpresetupmemo: Optional[str] = None  # 段取メモ

    # リレーション: 倉庫マスタ（保管場所）
    ware: Optional[WareDetail] = None
    # リレーション: 工程マスタ（前工程）
    workc: Optional[WorkcDetail] = None

    class Config:
        from_attributes = True


class ItemDetail(BaseModel):
    """品目マスタ詳細"""

    prkey: int
    fctcd: Optional[str] = None
    deptcd: Optional[str] = None
    itemgr: Optional[str] = None  # 調理区分（品目種別）
    itemcd: str
    itemnm: str
    std: Optional[str] = None  # 規格
    uni0: Optional[str] = None  # 単位0(基本単位)
    nwei: Optional[float] = None  # 正味重量
    jouni: Optional[str] = None  # 処理1で使用する単位
    mernm: Optional[str] = None
    ordnm: Optional[str] = None
    searnm: Optional[str] = None
    # 分類コード
    clascd1: Optional[str] = None
    clascd2: Optional[str] = None
    clascd3: Optional[str] = None
    # 単位
    uni1: Optional[str] = None
    uni2: Optional[str] = None
    uni3: Optional[str] = None
    unicon1: Optional[float] = None
    unicon2: Optional[float] = None
    unicon3: Optional[float] = None
    # 在庫・発注単位
    stocuni: Optional[str] = None
    orduni: Optional[str] = None
    issuni: Optional[str] = None
    planuni: Optional[str] = None
    # 在庫管理
    safstoc: Optional[float] = None  # 安全在庫
    maxstoc: Optional[float] = None  # 最大在庫
    lt: Optional[float] = None  # リードタイム
    # その他
    bbdtaltdays: Optional[float] = None  # 賞味期限日数
    memo: Optional[str] = None
    oldcd: Optional[str] = None  # 旧コード
    # 殺菌関連
    steritemprange: Optional[str] = None  # 殺菌温度範囲
    steritime: Optional[float] = None  # 殺菌時間

    # リレーション: 単位マスタ
    uni: Optional[UniDetail] = None
    # リレーション: 工程手順マスタ（複数）
    routs: List[RoutDetail] = []

    class Config:
        from_attributes = True


class ShpctrDetail(BaseModel):
    """納入場所マスタ詳細"""

    prkey: int
    fctcd: Optional[str] = None
    cuscd: Optional[str] = None  # 得意先コード
    shpctrcd: str
    shpctrnm: str  # 正式名称
    shpctrabb: Optional[str] = None  # 略称
    # 住所情報
    zip: Optional[str] = None  # 郵便番号
    add1: Optional[str] = None
    add2: Optional[str] = None
    # 連絡先
    email: Optional[str] = None
    tel: Optional[str] = None
    fax: Optional[str] = None
    # 取引期間
    stadeal: Optional[str] = None
    enddeal: Optional[str] = None
    # 配送関連
    distctrcd: Optional[str] = None  # 配送センターコード
    distno: Optional[float] = None  # 配送番号
    trcd: Optional[str] = None  # 運送業者コード
    # その他
    shpctrcolor: Optional[str] = None  # 色コード
    shpctrcannm: Optional[str] = None  # カナ名称
    shpctrprntnm: Optional[str] = None  # 印刷名
    shpctrnm2: Optional[str] = None  # 名称2
    linecd: Optional[str] = None  # ラインコード
    dispno: Optional[float] = None  # 表示順

    class Config:
        from_attributes = True


class MbomDetail(BaseModel):
    """レシピマスタ詳細"""

    prkey: int
    # 親品目情報
    pfctcd: Optional[str] = None
    pdeptcd: Optional[str] = None
    pitemgr: Optional[str] = None
    pitemcd: Optional[str] = None
    proutno: Optional[float] = None
    # 子品目情報
    cfctcd: Optional[str] = None
    cdeptcd: Optional[str] = None
    citemgr: Optional[str] = None
    citemcd: str  # 子品目コード
    # 数量・タイプ
    amu: Optional[float] = None  # 規格数量（子の投入量）
    otp: Optional[float] = None  # 総数量（親の出来高）
    partyp: Optional[str] = None
    par: Optional[float] = None
    prvtyp: Optional[str] = None
    # その他
    issjor: Optional[str] = None
    memo: Optional[str] = None
    mbompic: Optional[str] = None
    # 期間
    stadt: Optional[str] = None  # 開始日
    enddt: Optional[str] = None  # 終了日

    # リレーション: 子品目マスタ
    child_item: Optional[ItemDetail] = None

    class Config:
        from_attributes = True


class CusmcdDetail(BaseModel):
    """得意先品目変換マスタ詳細"""

    prkey: int
    # キー情報
    merfctcd: Optional[str] = None
    cuscd: Optional[str] = None  # 得意先コード
    cusitemcd: str  # 得意先品目コード
    cusitemnm: str  # 得意先品目名称
    # 自社品目情報
    fctcd: Optional[str] = None
    deptcd: Optional[str] = None
    itemgr: Optional[str] = None
    itemcd: Optional[str] = None
    # 価格情報
    salprc0: Optional[float] = None
    salprc1: Optional[float] = None
    salprc2: Optional[float] = None
    salprc3: Optional[float] = None
    # 出荷ブロック
    shpblock: Optional[str] = None
    shpblockqun: Optional[float] = None
    # 販売メモ
    salmemo1: Optional[str] = None
    salmemo2: Optional[str] = None
    salmemo3: Optional[str] = None
    salmemo4: Optional[str] = None
    # 店舗価格
    shopprcexc0: Optional[float] = None
    shopprcinc0: Optional[float] = None
    # その他
    orgplacecd: Optional[str] = None  # 原産地コード
    disusecusitem: Optional[str] = None  # 廃止フラグ

    class Config:
        from_attributes = True


class JobordDetailItem(BaseModel):
    """受注明細詳細項目（全リレーションデータ含む）"""

    # 基本情報
    prkey: int
    jobordno: str
    jobordsno: float
    prddt: Optional[str] = None
    delvedt: Optional[str] = None
    shptm: Optional[str] = None
    itemcd: Optional[str] = None
    cuscd: Optional[str] = None
    shpctrcd: Optional[str] = None
    cusitemcd: Optional[str] = None
    jobordqun: Optional[float] = None
    linecd: Optional[str] = None
    jobordmernm: Optional[str] = None  # 受注商品名称

    # リレーションデータ
    item: Optional[ItemDetail] = None
    shpctr: Optional[ShpctrDetail] = None
    routs: List[RoutDetail] = []
    mboms: List[MbomDetail] = []
    cusmcd: Optional[CusmcdDetail] = None

    class Config:
        from_attributes = True


class SearchDetailResponse(BaseModel):
    """検索詳細レスポンス（全リレーションデータ含む）"""

    total: int
    items: List[JobordDetailItem]
