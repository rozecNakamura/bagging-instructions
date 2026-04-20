using System.Xml.Linq;
using PdfSharp.Pdf;
using PdfSharp.Drawing;
using PdfSharp.Fonts;

namespace BaggingInstructions.Api.Services;

/// <summary>
/// 汁仕分表.rxz をテンプレートに PDF を生成する（CoReportsXML / RozecCrPrintClass のロジックを簡略化）。
/// </summary>
public class JuicePdfService
{
    /// <summary>座標変換: rxz の値 / twip = ポイント。参照: RozecCrPrintClass twip = 20000</summary>
    private const double Twip = 20000;

    /// <summary>ShrinkToFit 時の既定最小フォント（pt）。</summary>
    private const double DefaultShrinkToFitMinFontSizePts = 1.0;

    /// <summary>ShrinkToFit で 1 段階ずつ下げるフォント（pt）。</summary>
    private const double ShrinkToFitStepPts = 0.25;

    /// <summary>
    /// 単位列のみ、枠内判定をわずかに緩める（MeasureString と DrawString の差・丸めで収まるのに 1 段階大きいフォントが捨てられるのを防ぐ）。
    /// </summary>
    private const double QuantityUnitFitWidthSlackPts = 1.25;

    private const double QuantityUnitFitHeightSlackPts = 0.5;

    /// <summary>1ページあたりの最大表示行数。これを超える行は2ページ目以降に出力する。</summary>
    private const int RowsPerPage = 23;

    /// <summary>rxz の Data（TextField）／Text 1要素を表す。CoReportsXMLText / RozecCrPrintClass に合わせて Margin を保持。</summary>
    private class RxzTextItem
    {
        public string Name { get; set; } = "";
        public int StartX { get; set; }
        public int StartY { get; set; }
        public int SizeWidth { get; set; }
        public int SizeHeight { get; set; }
        public string TextData { get; set; } = "";
        public int Alignment { get; set; } // 1=左 2=右 3=中央
        public int FontHeight { get; set; } // 例: 1100 → 11pt
        public string FontName { get; set; } = "ＭＳ ゴシック";
        public int DrawSeq { get; set; }
        public bool Visible { get; set; } = true;
        /// <summary>枠線を描画するか（rxz の Frame）</summary>
        public bool Frame { get; set; }
        /// <summary>枠線の太さ（rxz の LineWidth、twip）</summary>
        public int LineWidth { get; set; } = 14173;
        /// <summary>枠線色（rxz の LineColor、AARRGGBB 16進）</summary>
        public string LineColor { get; set; } = "ff000000";
        /// <summary>テキスト／Data 内側余白（rxz の Text > Margin）。描画時はこの内側に文字を描く。</summary>
        public int MarginLeft { get; set; }
        public int MarginTop { get; set; }
        public int MarginRight { get; set; }
        public int MarginBottom { get; set; }
        /// <summary>縮小して表示（rxz の ShrinkToFit）。true のとき枠に収まるまでフォントを縮小する。</summary>
        public bool ShrinkToFit { get; set; }

        /// <summary>
        /// ShrinkToFit 時の最小フォント（pt）。null のときは <see cref="DefaultShrinkToFitMinFontSizePts"/>。
        /// </summary>
        public double? ShrinkToFitMinFontSizePts { get; set; }
        /// <summary>rxz の AlignVertical。1=上 2=中央 3=下。同一矩形に複数 Data を重ねるテンプレで必須。</summary>
        public int AlignVertical { get; set; } = 2;
        /// <summary>rxz の Box（枠）。true のとき Data/Text ではなく矩形のみ描画する。</summary>
        public bool IsBox { get; set; }
        public int FillPattern { get; set; }
        /// <summary>Box の塗り（FillPattern≠0 のとき）。AARRGGBB。</summary>
        public string BoxFillColor { get; set; } = "ff000000";

        /// <summary>rxz の Line（グリッド線）。<see cref="EndX"/>/<see cref="EndY"/> まで描画。</summary>
        public bool IsVectorLine { get; set; }

