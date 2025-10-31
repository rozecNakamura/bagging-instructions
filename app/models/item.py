from sqlalchemy import Column, String, BigInteger, Numeric, DateTime
from sqlalchemy import UniqueConstraint, Index
from app.core.database import Base


class Item(Base):
    """品目マスタ（ITEM）- 全391カラム定義"""

    __tablename__ = "item"

    # プライマリキー
    prkey = Column(BigInteger, primary_key=True, comment="プライマリキー")

    # 複合ユニークキー構成要素
    fctcd = Column(String(50), nullable=False, default="", comment="工場コード")
    deptcd = Column(String(50), nullable=False, default="", comment="部門コード")
    itemgr = Column(String(1), nullable=False, default="", comment="品目グループ")
    itemcd = Column(String(50), nullable=False, default="", comment="品目コード")

    # 基本情報
    itemnm = Column(String(150), default="", comment="品目名称")
    mernm = Column(String(150), default="", comment="")
    ordnm = Column(String(150), default="", comment="")
    searnm = Column(String(150), default="", comment="")

    # 分類コード
    clascd1 = Column(String(50), default="", comment="分類コード1")
    clascd2 = Column(String(50), default="", comment="分類コード2")
    clascd3 = Column(String(50), default="", comment="分類コード3")
    clascd4 = Column(String(50), default="", comment="分類コード4")
    clascd5 = Column(String(50), default="", comment="分類コード5")
    clascd6 = Column(String(50), default="", comment="分類コード6")
    clascd7 = Column(String(50), default="", comment="分類コード7")
    clascd8 = Column(String(50), default="", comment="分類コード8")
    clascd9 = Column(String(50), default="", comment="分類コード9")
    clascd10 = Column(String(50), default="", comment="分類コード10")
    clascd11 = Column(String(50), default="", comment="分類コード11")
    clascd12 = Column(String(50), default="", comment="分類コード12")
    clascd13 = Column(String(50), default="", comment="分類コード13")
    clascd14 = Column(String(50), default="", comment="分類コード14")
    clascd15 = Column(String(50), default="", comment="分類コード15")
    clascd16 = Column(String(50), default="", comment="分類コード16")
    clascd17 = Column(String(50), default="", comment="分類コード17")
    clascd18 = Column(String(50), default="", comment="分類コード18")
    clascd19 = Column(String(50), default="", comment="分類コード19")
    clascd20 = Column(String(50), default="", comment="分類コード20")

    # グループコード
    nargrpcd = Column(String(50), default="", comment="")
    widgrpcd = Column(String(50), default="", comment="")
    midgrpcd = Column(String(50), default="", comment="")

    # フラグ類
    nctyp = Column(String(1), default="", comment="")
    llci = Column(String(3), default="", comment="")
    masflg = Column(String(1), default="", comment="")
    calcflg = Column(String(1), default="", comment="")
    pilflg = Column(String(1), default="", comment="")
    senflg = Column(String(1), default="", comment="")

    # 規格・重量
    std = Column(String(150), default="", comment="規格")
    nwei = Column(Numeric, default=0, comment="正味重量")
    spowei = Column(Numeric, default=0, comment="")
    phant = Column(String(1), default="", comment="")
    stdunip = Column(Numeric, default=0, comment="")

    # コード・番号
    oldcd = Column(String(50), default="", comment="旧コード")
    drawno = Column(String(150), default="", comment="")
    drawpat = Column(String(150), default="", comment="")

    # タイプ
    jobordtyp = Column(String(1), default="", comment="")
    plantyp = Column(String(1), default="", comment="")
    dispord = Column(String(1), default="", comment="")
    exptyp = Column(String(1), default="", comment="")
    itemordtyp = Column(String(1), default="", comment="")

    # 単位
    uni0 = Column(String(50), default="", comment="単位0")
    uni1 = Column(String(50), default="", comment="単位1")
    uni2 = Column(String(50), default="", comment="単位2")
    uni3 = Column(String(50), default="", comment="単位3")
    unicon1 = Column(Numeric, default=1, comment="単位換算1")
    unicon2 = Column(Numeric, default=1, comment="単位換算2")
    unicon3 = Column(Numeric, default=1, comment="単位換算3")

    # 価格
    salprc0 = Column(Numeric, default=0, comment="販売価格0")
    salprc1 = Column(Numeric, default=0, comment="販売価格1")
    salprc2 = Column(Numeric, default=0, comment="販売価格2")
    salprc3 = Column(Numeric, default=0, comment="販売価格3")

    # 単位情報
    stocuni = Column(String(50), default="", comment="在庫単位")
    orduni = Column(String(50), default="", comment="発注単位")
    issuni = Column(String(50), default="", comment="")
    planuni = Column(String(50), default="", comment="")
    minunit = Column(Numeric, default=0, comment="")
    picord = Column(String(50), default="", comment="")

    # MRP関連
    mrptyp = Column(String(1), default="", comment="")
    lotsiz = Column(Numeric, default=0, comment="")
    safstoc = Column(Numeric, default=0, comment="安全在庫")
    safstocitv = Column(Numeric, default=0, comment="")
    maxstoc = Column(Numeric, default=0, comment="最大在庫")
    maxrecv = Column(Numeric, default=0, comment="")
    contlotsiz = Column(Numeric, default=0, comment="")

    # リードタイム
    term = Column(Numeric, default=0, comment="")
    lt = Column(Numeric, default=0, comment="リードタイム")
    saflt = Column(Numeric, default=0, comment="")
    mrpavlt = Column(Numeric, default=0, comment="")
    dclt = Column(Numeric, default=0, comment="")
    ordlt = Column(Numeric, default=0, comment="")
    jobordlt = Column(Numeric, default=0, comment="")

    # チェック・ブロック
    wkdaychk = Column(String(1), default="", comment="")
    ordblock = Column(String(1), default="", comment="")
    contblockbuf = Column(Numeric, default=0, comment="")
    ordblockbuf = Column(Numeric, default=0, comment="")
    autoordtyp = Column(String(1), default="", comment="")
    car = Column(Numeric, default=0, comment="")

    # 管理タイプ
    isstyp = Column(String(1), default="", comment="")
    stocmngtyp = Column(String(1), default="", comment="")
    entlottyp = Column(String(1), default="", comment="")
    unipseltyp = Column(String(1), default="", comment="")

    # 有効期限
    inval = Column(String(1), default="", comment="")
    invalup = Column(Numeric, default=0, comment="")
    invallot = Column(Numeric, default=0, comment="")

    # その他情報
    actcd = Column(String(50), default="", comment="")
    img = Column(String(50), default="", comment="")
    specitemflg = Column(String(50), default="", comment="")
    bbdtaltdays = Column(Numeric, default=0, comment="賞味期限日数")
    disusestocaltdays = Column(Numeric, default=0, comment="")
    bbdtadddays = Column(Numeric, default=0, comment="")
    memo = Column(String(300), default="", comment="メモ")

    # 出荷関連
    shpblockqun = Column(Numeric, default=0, comment="")
    shpblock = Column(String(1), default="", comment="")

    # オプション
    option1 = Column(String(50), default="", comment="オプション1")
    option2 = Column(String(50), default="", comment="オプション2")
    option3 = Column(String(50), default="", comment="オプション3")
    option4 = Column(String(50), default="", comment="オプション4")

    # デフォルト
    deforgplacecd = Column(String(50), default="", comment="")
    defgradecd = Column(String(50), default="", comment="")
    deforganic = Column(String(1), default="", comment="")
    cwm = Column(String(1), default="", comment="")
    dispno = Column(Numeric, default=0, comment="")
    itemprnttyp = Column(String(1), default="", comment="")
    disuseitem = Column(String(1), default="", comment="")
    enddt = Column(String(8), default="", comment="")

    # システム情報
    deldt = Column(DateTime, comment="削除日時")
    ludate = Column(DateTime, comment="最終更新日時")
    uuser = Column(String(20), default="", comment="更新ユーザー")
    udate = Column(DateTime, comment="更新日時")

    # 追加情報
    iteminfnm = Column(String(150), default="", comment="")
    jancd = Column(String(20), default="", comment="JANコード")

    # 販売メモ
    salmemo1 = Column(String(300), default="", comment="販売メモ1")
    salmemo2 = Column(String(300), default="", comment="販売メモ2")
    salmemo3 = Column(String(300), default="", comment="販売メモ3")
    salmemo4 = Column(String(300), default="", comment="販売メモ4")

    # 店舗価格
    shopprcexc0 = Column(Numeric, default=0, comment="")
    shopprcinc0 = Column(Numeric, default=0, comment="")
    shopprcexc1 = Column(Numeric, default=0, comment="")
    shopprcexc2 = Column(Numeric, default=0, comment="")
    shopprcexc3 = Column(Numeric, default=0, comment="")
    shopprcinc1 = Column(Numeric, default=0, comment="")
    shopprcinc2 = Column(Numeric, default=0, comment="")
    shopprcinc3 = Column(Numeric, default=0, comment="")

    # 保持関連
    kepacc = Column(String(1), default="", comment="")
    phtretlim = Column(Numeric, default=0, comment="")
    phtretdenc = Column(Numeric, default=0, comment="")
    phtretproc = Column(Numeric, default=0, comment="")
    prdabd = Column(Numeric, default=0, comment="")
    expprdrate = Column(Numeric, default=0, comment="")
    cusprdgrpdiv = Column(String(1), default="", comment="")
    prdorddivsiz = Column(Numeric, default=0, comment="")
    ordproptyp = Column(String(1), default="", comment="")
    minrecv = Column(Numeric, default=0, comment="")

    # スコア
    elecscore = Column(Numeric, default=0, comment="電気スコア")
    gasscore = Column(Numeric, default=0, comment="ガススコア")
    waterscore = Column(Numeric, default=0, comment="水スコア")
    sparescore01 = Column(Numeric, default=0, comment="")
    sparescore02 = Column(Numeric, default=0, comment="")
    sparescore03 = Column(Numeric, default=0, comment="")
    sparescore04 = Column(Numeric, default=0, comment="")
    sparescore05 = Column(Numeric, default=0, comment="")
    sparescore06 = Column(Numeric, default=0, comment="")
    sparescore07 = Column(Numeric, default=0, comment="")
    sparescore08 = Column(Numeric, default=0, comment="")
    sparescore09 = Column(Numeric, default=0, comment="")
    sparescore10 = Column(Numeric, default=0, comment="")

    # 単位原価
    elecunip = Column(Numeric, default=0, comment="")
    gasunip = Column(Numeric, default=0, comment="")
    waterunip = Column(Numeric, default=0, comment="")
    spareunip01 = Column(Numeric, default=0, comment="")
    spareunip02 = Column(Numeric, default=0, comment="")
    spareunip03 = Column(Numeric, default=0, comment="")
    spareunip04 = Column(Numeric, default=0, comment="")
    spareunip05 = Column(Numeric, default=0, comment="")
    spareunip06 = Column(Numeric, default=0, comment="")
    spareunip07 = Column(Numeric, default=0, comment="")
    spareunip08 = Column(Numeric, default=0, comment="")
    spareunip09 = Column(Numeric, default=0, comment="")
    spareunip10 = Column(Numeric, default=0, comment="")

    # メーカー情報
    makernm = Column(String(150), default="", comment="メーカー名")
    makercd = Column(String(50), default="", comment="メーカーコード")
    strtemp = Column(String(300), default="", comment="")
    bbdttmmsg = Column(String(150), default="", comment="")
    trkcd = Column(String(50), default="", comment="")

    # 自動ロット
    autolottyp = Column(String(2), default="", comment="")
    childreqputtyp = Column(String(1), default="", comment="")
    unipalttyp = Column(String(1), default="", comment="")
    yielddisptyp = Column(String(1), default="", comment="")
    shpdisptyp = Column(String(1), default="", comment="")

    # MBOM/JO単位
    mbomuni = Column(String(50), default="", comment="")
    jouni = Column(String(50), default="", comment="")

    # 時間単価
    elecunipbytm = Column(Numeric, default=0, comment="")
    waterunipbytm = Column(Numeric, default=0, comment="")
    gasunipbytm = Column(Numeric, default=0, comment="")
    spareunipbytm01 = Column(Numeric, default=0, comment="")
    spareunipbytm02 = Column(Numeric, default=0, comment="")
    spareunipbytm03 = Column(Numeric, default=0, comment="")
    spareunipbytm04 = Column(Numeric, default=0, comment="")
    spareunipbytm05 = Column(Numeric, default=0, comment="")
    spareunipbytm06 = Column(Numeric, default=0, comment="")
    spareunipbytm07 = Column(Numeric, default=0, comment="")
    spareunipbytm08 = Column(Numeric, default=0, comment="")
    spareunipbytm09 = Column(Numeric, default=0, comment="")
    spareunipbytm10 = Column(Numeric, default=0, comment="")

    # 検査パターン
    subinspptn = Column(String(50), default="", comment="")
    compinspptn = Column(String(50), default="", comment="")
    shpinspptn = Column(String(50), default="", comment="")

    # 重量・容量単位
    nweiuni = Column(String(50), default="", comment="")
    caruni = Column(String(50), default="", comment="")
    contqun = Column(Numeric, default=0, comment="")

    # 異名コード
    itemanocd1 = Column(String(50), default="", comment="")
    itemanocd2 = Column(String(50), default="", comment="")
    itemanocd3 = Column(String(50), default="", comment="")
    itemanocd4 = Column(String(50), default="", comment="")

    # 帳票関連
    expmemoptncd = Column(String(50), default="", comment="")
    packwei = Column(Numeric, default=0, comment="")

    # 権限・QOM
    itemauth = Column(String(150), default="", comment="")
    qom = Column(String(150), default="", comment="")
    incap = Column(String(150), default="", comment="")
    palletqun = Column(Numeric, default=0, comment="")

    # 追加情報 (01-20)
    addinfo01 = Column(String(300), default="", comment="")
    addinfo02 = Column(String(300), default="", comment="")
    addinfo03 = Column(String(300), default="", comment="")
    addinfo04 = Column(String(300), default="", comment="")
    addinfo05 = Column(String(300), default="", comment="")
    addinfo06 = Column(String(300), default="", comment="")
    addinfo07 = Column(String(300), default="", comment="")
    addinfo08 = Column(String(300), default="", comment="")
    addinfo09 = Column(String(300), default="", comment="")
    addinfo10 = Column(String(300), default="", comment="")
    addinfo11 = Column(String(300), default="", comment="")
    addinfo12 = Column(String(300), default="", comment="")
    addinfo13 = Column(String(300), default="", comment="")
    addinfo14 = Column(String(300), default="", comment="")
    addinfo15 = Column(String(300), default="", comment="")
    addinfo16 = Column(String(300), default="", comment="")
    addinfo17 = Column(String(300), default="", comment="")
    addinfo18 = Column(String(300), default="", comment="")
    addinfo19 = Column(String(300), default="", comment="")
    addinfo20 = Column(String(300), default="", comment="")

    # ラベル
    prdlabel01 = Column(String(1000), default="", comment="")
    casejancd = Column(String(20), default="", comment="")

    # 重量情報
    tarewei = Column(Numeric, default=0, comment="風袋重量")
    grosswei = Column(Numeric, default=0, comment="総重量")
    biluni = Column(String(50), default="", comment="")
    bbdtaddtyp = Column(String(1), default="0", comment="")
    prdcapperhour = Column(Numeric, default=0, comment="")
    regshptyp = Column(String(2), default="", comment="")

    # 単位情報 (0-3)
    uniinfo0 = Column(String(50), default="", comment="")
    uniinfo1 = Column(String(50), default="", comment="")
    uniinfo2 = Column(String(50), default="", comment="")
    uniinfo3 = Column(String(50), default="", comment="")

    # MRP・PIC
    mrpusetodaysord = Column(String(1), default="", comment="")
    picjobord = Column(String(50), default="", comment="")
    itemcmngrpcd = Column(String(50), default="", comment="")

    # 温度ゾーン
    tempzonecd = Column(String(50), default="", comment="")
    tempitemcd = Column(String(1), default="0", comment="")

    # 重量別価格
    salprcbywei = Column(Numeric, default=0, comment="")
    prcsprcbywei = Column(Numeric, default=0, comment="")

    # 税グループ
    saltaxgrpcd = Column(String(50), default="", comment="")
    prcstaxgrpcd = Column(String(50), default="", comment="")

    # リードタイム・数量
    isslt = Column(Numeric, default=0, comment="")
    weighqun = Column(Numeric, default=0, comment="")
    regdt = Column(String(8), default="", comment="")

    # 食品表示
    foodlblcd = Column(String(50), default="", comment="")
    reglimtyp = Column(String(2), default="", comment="")

    # 輸送重量
    transwei0 = Column(Numeric, default=0, comment="")
    transwei1 = Column(Numeric, default=0, comment="")
    transwei2 = Column(Numeric, default=0, comment="")
    transwei3 = Column(Numeric, default=0, comment="")

    # 出荷・LLC
    shpaltdays = Column(Numeric, default=0, comment="")
    llc = Column(Numeric, default=0, comment="")
    cmnorditemgrpno = Column(Numeric, default=0, comment="")
    boardpack = Column(Numeric, default=0, comment="")
    packuni = Column(String(50), default="", comment="")
    coeford = Column(Numeric, default=0, comment="")

    # アレルゲンコード
    alrgcd01 = Column(String(50), default="", comment="")
    alrgcd02 = Column(String(50), default="", comment="")
    alrgcd03 = Column(String(50), default="", comment="")
    alrgcd04 = Column(String(50), default="", comment="")
    alrgcd05 = Column(String(50), default="", comment="")
    alrgcd06 = Column(String(50), default="", comment="")
    alrgcd07 = Column(String(50), default="", comment="")
    alrgcd08 = Column(String(50), default="", comment="")
    alrgcd09 = Column(String(50), default="", comment="")
    alrgcd10 = Column(String(50), default="", comment="")

    # パレット・カート
    palsiz = Column(Numeric, default=0, comment="")
    cartsiz = Column(Numeric, default=0, comment="")

    # 出庫・在庫ターゲット
    isstgt = Column(String(1), default="1", comment="")
    thrownstoc = Column(String(1), default="0", comment="")

    # デフォルト在庫サブ情報
    defstocsubinfo01 = Column(String(50), default="", comment="")
    defstocsubinfo02 = Column(String(50), default="", comment="")
    defstocsubinfo03 = Column(String(50), default="", comment="")
    defstocsubinfo04 = Column(String(50), default="", comment="")
    defstocsubinfo05 = Column(String(50), default="", comment="")
    defstocsubinfo06 = Column(String(50), default="", comment="")
    defstocsubinfo07 = Column(String(50), default="", comment="")
    defstocsubinfo08 = Column(String(50), default="", comment="")
    defstocsubinfo09 = Column(String(50), default="", comment="")
    defstocsubinfo10 = Column(String(50), default="", comment="")

    # 発注計画アラート
    subordplanalerttyp = Column(String(2), default="0", comment="")
    subordplanbbdtalertbdr1 = Column(Numeric, default=0, comment="")
    subordplanbbdtalertbdr2 = Column(Numeric, default=0, comment="")
    subordplanbbdtalertbdr3 = Column(Numeric, default=0, comment="")
    subordplanstocalertbdr1 = Column(Numeric, default=0, comment="")

    # 請求グループ
    itembilgrp = Column(String(50), default="", comment="")
    ordcd = Column(String(50), default="", comment="")

    # 印刷タイプ
    stocprnttyp = Column(String(1), default="", comment="")
    manordprnttyp = Column(String(2), default="", comment="")

    # アラート結果
    prdplanstocalertrslt1 = Column(String(1), default="", comment="")
    prdplanstocalertrslt2 = Column(String(1), default="", comment="")
    prdplanstocalertrslt3 = Column(String(1), default="", comment="")
    prdplanbbdtalertrslt1 = Column(String(1), default="", comment="")
    prdplanbbdtalertrslt2 = Column(String(1), default="", comment="")
    prdplanbbdtalertrslt3 = Column(String(1), default="", comment="")
    subordplanstocalertrslt1 = Column(String(1), default="", comment="")
    subordplanstocalertrslt2 = Column(String(1), default="", comment="")
    subordplanstocalertrslt3 = Column(String(1), default="", comment="")
    subordplanbbdtalertrslt1 = Column(String(1), default="", comment="")
    subordplanbbdtalertrslt2 = Column(String(1), default="", comment="")
    subordplanbbdtalertrslt3 = Column(String(1), default="", comment="")

    # コンテナタイプ
    conttyp = Column(String(2), default="", comment="")

    # 単位使用フラグ
    useduni0 = Column(String(1), default="0", comment="")
    useduni1 = Column(String(1), default="0", comment="")
    useduni2 = Column(String(1), default="0", comment="")
    useduni3 = Column(String(1), default="0", comment="")

    # 汎用グループコード
    itemgengrpcd01 = Column(String(50), default="", comment="")
    itemgengrpcd02 = Column(String(50), default="", comment="")
    itemgengrpcd03 = Column(String(50), default="", comment="")
    itemgengrpcd04 = Column(String(50), default="", comment="")

    # 発注推奨
    ordrecommendation = Column(String(1), default="0", comment="")
    ordrecommendationalertdays = Column(Numeric, default=0, comment="")

    # 生産計画アラート
    prdplanalerttyp = Column(String(2), default="0", comment="")
    prdplanbbdtalertbdr1 = Column(Numeric, default=0, comment="")
    prdplanbbdtalertbdr2 = Column(Numeric, default=0, comment="")
    prdplanbbdtalertbdr3 = Column(Numeric, default=0, comment="")
    prdplanstocalertbdr1 = Column(Numeric, default=0, comment="")

    # ブレンドタイプ
    blendtyp = Column(String(1), default="0", comment="")

    # 品目カラー
    itemclr1 = Column(String(10), default="", comment="")
    itemclr2 = Column(String(10), default="", comment="")

    # 生産グループ
    itemprdgrpcd01 = Column(String(50), default="", comment="")
    sametreewc = Column(String(50), default="", comment="")
    manpowreq = Column(Numeric, default=0, comment="")
    dispyieldpar = Column(Numeric, default=0, comment="")
    proclstprnttyp = Column(String(1), default="1", comment="")

    # 作業計画チェック
    workplanchk01 = Column(String(10), default="", comment="")
    workplanchk02 = Column(String(10), default="", comment="")
    workplanchk03 = Column(String(10), default="", comment="")
    workplanchk04 = Column(String(10), default="", comment="")
    workplanchk05 = Column(String(10), default="", comment="")
    workplanchk06 = Column(String(10), default="", comment="")
    workplanchk07 = Column(String(10), default="", comment="")
    workplanchk08 = Column(String(10), default="", comment="")
    workplanchk09 = Column(String(10), default="", comment="")
    workplanchk10 = Column(String(10), default="", comment="")
    workplanchk11 = Column(String(10), default="", comment="")
    workplanchk12 = Column(String(10), default="", comment="")
    workplanchk13 = Column(String(10), default="", comment="")
    workplanchk14 = Column(String(10), default="", comment="")
    workplanchk15 = Column(String(10), default="", comment="")
    workplanchk16 = Column(String(10), default="", comment="")
    workplanchk17 = Column(String(10), default="", comment="")
    workplanchk18 = Column(String(10), default="", comment="")
    workplanchk19 = Column(String(10), default="", comment="")
    workplanchk20 = Column(String(10), default="", comment="")

    # テントタイプ
    tenttyp = Column(String(1), default="0", comment="")
    comexp01 = Column(Numeric, default=0, comment="")

    # 受注代替日付
    jobordaltstadt1 = Column(String(8), default="", comment="")
    jobordaltenddt1 = Column(String(8), default="", comment="")
    jobordaltstadt2 = Column(String(8), default="", comment="")
    jobordaltenddt2 = Column(String(8), default="", comment="")
    jobordaltstadt3 = Column(String(8), default="", comment="")
    jobordaltenddt3 = Column(String(8), default="", comment="")

    # 表示順
    saledispno = Column(Numeric, default=0, comment="")
    plandispno = Column(Numeric, default=0, comment="")
    suborddispno = Column(Numeric, default=0, comment="")
    manorddispno = Column(Numeric, default=0, comment="")
    stocdispno = Column(Numeric, default=0, comment="")
    quamagdispno = Column(Numeric, default=0, comment="")
    shipdispno = Column(Numeric, default=0, comment="")
    magdispno = Column(Numeric, default=0, comment="")

    # マージングループ
    margingrpcd = Column(String(50), default="", comment="")

    # 印刷タイプ
    bbdtprnttyp = Column(String(1), default="", comment="")
    splsrate = Column(Numeric, default=0, comment="")
    easytempcd = Column(String(50), default="", comment="")
    nxtdaybufrate = Column(Numeric, default=0, comment="")

    # 梱包数量
    packqun0 = Column(Numeric, default=0, comment="")
    packqun1 = Column(Numeric, default=0, comment="")
    packqun2 = Column(Numeric, default=0, comment="")
    packqun3 = Column(Numeric, default=0, comment="")

    # 廃棄在庫
    thrownstocmax = Column(Numeric, default=0, comment="")
    rsvthrownitemtyp = Column(String(1), default="0", comment="")
    bbdtaddmonths = Column(Numeric, default=0, comment="")
    proftyp = Column(String(1), default="", comment="")
    minthrownqun = Column(Numeric, default=0, comment="")
    compratio = Column(Numeric, default=0, comment="")
    thrownlt = Column(Numeric, default=0, comment="")

    # 計画締め追加日数 (NOT NULL)
    planclosadddays = Column(String(4), nullable=False, default="0", comment="")

    # 制約定義
    __table_args__ = (
        UniqueConstraint(
            "fctcd",
            "deptcd",
            "itemgr",
            "itemcd",
            name="item_fctcd_deptcd_itemgr_itemcd_key",
        ),
        Index("item_fctcd_dispno", "fctcd", "dispno"),
        Index(
            "item_fctcd_widgrpcd_midgrpcd_nargrpcd",
            "fctcd",
            "widgrpcd",
            "midgrpcd",
            "nargrpcd",
        ),
    )

    def __repr__(self):
        return f"<Item(prkey={self.prkey}, itemcd={self.itemcd}, itemnm={self.itemnm})>"
