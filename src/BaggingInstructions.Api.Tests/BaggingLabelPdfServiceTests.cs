using System.IO;
using BaggingInstructions.Api.DTOs;
using BaggingInstructions.Api.Services;
using PdfSharp.Fonts;
using PdfSharp.Pdf.IO;

namespace BaggingInstructions.Api.Tests;

public class BaggingLabelPdfServiceTests
{
    static BaggingLabelPdfServiceTests()
    {
        if (GlobalFontSettings.FontResolver == null)
            GlobalFontSettings.FontResolver = new JuicePdfFontResolver();
    }
    [Fact]
    public void GeneratePdf_page_size_matches_rxz_square_60mm()
    {
        var templatePath = ResolveBaggingLabelTemplatePath();
        var svc = new BaggingLabelPdfService(new JuicePdfService());
        var item = new LabelItemDto
        {
            LabelType = "standard",
            Count = 1,
            Delvedt = "20260513",
            ShptmName = "朝便",
            Itemcd = "ITEM001",
            Itemnm = "テスト品目",
            ExpiryDate = "20260630",
            Shpctrnm = "テスト施設",
            PageNo = 0,
            Kikunip = 10,
            UnitName = "個",
            Classification1Name = "大分類"
        };

        var bytes = svc.GeneratePdf(templatePath, new[] { item });
        Assert.NotEmpty(bytes);

        using var ms = new MemoryStream(bytes);
        using var doc = PdfReader.Open(ms, PdfDocumentOpenMode.Import);
        Assert.Equal(1, doc.PageCount);
        var page = doc.Pages[0];
        double wPt = page.Width.Point;
        double hPt = page.Height.Point;
        // rxz Size 3401580 twip / 20000 ≈ 170.079 pt ≈ 60 mm
        Assert.InRange(wPt, 169.9, 170.3);
        Assert.InRange(hPt, 169.9, 170.3);
    }

    [Fact]
    public void GeneratePdf_long_text_produces_valid_pdf()
    {
        var templatePath = ResolveBaggingLabelTemplatePath();
        var svc = new BaggingLabelPdfService(new JuicePdfService());
        var longName = new string('長', 40) + new string('A', 20);
        var item = new LabelItemDto
        {
            LabelType = "standard",
            Count = 1,
            Delvedt = "20260513",
            ShptmName = "便",
            Itemcd = "X",
            Itemnm = longName,
            ExpiryDate = "20260630",
            Shpctrnm = longName,
            PageNo = 3,
            StartPageNo = 1,
            Kikunip = 1,
            UnitName = "g",
            Classification1Name = longName
        };

        var bytes = svc.GeneratePdf(templatePath, new[] { item });
        Assert.NotEmpty(bytes);
        using var ms = new MemoryStream(bytes);
        using var doc = PdfReader.Open(ms, PdfDocumentOpenMode.Import);
        Assert.Equal(1, doc.PageCount);
    }

    private static string ResolveBaggingLabelTemplatePath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "static", "templates", "袋詰現品票1枚.rxz");
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            "袋詰現品票1枚.rxz not found. Run tests from repo root or ensure static/templates is reachable from BaseDirectory.");
    }
}
