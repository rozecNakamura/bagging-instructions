using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using BaggingInstructions.Api.Services;

namespace BaggingInstructions.Api.Controllers;

[ApiController]
[Route("api/juice")]
public class JuiceController : ControllerBase
{
    private readonly JuicePdfService _juicePdfService;
    private readonly IWebHostEnvironment _env;

    public JuiceController(JuicePdfService juicePdfService, IWebHostEnvironment env)
    {
        _juicePdfService = juicePdfService;
        _env = env;
    }

    /// <summary>汁仕分表を rxz テンプレートで PDF 生成し返す。</summary>
    [HttpPost("pdf")]
    public async Task<IActionResult> GeneratePdf([FromBody] JuicePrintRequest request, CancellationToken ct)
    {
        if (request?.Rows == null || request.Rows.Count == 0)
            return BadRequest(new { detail = "印刷する行を選択してください" });

        var templatePath = Path.Combine(_env.ContentRootPath, "..", "..", "static", "templates", "汁仕分表.rxz");
        var fullPath = Path.GetFullPath(templatePath);
        if (!System.IO.File.Exists(fullPath))
            return NotFound(new { detail = "汁仕分表テンプレートが見つかりません" });

        var rows = request.Rows.Select(r => new JuicePrintRowDto
        {
            Delvedt = r.Delvedt,
            ShptmDisplay = r.ShptmDisplay,
            Jobordmernm = r.Jobordmernm,
            Shpctrnm = r.Shpctrnm,
            Jobordqun = r.Jobordqun,
            Addinfo02 = r.Addinfo02
        }).ToList();

        var tagValues = JuicePdfService.BuildTagValues(rows);
        var pdfBytes = _juicePdfService.GeneratePdf(fullPath, tagValues);

        await Task.CompletedTask; // 同期処理のため
        return File(pdfBytes, "application/pdf", "汁仕分表.pdf");
    }
}

public class JuicePrintRequest
{
    public List<JuicePrintRowRequest> Rows { get; set; } = new();
}

public class JuicePrintRowRequest
{
    [JsonPropertyName("delvedt")]
    public string? Delvedt { get; set; }
    [JsonPropertyName("shptmDisplay")]
    public string? ShptmDisplay { get; set; }
    [JsonPropertyName("jobordmernm")]
    public string? Jobordmernm { get; set; }
    [JsonPropertyName("shpctrnm")]
    public string? Shpctrnm { get; set; }
    [JsonPropertyName("jobordqun")]
    public decimal Jobordqun { get; set; }
    [JsonPropertyName("addinfo02")]
    public string? Addinfo02 { get; set; }
}
