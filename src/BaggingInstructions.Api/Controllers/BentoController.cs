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

    /// <summary>弁当箱盛り付け指示書（ご飯）を rxz テンプレートで PDF 生成し返す。</summary>
    [HttpPost("pdf")]
    public async Task<IActionResult> GeneratePdf([FromBody] BentoPrintRequest request, CancellationToken ct)
    {
        if (request?.Rows == null || request.Rows.Count == 0)
            return BadRequest(new { detail = "印刷する行を選択してください" });

        var templatePath = Path.Combine(AppContentPaths.TemplatesDirectory(_env), "弁当箱盛り付け指示書（ご飯）.rxz");
        var fullPath = Path.GetFullPath(templatePath);
        if (!System.IO.File.Exists(fullPath))
            return NotFound(new { detail = "弁当箱盛り付け指示書（ご飯）テンプレートが見つかりません" });

        var rows = request.Rows.Select(r => new BentoPrintRowDto
        {
            Delvedt = r.Delvedt,
            ShptmDisplay = r.ShptmDisplay,
            Jobordmernm = r.Jobordmernm,
            Jobordqun = r.Jobordqun,
            Quantity = r.Quantity,
            Addinfo01 = r.Addinfo01,
            Addinfo08 = r.Addinfo08
        }).ToList();

        // addinfo08 の先頭文字（"0" / "1" / その他）でグループ分けしてページを分割
        static string Addinfo08Group(BentoPrintRowDto r)
        {
            var s = (r.Addinfo08 ?? "").TrimStart();
            if (s.StartsWith("0")) return "0";
            if (s.StartsWith("1")) return "1";
            return "";
        }

        var groups = rows
            .GroupBy(Addinfo08Group)
            .Where(g => g.Key == "0" || g.Key == "1")
            .OrderBy(g => g.Key)
            .Select(g => g.ToList())
            .ToList();

        var pagesTagValues = new List<Dictionary<string, string>>();
        var totalPages = groups.Sum(g => (g.Count + BentoPdfService.RowsPerPage - 1) / BentoPdfService.RowsPerPage);
        if (totalPages < 1) totalPages = 1;
        var printNow = DateTime.Now;
        int pageIndex = 0;

        foreach (var group in groups)
        {
            for (int offset = 0; offset < group.Count; offset += BentoPdfService.RowsPerPage)
            {
                var chunk = group.Skip(offset).Take(BentoPdfService.RowsPerPage).ToList();
                var tagValues = BentoPdfService.BuildTagValues(chunk);
                JuicePdfService.AddPrintTags(tagValues, printNow, pageIndex + 1, totalPages);
                pagesTagValues.Add(tagValues);
                pageIndex++;
            }
        }
        var pdfBytes = _juicePdfService.GeneratePdfMultiPage(fullPath, pagesTagValues);

        await Task.CompletedTask;
        return File(pdfBytes, "application/pdf", "弁当箱盛り付け指示書（ご飯）.pdf");
    }
}

public class BentoPrintRequest
{
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
    [JsonPropertyName("jobordqun")]
    public decimal Jobordqun { get; set; }
    [JsonPropertyName("quantity")]
    public decimal Quantity { get; set; }
    [JsonPropertyName("addinfo01")]
    public string? Addinfo01 { get; set; }
    [JsonPropertyName("addinfo08")]
    public string? Addinfo08 { get; set; }
}
