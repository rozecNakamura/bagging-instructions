using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using BaggingInstructions.Api.DTOs;
using BaggingInstructions.Api.Services;

namespace BaggingInstructions.Api.Controllers;

[ApiController]
[Route("api/personal-delivery")]
public class PersonalDeliveryController : ControllerBase
{
    private readonly PersonalDeliveryService _searchService;
    private readonly PersonalDeliveryPdfService _pdfService;
    private readonly IWebHostEnvironment _env;

    public PersonalDeliveryController(
        PersonalDeliveryService searchService,
        PersonalDeliveryPdfService pdfService,
        IWebHostEnvironment env)
    {
        _searchService = searchService;
        _pdfService = pdfService;
        _env = env;
    }

    /// <summary>配送日で個人配送指示書検索（配送日・喫食時間・配送エリア）</summary>
    [HttpGet("search")]
    public async Task<ActionResult<PersonalDeliverySearchResponseDto>> Search(
        [FromQuery] string delvedt,
        CancellationToken ct)
    {
        try
        {
            var normalized = delvedt?.Replace("-", "") ?? "";
            var items = await _searchService.SearchByDeliveryDateAsync(normalized, ct);
            return Ok(new PersonalDeliverySearchResponseDto { Total = items.Count, Items = items });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = $"検索エラー: {ex.Message}" });
        }
    }

    /// <summary>個人配送指示書PDFを生成。variant=detail は個人配送指示書.rxz のみ、variant=summary は個人配送指示書（集計）.rxz のみ。</summary>
    [HttpPost("pdf")]
    public async Task<IActionResult> GeneratePdf([FromBody] PersonalDeliveryPrintRequest request, CancellationToken ct)
    {
        if (request?.Rows == null || request.Rows.Count == 0)
            return BadRequest(new { detail = "印刷する行を選択してください" });

        var variant = (request.Variant ?? "").Trim().ToLowerInvariant();
        if (variant != "detail" && variant != "summary")
            return BadRequest(new { detail = "variant は 'detail' または 'summary' を指定してください" });

        var templateDetailPath = Path.Combine(_env.ContentRootPath, "..", "..", "static", "templates", "個人配送指示書.rxz");
        var templateSummaryPath = Path.Combine(_env.ContentRootPath, "..", "..", "static", "templates", "個人配送指示書（集計）.rxz");
        var fullDetailPath = Path.GetFullPath(templateDetailPath);
        var fullSummaryPath = Path.GetFullPath(templateSummaryPath);

        if (variant == "detail" && !System.IO.File.Exists(fullDetailPath))
            return NotFound(new { detail = "個人配送指示書テンプレートが見つかりません" });
        if (variant == "summary" && !System.IO.File.Exists(fullSummaryPath))
            return NotFound(new { detail = "個人配送指示書（集計）テンプレートが見つかりません" });

        var rows = request.Rows
            .Select(r => (
                DeliveryDate: r.DeliveryDate ?? "",
                TimeName: r.TimeName ?? "",
                Area: r.Area ?? ""))
            .Where(t => !string.IsNullOrEmpty(t.DeliveryDate))
            .ToList();

        if (rows.Count == 0)
            return BadRequest(new { detail = "有効な印刷対象がありません" });

        var pdfBytes = await _pdfService.GeneratePdfAsync(fullDetailPath, fullSummaryPath, rows, variant, ct);
        var fileName = variant == "detail" ? "個人配送指示書.pdf" : "個人配送指示書（集計）.pdf";
        return File(pdfBytes, "application/pdf", fileName);
    }
}

public class PersonalDeliveryPrintRequest
{
    [JsonPropertyName("rows")]
    public List<PersonalDeliveryPrintRowRequest> Rows { get; set; } = new();

    /// <summary>"detail" = 個人配送指示書.rxz のみ / "summary" = 個人配送指示書（集計）.rxz のみ</summary>
    [JsonPropertyName("variant")]
    public string? Variant { get; set; }
}

public class PersonalDeliveryPrintRowRequest
{
    [JsonPropertyName("delivery_date")]
    public string? DeliveryDate { get; set; }

    [JsonPropertyName("time_name")]
    public string? TimeName { get; set; }

    [JsonPropertyName("area")]
    public string? Area { get; set; }
}
