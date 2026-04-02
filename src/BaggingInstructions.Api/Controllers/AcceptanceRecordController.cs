using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;
using BaggingInstructions.Api.DTOs;
using BaggingInstructions.Api.Services;

namespace BaggingInstructions.Api.Controllers;

[ApiController]
[Route("api/acceptance-record")]
public class AcceptanceRecordController : ControllerBase
{
    private readonly AcceptanceRecordService _service;
    private readonly AcceptanceRecordPdfService _pdfService;
    private readonly IWebHostEnvironment _env;

    public AcceptanceRecordController(
        AcceptanceRecordService service,
        AcceptanceRecordPdfService pdfService,
        IWebHostEnvironment env)
    {
        _service = service;
        _pdfService = pdfService;
        _env = env;
    }

    /// <summary>店舗（納入場所）マルチセレクト用マスタ。</summary>
    [HttpGet("delivery-locations")]
    public async Task<ActionResult<List<AcceptanceRecordDeliveryLocationOptionDto>>> DeliveryLocations(CancellationToken ct)
    {
        try
        {
            var list = await _service.ListDeliveryLocationOptionsAsync(ct);
            return Ok(list);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = $"納入場所一覧取得エラー: {ex.Message}" });
        }
    }

    [HttpGet("search")]
    public async Task<ActionResult<AcceptanceRecordSearchResponseDto>> Search(
        [FromQuery(Name = "deliverydate")] string deliveryDate,
        [FromQuery(Name = "shipdate")] string? shipDate,
        [FromQuery(Name = "storePair")] List<string>? storePairs,
        CancellationToken ct)
    {
        try
        {
            var rows = await _service.SearchAsync(deliveryDate, shipDate, storePairs, ct);
            return Ok(new AcceptanceRecordSearchResponseDto
            {
                Total = rows.Count,
                Rows = rows
            });
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

    public sealed class AcceptanceRecordReportRequestDto
    {
        [JsonPropertyName("deliverydate")]
        public string DeliveryDate { get; set; } = "";

        [JsonPropertyName("shipdate")]
        public string? ShipDate { get; set; }

        [JsonPropertyName("lineIds")]
        public List<long> LineIds { get; set; } = new();

        [JsonPropertyName("headerLocation")]
        public string? HeaderLocation { get; set; }

        [JsonPropertyName("headerOutDate")]
        public string? HeaderOutDate { get; set; }

        [JsonPropertyName("headerDelvDate")]
        public string? HeaderDelvDate { get; set; }
    }

    [HttpPost("report")]
    public async Task<IActionResult> ExportReport([FromBody] AcceptanceRecordReportRequestDto body, CancellationToken ct)
    {
        if (body == null || body.LineIds == null || body.LineIds.Count == 0)
            return BadRequest(new { detail = "印刷するデータを選択してください" });

        if (string.IsNullOrEmpty(body.DeliveryDate) || body.DeliveryDate.Length != 8)
            return BadRequest(new { detail = "納品日はYYYYMMDD形式（8桁）で指定してください。" });

        var templatePath = Path.Combine(_env.ContentRootPath, "..", "..", "static", "templates", "検収の記録簿.rxz");
        var fullPath = Path.GetFullPath(templatePath);
        if (!System.IO.File.Exists(fullPath))
            return NotFound(new { detail = "検収の記録簿テンプレートが見つかりません" });

        try
        {
            var lines = await _service.BuildPdfLineModelsAsync(body.LineIds, ct);
            if (lines.Count == 0)
                return BadRequest(new { detail = "該当する受注明細がありません" });

            var pdfBytes = _pdfService.GeneratePdf(
                fullPath,
                lines,
                body.HeaderLocation,
                body.HeaderOutDate,
                body.HeaderDelvDate);
            if (pdfBytes.Length == 0)
                return BadRequest(new { detail = "印刷する行がありません" });

            return File(pdfBytes, "application/pdf", "検収の記録簿.pdf");
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = $"PDF 出力エラー: {ex.Message}" });
        }
    }
}
