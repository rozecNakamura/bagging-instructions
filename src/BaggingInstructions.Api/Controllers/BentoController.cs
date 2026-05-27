using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using BaggingInstructions.Api.Core;
using BaggingInstructions.Api.Services;

namespace BaggingInstructions.Api.Controllers;

[ApiController]
[Route("api/bento")]
public class BentoController : ControllerBase
{
    private readonly JuicePdfService _juicePdfService;
    private readonly IWebHostEnvironment _env;

    public BentoController(JuicePdfService juicePdfService, IWebHostEnvironment env)
    {
        _juicePdfService = juicePdfService;
        _env = env;
    }

    /// <summary>弁当箱盛り付け指示書を rxz テンプレートで PDF 生成し返す。</summary>
    [HttpPost("pdf")]
    public async Task<IActionResult> GeneratePdf([FromBody] BentoPrintRequest request, CancellationToken ct)
    {
        if (request?.Rows == null || request.Rows.Count == 0)
            return BadRequest(new { detail = "印刷する行を選択してください" });

        var templatePath = Path.Combine(AppContentPaths.TemplatesDirectory(_env), "弁当箱盛り付け指示書（ご飯）.rxz");
        var fullPath = Path.GetFullPath(templatePath);
        if (!System.IO.File.Exists(fullPath))
            return NotFound(new { detail = "弁当箱盛り付け指示書テンプレートが見つかりません" });

        var bentoType = request.BentoType;
        var rows = request.Rows.Select(r => new BentoPrintRowDto
        {
            Delvedt = r.Delvedt,
            ShptmDisplay = r.ShptmDisplay,
            Jobordmernm = r.Jobordmernm,
            Itemcd = r.Itemcd,
            Shpctrcd = r.Shpctrcd,
            Shpctrnm = r.Shpctrnm,
            Jobordqun = r.Jobordqun,
            Quantity = r.Quantity,
            Addinfo01 = r.Addinfo01,
            Addinfo05 = r.Addinfo05,
            Info17 = r.Info17,
            FoodTypeName = r.FoodTypeName
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
            .Select(g => BentoPdfService.PreparePrintRows(g, bentoType))
            .ToList();
        var totalPages = aggregatedGroups.Sum(g =>
            (g.Count + BentoPdfService.RowsPerPage - 1) / BentoPdfService.RowsPerPage);
        if (totalPages < 1) totalPages = 1;
        var printNow = DateTime.Now;
        int pageIndex = 0;

        foreach (var aggregated in aggregatedGroups)
        {
            for (int offset = 0; offset < aggregated.Count; offset += BentoPdfService.RowsPerPage)
            {
                var chunk = aggregated.Skip(offset).Take(BentoPdfService.RowsPerPage).ToList();
                var tagValues = BentoPdfService.BuildTagValues(chunk, bentoType);
                JuicePdfService.AddPrintTags(tagValues, printNow, pageIndex + 1, totalPages);
                pagesTagValues.Add(tagValues);
                pageIndex++;
            }
        }

        var pdfTitle = BentoSearchFilter.IsGohan(bentoType)
            ? "弁当箱盛り付け指示書（ご飯）"
            : "弁当箱盛り付け指示書（おかず）";
        var pdfBytes = _juicePdfService.GeneratePdfMultiPage(
            fullPath,
            pagesTagValues,
            pdfTitle,
            textLayoutFieldFilter: BentoPdfService.ShouldApplyTextLayout);

        await Task.CompletedTask;
        return File(pdfBytes, "application/pdf", $"{pdfTitle}.pdf");
    }
}

public class BentoPrintRequest
{
    [JsonPropertyName("bentoType")]
    public string? BentoType { get; set; }

    [JsonPropertyName("rows")]
    public List<BentoPrintRowRequest> Rows { get; set; } = new();
}

public class BentoPrintRowRequest
{
    [JsonPropertyName("delvedt")]
    public string? Delvedt { get; set; }
    [JsonPropertyName("shptmDisplay")]
    public string? ShptmDisplay { get; set; }
    [JsonPropertyName("jobordmernm")]
    public string? Jobordmernm { get; set; }
    [JsonPropertyName("itemcd")]
    public string? Itemcd { get; set; }
    [JsonPropertyName("shpctrcd")]
    public string? Shpctrcd { get; set; }
    [JsonPropertyName("shpctrnm")]
    public string? Shpctrnm { get; set; }
    [JsonPropertyName("jobordqun")]
    public decimal Jobordqun { get; set; }
    [JsonPropertyName("quantity")]
    public decimal Quantity { get; set; }
    [JsonPropertyName("addinfo01")]
    public string? Addinfo01 { get; set; }
    [JsonPropertyName("addinfo05")]
    public string? Addinfo05 { get; set; }
    [JsonPropertyName("info17")]
    public string? Info17 { get; set; }
    [JsonPropertyName("foodTypeName")]
    public string? FoodTypeName { get; set; }
}