        public int EndX { get; set; }
        public int EndY { get; set; }
    }

    /// <summary>
    /// 出力日・出力時刻・ページ数をタグに追加する。PRINTDATE=yyyy/MM/dd, PRINTTIME=HH:mm, PAGECOUNT=Page: N/M
    /// </summary>
    public static void AddPrintTags(Dictionary<string, string> tagValues, DateTime printNow, int currentPage, int totalPages)
    {
        tagValues["PRINTDATE"] = printNow.ToString("yyyy/MM/dd");
        tagValues["PRINTTIME"] = printNow.ToString("HH:mm");
        tagValues["PAGECOUNT"] = $"Page: {currentPage}/{totalPages}";
    }

    /// <summary>
    /// 選択行からタグ値を構築（フロントの buildJuiceTagValues と同一ロジック）
    /// </summary>
    public static Dictionary<string, string> BuildTagValues(IReadOnlyList<JuicePrintRowDto> rows)
    {
        var tagValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (rows == null || rows.Count == 0) return tagValues;

        tagValues["Date"] = rows[0].Delvedt ?? "";
        tagValues["Time"] = rows[0].ShptmDisplay ?? "";

        decimal totalGram = 0;
        decimal totalPack = 0;
        var packs = new List<decimal?>();
        foreach (var r in rows)
        {
            totalGram += r.Jobordqun;
            var div = ParseDivisor(r.Addinfo02);
            if (div.HasValue && div.Value != 0)
            {
                var pack = r.Jobordqun / div.Value;
                totalPack += pack;
                packs.Add(pack);
            }
            else
                packs.Add(null);
        }

        int lastIdx = rows.Count - 1;
        for (int i = 0; i < RowsPerPage; i++)
        {
            var nn = i.ToString("D2");
            var r = i < rows.Count ? rows[i] : null;
            tagValues[$"ITEMNM{nn}"] = i == 0 ? (rows[0].Jobordmernm ?? "") : "";
            tagValues[$"LOCATIONNM{nn}"] = r?.Shpctrnm ?? "";
            tagValues[$"GRAM{nn}"] = r != null ? r.Jobordqun.ToString() : "";
            tagValues[$"GRAMSUM{nn}"] = (i == lastIdx && rows.Count > 0) ? totalGram.ToString() : "";
            tagValues[$"PACK{nn}"] = (r != null && i < packs.Count && packs[i].HasValue) ? packs[i]!.Value.ToString() : "";
            tagValues[$"PACKSUM{nn}"] = (i == lastIdx && rows.Count > 0) ? totalPack.ToString() : "";
        }
        return tagValues;
    }

    private static decimal? ParseDivisor(string? addinfo02)
    {
        if (string.IsNullOrWhiteSpace(addinfo02)) return null;
        if (decimal.TryParse(addinfo02.Trim(), out var v) && v != 0) return v;
        return null;
    }

