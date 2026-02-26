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

    /// <summary>rxz の Data（TextField）1要素を表す</summary>
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
        for (int i = 0; i < 40; i++)
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
            LineColor = lineColor ?? "ff000000"
        };
        return true;
    }

    /// <summary>
    /// rxz テンプレートとタグ値から PDF を生成してバイト配列で返す。
    /// </summary>
    public byte[] GeneratePdf(string rxzTemplatePath, Dictionary<string, string> tagValues)
    {
        var items = ParseRxzDataElements(rxzTemplatePath);
        foreach (var item in items)
        {
            if (tagValues.TryGetValue(item.Name, out var value))
            {
                item.TextData = value ?? "";
            }
        }

        var doc = new PdfDocument();
        doc.Info.Title = "汁仕分表";

        // 用紙サイズは rxz の 1 ページ目から取得（同一 twip 換算）
        int pageWidth = 11905511;
        int pageHeight = 16837795;
        try
        {
            var doc2 = XDocument.Load(rxzTemplatePath);
            var sizeEl = doc2.Root?.Element("Pages")?.Element("Page")?.Element("Size");
            if (sizeEl != null)
            {
                pageWidth = (int?)sizeEl.Attribute("Width") ?? pageWidth;
                pageHeight = (int?)sizeEl.Attribute("Height") ?? pageHeight;
            }
        }
        catch { /* use default */ }

        var page = doc.AddPage();
        page.Width = XUnit.FromPoint(pageWidth / Twip);
        page.Height = XUnit.FromPoint(pageHeight / Twip);

        using (var gfx = XGraphics.FromPdfPage(page))
        {
            foreach (var item in items)
            {
                if (!item.Visible) continue;

                double x = item.StartX / Twip;
                double y = item.StartY / Twip;
                double w = item.SizeWidth / Twip;
                double h = item.SizeHeight / Twip;
                var rect = new XRect(x, y, w, h);

                // 枠線（表の線）を描画（Frame が true のセル）
                if (item.Frame)
                {
                    var lineWidthPt = item.LineWidth / Twip;
                    if (lineWidthPt <= 0) lineWidthPt = 0.5;
                    var penColor = ParseLineColor(item.LineColor);
                    var pen = new XPen(penColor, lineWidthPt);
                    gfx.DrawRectangle(pen, rect);
                }

                if (string.IsNullOrEmpty(item.TextData)) continue;

                double fontSize = item.FontHeight / 100.0;
                if (fontSize < 1) fontSize = 11;
                // 日本語表示のためカスタム FontResolver のフォントを明示的に使用（JuicePdfFontResolver が .ttf/TTC を提供）
                var font = new XFont(JuicePdfFontResolver.JapaneseFaceName, fontSize, XFontStyleEx.Regular);

                var format = new XStringFormat();
                if (item.Alignment == 2) format.Alignment = XStringAlignment.Far;
                else if (item.Alignment == 3) format.Alignment = XStringAlignment.Center;
                else format.Alignment = XStringAlignment.Near;
                format.LineAlignment = XLineAlignment.Center;

                gfx.DrawString(item.TextData, font, XBrushes.Black, rect, format);
            }
        }

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
