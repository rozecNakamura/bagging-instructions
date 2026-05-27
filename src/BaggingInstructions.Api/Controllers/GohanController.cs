using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using BaggingInstructions.Api.Core;
using BaggingInstructions.Api.Services;

namespace BaggingInstructions.Api.Controllers;

[ApiController]
[Route("api/gohan")]
public class GohanController : ControllerBase
{
    private readonly JuicePdfService _juicePdfService;
    private readonly IWebHostEnvironment _env;

    public GohanController(JuicePdfService juicePdfService, IWebHostEnvironment env)
    {
        _juicePdfService = juicePdfService;
        _env = env;
    }

    /// <summary>ご飯盛り付け指示書を rxz テンプレートで PDF 生成し返す。</summary>
    [HttpPost("pdf")]
    public async Task<IActionResult> GeneratePdf([FromBody] GohanPrintRequest request, CancellationToken ct)
    {
        if (request?.Rows == null || request.Rows.Count == 0)
            return BadRequest(new { detail = "印刷する行を選択してください" });

        var templatePath = Path.Combine(AppContentPaths.TemplatesDirectory(_env), "ご飯盛り付け指示書.rxz");
        var fullPath = Path.GetFullPath(templatePath);
        if (!System.IO.File.Exists(fullPath))
            return NotFound(new { detail = "ご飯盛り付け指示書テンプレートが見つかりません" });

        var rows = request.Rows.Select(r => new GohanPrintRowDto
        {
            Delvedt = r.Delvedt,
            Jobordmernm = r.Jobordmernm,
            Itemcd = r.Itemcd,
            Cuscd = r.Cuscd,
            Shpctrcd = r.Shpctrcd,
            Quantity = r.Quantity,
            Addinfo01 = r.Addinfo01,
            Addinfo08 = r.Addinfo08,
            Addinfo05 = r.Addinfo05,
            Shpctrnm = r.Shpctrnm
        }).ToList();

        // 喫食日・喫食時間（addinfo05）ごとにページを分割
        var groups = rows
            .GroupBy(r => (Delvedt: r.Delvedt ?? "", Addinfo05: (r.Addinfo05 ?? "").Trim()))
            .OrderBy(g => g.Key.Delvedt)
            .ThenBy(g => g.Key.Addinfo05)
            .Select(g => g.ToList())
            .ToList();

        var pagesTagValues = new List<Dictionary<string, string>>();
        var aggregatedGroups = groups
            .Select(g => GohanPdfService.PreparePrintRows(g))
            .ToList();
        var totalPages = aggregatedGroups.Sum(g =>
            (g.Count + GohanPdfService.RowsPerPage - 1) / GohanPdfService.RowsPerPage);
        if (totalPages < 1) totalPages = 1;
        var printNow = DateTime.Now;
        int pageIndex = 0;

        foreach (var aggregated in aggregatedGroups)
        {
            for (int offset = 0; offset < aggregated.Count; offset += GohanPdfService.RowsPerPage)
            {
                var chunk = aggregated.Skip(offset).Take(GohanPdfService.RowsPerPage).ToList();
                var tagValues = GohanPdfService.BuildTagValues(chunk);
                JuicePdfService.AddPrintTags(tagValues, printNow, pageIndex + 1, totalPages);
                pagesTagValues.Add(tagValues);
                pageIndex++;
            }
        }
        var pdfBytes = _juicePdfService.GeneratePdfMultiPage(
            fullPath,
            pagesTagValues,
            "ご飯盛り付け指示書",
            textLayoutFieldFilter: GohanPdfService.ShouldApplyTextLayout);

        await Task.CompletedTask;
        return File(pdfBytes, "application/pdf", "ご飯盛り付け指示書.pdf");
    }
}

public class GohanPrintRequest
{
    [JsonPropertyName("rows")]
    public List<GohanPrintRowRequest> Rows { get; set; } = new();
}

public class GohanPrintRowRequest
{
    [JsonPropertyName("delvedt")]
    public string? Delvedt { get; set; }
    [JsonPropertyName("jobordmernm")]
    public string? Jobordmernm { get; set; }
    [JsonPropertyName("jobordqun")]
    public decimal Jobordqun { get; set; }
    [JsonPropertyName("quantity")]
    public decimal Quantity { get; set; }
    [JsonPropertyName("addinfo01")]
    public string? Addinfo01 { get; set; }
    [JsonPropertyName("addinfo08")]
    public string? Addinfo08 { get; set; }
    [JsonPropertyName("addinfo05")]
    public string? Addinfo05 { get; set; }
    [JsonPropertyName("shpctrnm")]
    public string? Shpctrnm { get; set; }
    [JsonPropertyName("itemcd")]
    public string? Itemcd { get; set; }
    [JsonPropertyName("cuscd")]
    public string? Cuscd { get; set; }
    [JsonPropertyName("shpctrcd")]
    public string? Shpctrcd { get; set; }
}
