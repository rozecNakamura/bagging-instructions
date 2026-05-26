using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using BaggingInstructions.Api.Core;
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

        var templatePath = Path.Combine(AppContentPaths.TemplatesDirectory(_env), "汁仕分表.rxz");
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
            Addinfo01 = r.Addinfo01,
            Addinfo05 = r.Addinfo05
        }).ToList();

        // 喫食時間ごとにページを分割
        var groups = rows
            .GroupBy(r => r.ShptmDisplay ?? "")
            .OrderBy(g => g.Key)
            .Select(g => g.ToList())
            .ToList();

        var printNow = DateTime.Now;
        int totalPages = groups.Sum(g => (g.Count + JuicePdfService.RowsPerPage - 1) / JuicePdfService.RowsPerPage);
        if (totalPages < 1) totalPages = 1;

        var allPageTagValues = new List<Dictionary<string, string>>();
        int pageIndex = 0;
        foreach (var group in groups)
        {
            for (int offset = 0; offset < group.Count; offset += JuicePdfService.RowsPerPage)
            {
                var chunk = group.Skip(offset).Take(JuicePdfService.RowsPerPage).ToList();
                var tagValues = JuicePdfService.BuildTagValues(chunk);
                JuicePdfService.AddPrintTags(tagValues, printNow, pageIndex + 1, totalPages);
                allPageTagValues.Add(tagValues);
                pageIndex++;
            }
        }

        var pdfBytes = _juicePdfService.GeneratePdfMultiPage(fullPath, allPageTagValues, "汁仕分表");

        await Task.CompletedTask;
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
    [JsonPropertyName("addinfo01")]
    public string? Addinfo01 { get; set; }
    [JsonPropertyName("addinfo05")]
    public string? Addinfo05 { get; set; }
}
