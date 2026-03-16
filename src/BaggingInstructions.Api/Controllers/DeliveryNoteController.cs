using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using BaggingInstructions.Api.DTOs;
using BaggingInstructions.Api.Services;

namespace BaggingInstructions.Api.Controllers;

[ApiController]
[Route("api/delivery-note")]
public class DeliveryNoteController : ControllerBase
{
    private readonly DeliveryNoteService _searchService;
    private readonly DeliveryNotePdfService _pdfService;
    private readonly IWebHostEnvironment _env;

    public DeliveryNoteController(DeliveryNoteService searchService, DeliveryNotePdfService pdfService, IWebHostEnvironment env)
    {
        _searchService = searchService;
        _pdfService = pdfService;
        _env = env;
    }

    /// <summary>喫食日で納品書検索（喫食日・納入場所名）。cstmeat.info03 + customerdeliverylocation 結合。</summary>
    [HttpGet("search")]
    public async Task<ActionResult<DeliveryNoteSearchResponseDto>> Search(
        [FromQuery] string delvedt,
        CancellationToken ct)
    {
        try
        {
            var normalized = delvedt?.Replace("-", "") ?? "";
            var items = await _searchService.SearchByEatingDateAsync(normalized, ct);
            return Ok(new DeliveryNoteSearchResponseDto { Total = items.Count, Items = items });
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

    /// <summary>納品書を rxz テンプレートで PDF 生成し返す。複数選択時は複数ページに結合。</summary>
    [HttpPost("pdf")]
    public async Task<IActionResult> GeneratePdf([FromBody] DeliveryNotePrintRequest request, CancellationToken ct)
    {
        if (request?.Rows == null || request.Rows.Count == 0)
            return BadRequest(new { detail = "印刷する行を選択してください" });

        var templatePath = Path.Combine(_env.ContentRootPath, "..", "..", "static", "templates", "納品書.rxz");
        var fullPath = Path.GetFullPath(templatePath);
        if (!System.IO.File.Exists(fullPath))
            return NotFound(new { detail = "納品書テンプレートが見つかりません" });

        var rows = request.Rows
            .Select(r => (EatingDate: r.EatingDate ?? "", LocationCode: r.LocationCode ?? "", CustomerCode: r.CustomerCode ?? ""))
            .Where(t => !string.IsNullOrEmpty(t.EatingDate) && !string.IsNullOrEmpty(t.LocationCode))
            .ToList();

        if (rows.Count == 0)
            return BadRequest(new { detail = "有効な印刷対象がありません" });

        var pdfBytes = _pdfService.GenerateMergedPdf(fullPath, rows);
        await Task.CompletedTask;
        return File(pdfBytes, "application/pdf", "納品書.pdf");
    }
}

public class DeliveryNotePrintRequest
{
    [JsonPropertyName("rows")]
    public List<DeliveryNotePrintRowRequest> Rows { get; set; } = new();
}

public class DeliveryNotePrintRowRequest
{
    [JsonPropertyName("eating_date")]
    public string? EatingDate { get; set; }

    [JsonPropertyName("location_code")]
    public string? LocationCode { get; set; }

    [JsonPropertyName("customer_code")]
    public string? CustomerCode { get; set; }
}
