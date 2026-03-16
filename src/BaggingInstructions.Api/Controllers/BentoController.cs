using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
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

        var templatePath = Path.Combine(_env.ContentRootPath, "..", "..", "static", "templates", "弁当箱盛り付け指示書（ご飯）.rxz");
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
            Addinfo02 = r.Addinfo02
        }).ToList();

        var pagesTagValues = new List<Dictionary<string, string>>();
        for (int offset = 0; offset < rows.Count; offset += BentoPdfService.RowsPerPage)
        {
            var chunk = rows.Skip(offset).Take(BentoPdfService.RowsPerPage).ToList();
            pagesTagValues.Add(BentoPdfService.BuildTagValues(chunk));
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
    [JsonPropertyName("addinfo02")]
    public string? Addinfo02 { get; set; }
}