    /// <summary>rxz の LineColor（AARRGGBB 16進）を XColor に変換</summary>
    private static XColor ParseLineColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex) || hex.Length < 8) return XColors.Black;
        hex = hex.TrimStart('#');
        try
        {
            var argb = Convert.ToUInt32(hex[..8], 16);
            int a = (int)((argb >> 24) & 0xFF);
            int r = (int)((argb >> 16) & 0xFF);
            int g = (int)((argb >> 8) & 0xFF);
            int b = (int)(argb & 0xFF);
            return XColor.FromArgb(a, r, g, b);
        }
        catch
        {
            return XColors.Black;
        }
    }

    /// <summary>
    /// rxz ファイルをパースし、Data（TextField）および Objects 直下の Text（ラベル）の一覧を取得。DrawSeq でソート済み。
    /// </summary>
    private static List<RxzTextItem> ParseRxzDataElements(string rxzPath)
    {
        var list = new List<RxzTextItem>();
        var doc = XDocument.Load(rxzPath);
        var root = doc.Root;
        if (root?.Name.LocalName != "RxzForm") return list;

        XElement? fontsEl = root?.Element("Fonts");
        var fontNames = new List<string>();
        if (fontsEl != null)
        {
            foreach (var f in fontsEl.Elements("Font"))
            {
                var name = (string?)f.Attribute("Name");
                fontNames.Add(string.IsNullOrEmpty(name) ? "ＭＳ ゴシック" : name);
            }
        }
        if (fontNames.Count == 0) fontNames.Add("ＭＳ ゴシック");

        var pagesEl = root!.Element("Pages");
        if (pagesEl == null) return list;
        var pages = pagesEl.Elements("Page");

        foreach (var page in pages)
        {
            if (page == null) continue;
            var layers = page.Element("Layers")?.Elements("Layer");
            if (layers == null) continue;
            foreach (var layer in layers)
            {
                var objectsEl = layer.Element("Objects");
                if (objectsEl == null) continue;

                // 1) Data > TextField > Text（データ項目: ITEMNM00, LOCATIONNM00 など）
                foreach (var data in objectsEl.Elements("Data"))
                {
                    var textField = data.Element("TextField");
                    var text = textField?.Element("Text");
                    var obj = text?.Element("Object");
                    if (obj == null || text == null) continue;
                    if (!TryParseTextItem(obj, text, fontNames, out var item)) continue;
                    list.Add(item);
                }

                // 2) Objects 直下の Text（ラベル: 汁仕分表, 喫食日, 品目名称, 施設 など）
                foreach (var text in objectsEl.Elements("Text"))
                {
                    var obj = text.Element("Object");
                    if (obj == null) continue;
                    if (!TryParseTextItem(obj, text, fontNames, out var item)) continue;
                    list.Add(item);
                }

                // 3) Box（枠線・パネル）。作業前準備書などでレイアウト枠として使用。
                foreach (var box in objectsEl.Elements("Box"))
                {
                    if (!TryParseBoxItem(box, out var boxItem)) continue;
                    list.Add(boxItem);
                }

                // 4) Line（直線・表グリッド）。検収の記録簿.rxz など。
                foreach (var lineEl in objectsEl.Elements("Line"))
                {
                    if (!TryParseVectorLineItem(lineEl, out var lineItem)) continue;
                    list.Add(lineItem);
                }
            }
        }

        list.Sort((a, b) => a.DrawSeq.CompareTo(b.DrawSeq));
        return list;
    }

    /// <summary>
    /// Object と Text 要素から RxzTextItem を組み立てる。
    /// </summary>
    private static bool TryParseTextItem(XElement obj, XElement text, List<string> fontNames, out RxzTextItem item)
    {
        item = null!;
        var name = (string?)obj.Element("Name");
        if (string.IsNullOrEmpty(name)) return false;

        var start = obj.Element("Start");
        int startX = (int?)start?.Attribute("X") ?? 0;
        int startY = (int?)start?.Attribute("Y") ?? 0;
        int drawSeq = (int?)obj.Element("DrawSeq") ?? 0;
        bool visible = (bool?)obj.Element("Visible") ?? true;

        var size = text.Element("Size");
        int sizeW = (int?)size?.Attribute("Width") ?? 0;
        int sizeH = (int?)size?.Attribute("Height") ?? 0;
        int alignment = (int?)text.Element("Alignment") ?? 1;
        var fontZenkaku = text.Element("FontZenkaku");
        var fontSize = fontZenkaku?.Element("FontSize");
        int fontHeight = (int?)fontSize?.Attribute("Height") ?? 1100;
        int fontNo = (int?)fontZenkaku?.Element("FontNo") ?? 0;
        string fontName = (fontNames.Count > 0 && fontNo < fontNames.Count) ? fontNames[fontNo] : "ＭＳ ゴシック";

        var textData = (string?)text.Element("TextData") ?? "";
        bool frame = (bool?)text.Element("Frame") ?? false;
        int lineWidth = (int?)text.Element("LineWidth") ?? 14173;
        string lineColor = (string?)text.Element("LineColor") ?? "ff000000";

        int marginLeft = 0, marginTop = 0, marginRight = 0, marginBottom = 0;
        var marginEl = text.Element("Margin");
        if (marginEl != null)
        {
            marginLeft = (int?)marginEl.Attribute("Left") ?? 0;
            marginTop = (int?)marginEl.Attribute("Top") ?? 0;
            marginRight = (int?)marginEl.Attribute("Right") ?? 0;
            marginBottom = (int?)marginEl.Attribute("Bottom") ?? 0;
        }
        bool shrinkToFit = (bool?)text.Element("ShrinkToFit") ?? false;
        int alignVertical = (int?)text.Element("AlignVertical") ?? 2;

        item = new RxzTextItem
        {
            Name = name,
            StartX = startX,
            StartY = startY,
            SizeWidth = sizeW,
            SizeHeight = sizeH,
            TextData = textData ?? "",
            Alignment = alignment,
            FontHeight = fontHeight,
            FontName = fontName,
            DrawSeq = drawSeq,
            Visible = visible,
            Frame = frame,
            LineWidth = lineWidth,
            LineColor = lineColor ?? "ff000000",
            MarginLeft = marginLeft,
            MarginTop = marginTop,
            MarginRight = marginRight,
            MarginBottom = marginBottom,
            ShrinkToFit = shrinkToFit,
            AlignVertical = alignVertical,
            ShrinkToFitMinFontSizePts = null
        };
        if (IsQuantityUnitFieldName(name))
            item.ShrinkToFit = true;
        // 単位列・予定数量列は幅が狭いがテンプレでは ShrinkToFit=false のことが多い。全文を枠内に収める。
        if (name.StartsWith("UNITPAR", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("UNITCHI", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("USEQUNSUM", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("FILLQUNSUM", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("SUBUSEQUNSUM", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("SUBFILLQUNSUM", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("SUBUSEQUN11", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("MAKEQUNPLAN", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("USEQUNPLAN", StringComparison.OrdinalIgnoreCase))
            item.ShrinkToFit = true;
        // 調理指示書等: 子品目名・製造予定・使用予定を枠内の中央（横・縦）に。
        if (name.StartsWith("ITEMCHINM", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("MAKEQUNPLAN", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("USEQUNPLAN", StringComparison.OrdinalIgnoreCase))
        {
            item.Alignment = 3;
            item.AlignVertical = 2;
        }
        // 親品目セル下段（注番）: 右寄せ＋下寄せで右下隅（rxz の AlignVertical=3 と整合）。
        else if (name.StartsWith("ITEMPALNM", StringComparison.OrdinalIgnoreCase))
        {
            item.Alignment = 2;
            item.AlignVertical = 3;
        }
        return true;
    }

    /// <summary>rxz の Line（Graphic/Object/Start と End）をパースする。</summary>
    private static bool TryParseVectorLineItem(XElement lineEl, out RxzTextItem item)
    {
        item = null!;
        var graphic = lineEl.Element("Graphic");
        var obj = graphic?.Element("Object");
        var startEl = obj?.Element("Start");
        var endEl = lineEl.Element("End");
        if (startEl == null || endEl == null || obj == null) return false;

        int sx = (int?)startEl.Attribute("X") ?? 0;
        int sy = (int?)startEl.Attribute("Y") ?? 0;
        int ex = (int?)endEl.Attribute("X") ?? 0;
        int ey = (int?)endEl.Attribute("Y") ?? 0;
        int lineWidth = (int?)graphic?.Element("LineWidth") ?? 14173;
        string lineColor = (string?)graphic?.Element("LineColor") ?? "ff000000";
        int drawSeq = (int?)obj.Element("DrawSeq") ?? 0;
        bool visible = (bool?)obj.Element("Visible") ?? true;
        var name = (string?)obj.Element("Name") ?? $"Line_{drawSeq}";

        item = new RxzTextItem
        {
            Name = name,
            StartX = sx,
            StartY = sy,
            EndX = ex,
            EndY = ey,
            SizeWidth = 0,
            SizeHeight = 0,
            TextData = "",
            Alignment = 1,
            FontHeight = 1100,
            FontName = "ＭＳ ゴシック",
            DrawSeq = drawSeq,
            Visible = visible,
            Frame = false,
            LineWidth = lineWidth,
            LineColor = lineColor ?? "ff000000",
            MarginLeft = 0,
            MarginTop = 0,
            MarginRight = 0,
            MarginBottom = 0,
            ShrinkToFit = false,
            AlignVertical = 2,
            IsVectorLine = true
        };
        return true;
    }

    private static bool IsQuantityUnitFieldName(string name) =>
        name.StartsWith("USEQUNUNIT", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("SUBUSEQUNUNIT", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("FILLQUNUNIT", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("SUBFILLQUNUNIT", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("FILLQUNSUMUNIT", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("SUBFILLQUNSUMUNIT", StringComparison.OrdinalIgnoreCase);

    /// <summary>rxz の Box（Graphic + Size）をパースする。</summary>
    private static bool TryParseBoxItem(XElement box, out RxzTextItem item)
    {
        item = null!;
        var graphic = box.Element("Graphic");
        var obj = graphic?.Element("Object");
        var size = box.Element("Size");
        if (obj == null || size == null) return false;

        var name = (string?)obj.Element("Name");
        if (string.IsNullOrEmpty(name)) return false;

        var start = obj.Element("Start");
        int startX = (int?)start?.Attribute("X") ?? 0;
        int startY = (int?)start?.Attribute("Y") ?? 0;
        int drawSeq = (int?)obj.Element("DrawSeq") ?? 0;
        bool visible = (bool?)obj.Element("Visible") ?? true;

        int sizeW = (int?)size.Attribute("Width") ?? 0;
        int sizeH = (int?)size.Attribute("Height") ?? 0;

        int lineWidth = (int?)graphic?.Element("LineWidth") ?? 14173;
        string lineColor = (string?)graphic?.Element("LineColor") ?? "ff000000";
        int fillPattern = (int?)graphic?.Element("FillPattern") ?? 0;
        string fillColorHex = (string?)graphic?.Element("FillColor") ?? "ff000000";
        bool frame = (bool?)box.Element("Frame") ?? true;

        item = new RxzTextItem
        {
            Name = name,
            StartX = startX,
            StartY = startY,
            SizeWidth = sizeW,
            SizeHeight = sizeH,
            TextData = "",
            Alignment = 1,
            FontHeight = 1100,
            FontName = "ＭＳ ゴシック",
            DrawSeq = drawSeq,
            Visible = visible,
            Frame = frame,
            LineWidth = lineWidth,
            LineColor = lineColor ?? "ff000000",
            MarginLeft = 0,
            MarginTop = 0,
            MarginRight = 0,
            MarginBottom = 0,
            ShrinkToFit = false,
            IsBox = true,
            FillPattern = fillPattern,
            BoxFillColor = fillColorHex ?? "ff000000"
        };
        return true;
    }

    /// <summary>
    /// タグ値をパース済みアイテム一覧に反映する。
    /// </summary>
    private static void ApplyTagValuesToItems(List<RxzTextItem> items, Dictionary<string, string> tagValues)
    {
        foreach (var item in items)
        {
            if (tagValues.TryGetValue(item.Name, out var value))
            {
                item.TextData = value ?? "";
            }
        }
    }

    /// <summary>
    /// 1ページを追加し、現在の items の TextData を描画する。rxz の Page Margin をオフセットとして反映する。
    /// </summary>
    private static void AddPageAndDraw(PdfDocument doc, List<RxzTextItem> items, int pageWidth, int pageHeight, int marginLeft = 0, int marginTop = 0)
    {
        var page = doc.AddPage();
        page.Width = XUnit.FromPoint(pageWidth / Twip);
        page.Height = XUnit.FromPoint(pageHeight / Twip);

        double offsetX = marginLeft / Twip;
        double offsetY = marginTop / Twip;

        using var gfx = XGraphics.FromPdfPage(page);
        foreach (var item in items)
        {
            if (!item.Visible) continue;

            if (item.IsVectorLine)
            {
                double x1 = item.StartX / Twip + offsetX;
                double y1 = item.StartY / Twip + offsetY;
                double x2 = item.EndX / Twip + offsetX;
                double y2 = item.EndY / Twip + offsetY;
                var lineWidthPt = item.LineWidth / Twip;
                if (lineWidthPt <= 0) lineWidthPt = 0.35;
                var penColor = ParseLineColor(item.LineColor);
                var pen = new XPen(penColor, lineWidthPt);
                gfx.DrawLine(pen, x1, y1, x2, y2);
                continue;
            }

            double x = item.StartX / Twip + offsetX;
            double y = item.StartY / Twip + offsetY;
            double w = item.SizeWidth / Twip;
            double h = item.SizeHeight / Twip;
            var rect = new XRect(x, y, w, h);

            if (item.IsBox)
            {
                if (item.FillPattern != 0)
                {
                    var fill = ParseLineColor(item.BoxFillColor);
                    gfx.DrawRectangle(new XSolidBrush(fill), rect);
                }
                if (item.Frame)
                {
                    var lineWidthPt = item.LineWidth / Twip;
                    if (lineWidthPt <= 0) lineWidthPt = 0.5;
                    var penColor = ParseLineColor(item.LineColor);
                    var pen = new XPen(penColor, lineWidthPt);
                    gfx.DrawRectangle(pen, rect);
                }
                continue;
            }

            if (item.Frame)
            {
                var lineWidthPt = item.LineWidth / Twip;
                if (lineWidthPt <= 0) lineWidthPt = 0.5;
                var penColor = ParseLineColor(item.LineColor);
                var pen = new XPen(penColor, lineWidthPt);
                gfx.DrawRectangle(pen, rect);
            }

            if (string.IsNullOrEmpty(item.TextData)) continue;

            // Text/Data の Margin を反映（RozecCrPrintClass と同様：外枠の内側にテキスト描画領域を取る）
            double textMarginLeft = item.MarginLeft / Twip;
            double textMarginTop = item.MarginTop / Twip;
            double textMarginRight = item.MarginRight / Twip;
            double textMarginBottom = item.MarginBottom / Twip;
            double textX = x + textMarginLeft;
            double textY = y + textMarginTop;
            double textW = w - textMarginLeft - textMarginRight;
            double textH = h - textMarginTop - textMarginBottom;
            if (textW <= 0 || textH <= 0) continue;
            var textRect = new XRect(textX, textY, textW, textH);

            var lines = item.TextData.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
            if (lines.Length == 0) continue;

            double fontSize = item.FontHeight / 100.0;
            if (fontSize < 1) fontSize = 11;
            double minFontSize = item.ShrinkToFitMinFontSizePts ?? DefaultShrinkToFitMinFontSizePts;

            // 縮小して表示: 枠に収まるまでフォントサイズを小さくする（RozecCrPrintClass と同様）
            if (item.ShrinkToFit)
            {
                double widthLimit = textRect.Width;
                double heightLimit = textRect.Height;
                if (IsQuantityUnitFieldName(item.Name))
                {
                    widthLimit += QuantityUnitFitWidthSlackPts;
                    heightLimit += QuantityUnitFitHeightSlackPts;
                }

                double trySize = fontSize;
                while (trySize >= minFontSize)
                {
                    var tryFont = new XFont(JuicePdfFontResolver.JapaneseFaceName, trySize, XFontStyleEx.Regular);
                    double maxLineWidth = 0;
                    double totalMeasuredHeight = 0;
                    foreach (var line in lines)
                    {
                        if (string.IsNullOrEmpty(line))
                        {
                            totalMeasuredHeight += tryFont.GetHeight();
                            continue;
                        }
                        var sz = gfx.MeasureString(line, tryFont);
                        if (sz.Width > maxLineWidth) maxLineWidth = sz.Width;
                        totalMeasuredHeight += tryFont.GetHeight();
                    }
                    if (maxLineWidth <= widthLimit && totalMeasuredHeight <= heightLimit)
                    {
                        fontSize = trySize;
                        break;
                    }
                    trySize -= ShrinkToFitStepPts;
                }
                if (trySize < minFontSize) fontSize = minFontSize;
            }

            var font = new XFont(JuicePdfFontResolver.JapaneseFaceName, fontSize, XFontStyleEx.Regular);

            var format = new XStringFormat();
            if (item.Alignment == 2) format.Alignment = XStringAlignment.Far;
            else if (item.Alignment == 3) format.Alignment = XStringAlignment.Center;
            else format.Alignment = XStringAlignment.Near;
            format.LineAlignment = XLineAlignment.Near;

            double lineHeight = font.GetHeight();
            double totalHeight = lineHeight * lines.Length;
            // rxz の AlignVertical を反映。従来は常に中央寄せのため、親数量+親品目など同一セルが常に重なっていた。
            double startY = item.AlignVertical switch
            {
                1 => textRect.Y,
                3 => Math.Max(textRect.Y, textRect.Y + textRect.Height - totalHeight),
                _ => textRect.Y + (textRect.Height - totalHeight) / 2.0
            };

            // 枠からはみ出した文字は表示しない（テキスト描画領域でクリップ）
            gfx.Save();
            gfx.IntersectClip(textRect);
            for (int li = 0; li < lines.Length; li++)
            {
                var lineRect = new XRect(textRect.X, startY + li * lineHeight, textRect.Width, lineHeight);
                gfx.DrawString(lines[li], font, XBrushes.Black, lineRect, format);
            }
            gfx.Restore();
        }
    }

    /// <summary>
    /// 用紙サイズとページマージンを rxz から取得する。Margin は Page 直下の Margin 要素（Left, Top, Right, Bottom）。
    /// Orientation が 2（横）のときは Size の幅・高さを入れ替えて PDF の向きと一致させる（オブジェクト座標は横レイアウト用）。
    /// </summary>
    private static (int Width, int Height, int MarginLeft, int MarginTop, int MarginRight, int MarginBottom) GetPageInfoFromRxz(string rxzTemplatePath)
    {
        int pageWidth = 11905511;
        int pageHeight = 16837795;
        int marginLeft = 0, marginTop = 0, marginRight = 0, marginBottom = 0;
        try
        {
            var doc2 = XDocument.Load(rxzTemplatePath);
            var pageEl = doc2.Root?.Element("Pages")?.Element("Page");
            if (pageEl != null)
            {
                var sizeEl = pageEl.Element("Size");
                if (sizeEl != null)
                {
                    pageWidth = (int?)sizeEl.Attribute("Width") ?? pageWidth;
                    pageHeight = (int?)sizeEl.Attribute("Height") ?? pageHeight;
                }
                var orientEl = pageEl.Element("Orientation");
                int orientation = (int?)orientEl ?? 1;
                // 2 = 横（長辺が左右）。テンプレの Size は縦置き寸法のままのため入れ替える。
                if (orientation == 2)
                {
                    (pageWidth, pageHeight) = (pageHeight, pageWidth);
                }
                var marginEl = pageEl.Element("Margin");
                if (marginEl != null)
                {
                    marginLeft = (int?)marginEl.Attribute("Left") ?? 0;
                    marginTop = (int?)marginEl.Attribute("Top") ?? 0;
                    marginRight = (int?)marginEl.Attribute("Right") ?? 0;
                    marginBottom = (int?)marginEl.Attribute("Bottom") ?? 0;
                }
            }
        }
        catch { /* use default */ }
        return (pageWidth, pageHeight, marginLeft, marginTop, marginRight, marginBottom);
    }

    /// <summary>
    /// 用紙サイズを rxz から取得する（後方互換）。
    /// </summary>
    private static (int Width, int Height) GetPageSizeFromRxz(string rxzTemplatePath)
    {
        var (w, h, _, _, _, _) = GetPageInfoFromRxz(rxzTemplatePath);
        return (w, h);
    }

    /// <summary>
    /// 行データを 23 行ごとに分割し、2 ページ目以降にまたいで PDF を生成する。
    /// </summary>
    public byte[] GeneratePdfFromRows(string rxzTemplatePath, IReadOnlyList<JuicePrintRowDto> rows)
    {
        if (rows == null || rows.Count == 0)
            return Array.Empty<byte>();

        var items = ParseRxzDataElements(rxzTemplatePath);
        var (pageWidth, pageHeight, marginLeft, marginTop, _, _) = GetPageInfoFromRxz(rxzTemplatePath);

        var doc = new PdfDocument();
        doc.Info.Title = "汁仕分表";

        var printNow = DateTime.Now;
        var totalPages = (rows.Count + RowsPerPage - 1) / RowsPerPage;
        if (totalPages < 1) totalPages = 1;

        for (int offset = 0, pageIndex = 0; offset < rows.Count; offset += RowsPerPage, pageIndex++)
        {
            var chunk = rows.Skip(offset).Take(RowsPerPage).ToList();
            var tagValues = BuildTagValues(chunk);
            AddPrintTags(tagValues, printNow, pageIndex + 1, totalPages);
            ApplyTagValuesToItems(items, tagValues);
            AddPageAndDraw(doc, items, pageWidth, pageHeight, marginLeft, marginTop);
        }

        using var ms = new MemoryStream();
        doc.Save(ms, false);
        return ms.ToArray();
    }

    /// <summary>
    /// 複数ページ分のタグ値を受け取り、1 ページずつ描画して PDF を生成する（弁当箱盛り付け指示書などで使用）。
    /// </summary>
    public byte[] GeneratePdfMultiPage(string rxzTemplatePath, IReadOnlyList<Dictionary<string, string>> pagesTagValues, string? documentTitle = null)
    {
        if (pagesTagValues == null || pagesTagValues.Count == 0)
            return Array.Empty<byte>();

        var items = ParseRxzDataElements(rxzTemplatePath);
        var (pageWidth, pageHeight, marginLeft, marginTop, _, _) = GetPageInfoFromRxz(rxzTemplatePath);

        var doc = new PdfDocument();
        doc.Info.Title = documentTitle ?? "弁当箱盛り付け指示書";

        foreach (var tagValues in pagesTagValues)
        {
            ApplyTagValuesToItems(items, tagValues);
            AddPageAndDraw(doc, items, pageWidth, pageHeight, marginLeft, marginTop);
        }

        using var ms = new MemoryStream();
        doc.Save(ms, false);
        return ms.ToArray();
    }

    /// <summary>
    /// rxz テンプレートとタグ値から 1 ページ分の PDF を生成してバイト配列で返す。
    /// </summary>
    public byte[] GeneratePdf(string rxzTemplatePath, Dictionary<string, string> tagValues)
    {
        var items = ParseRxzDataElements(rxzTemplatePath);
        ApplyTagValuesToItems(items, tagValues);

        var doc = new PdfDocument();
        doc.Info.Title = "汁仕分表";
        var (pageWidth, pageHeight, marginLeft, marginTop, _, _) = GetPageInfoFromRxz(rxzTemplatePath);
        AddPageAndDraw(doc, items, pageWidth, pageHeight, marginLeft, marginTop);

        using var ms = new MemoryStream();
        doc.Save(ms, false);
        return ms.ToArray();
    }
}

/// <summary>汁仕分表 PDF 印刷用の1行データ</summary>
public class JuicePrintRowDto
{
    public string? Delvedt { get; set; }
    public string? ShptmDisplay { get; set; }
    public string? Jobordmernm { get; set; }
    public string? Shpctrnm { get; set; }
    public decimal Jobordqun { get; set; }
    public string? Addinfo02 { get; set; }
}
