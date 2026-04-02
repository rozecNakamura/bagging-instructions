using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;
using BaggingInstructions.Api.DTOs;
using BaggingInstructions.Api.Services;

namespace BaggingInstructions.Api.Controllers;

[ApiController]
[Route("api/cooking-instruction")]
public class CookingInstructionController : ControllerBase
{
    private readonly CookingInstructionService _service;
    private readonly CookingInstructionPdfService _pdfService;
    private readonly IWebHostEnvironment _env;

    public CookingInstructionController(
        CookingInstructionService service,
        CookingInstructionPdfService pdfService,
        IWebHostEnvironment env)
    {
        _service = service;
        _pdfService = pdfService;
        _env = env;
    }

    [HttpGet("workcenters")]
    public async Task<ActionResult<List<CookingInstructionWorkcenterOptionDto>>> ListWorkcenters(CancellationToken ct)
    {
        try
        {
            var list = await _service.ListWorkcentersAsync(ct);
            return Ok(list);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = $"マスタ取得エラー: {ex.Message}" });
        }
    }

    [HttpGet("slots")]
    public async Task<ActionResult<List<CookingInstructionSlotOptionDto>>> ListSlots(CancellationToken ct)
    {
        try
        {
            var list = await _service.ListSlotsAsync(ct);
            return Ok(list);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = $"マスタ取得エラー: {ex.Message}" });
        }
    }

    [HttpGet("search")]
    public async Task<ActionResult<CookingInstructionSearchResponseDto>> Search(
        [FromQuery(Name = "needdate")] string needDate,
        [FromQuery(Name = "workcenter_id")] long[]? workcenterId,
        [FromQuery(Name = "slot_code")] string[]? slotCode,
        CancellationToken ct)
    {
        try
        {
            var wc = workcenterId ?? Array.Empty<long>();
            var sc = slotCode ?? Array.Empty<string>();
            var rows = await _service.SearchAsync(needDate, wc, sc, ct);
            return Ok(new CookingInstructionSearchResponseDto
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

    public sealed class CookingInstructionReportRequestDto
    {
        [JsonPropertyName("needdate")]
        public string NeedDate { get; set; } = "";

        /// <summary>ordertableid の一覧（検索結果のキー）。</summary>
        [JsonPropertyName("orderTableIds")]
        public List<long> OrderTableIds { get; set; } = new();

        /// <summary>互換: 旧クライアントが lineIds のみ送る場合。</summary>
        [JsonPropertyName("lineIds")]
        public List<long>? LineIds { get; set; }
    }

    [HttpPost("report")]
    public async Task<IActionResult> ExportReport([FromBody] CookingInstructionReportRequestDto body, CancellationToken ct)
    {
        var orderIds = ResolveOrderTableIds(body);
        if (body == null || orderIds.Count == 0)
            return BadRequest(new { detail = "印刷するデータを選択してください" });

        var templatePath = Path.Combine(_env.ContentRootPath, "..", "..", "static", "templates", "調理指示書.rxz");
        var fullPath = Path.GetFullPath(templatePath);
        if (!System.IO.File.Exists(fullPath))
            return NotFound(new { detail = "調理指示書テンプレートが見つかりません" });

        try
        {
            var lines = await _service.BuildPdfLineModelsAsync(orderIds, ct);
            if (lines.Count == 0)
                return BadRequest(new { detail = "該当するオーダー行がありません" });

            var reportTitle = await _service.ResolveCookingInstructionReportTitleAsync(fullPath, ct);
            var pdfBytes = _pdfService.GeneratePdf(fullPath, lines, reportTitle);
            if (pdfBytes.Length == 0)
                return BadRequest(new { detail = "印刷する行がありません" });

            var fileName = SafePdfDownloadName(reportTitle);
            return File(pdfBytes, "application/pdf", fileName);
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

    private static IReadOnlyList<long> ResolveOrderTableIds(CookingInstructionReportRequestDto? body)
    {
        if (body == null) return Array.Empty<long>();
        if (body.OrderTableIds is { Count: > 0 })
            return body.OrderTableIds;
        if (body.LineIds is { Count: > 0 })
            return body.LineIds;
        return Array.Empty<long>();
    }

    private static string SafePdfDownloadName(string title)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = title.Trim().Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        var baseName = new string(chars);
        if (string.IsNullOrWhiteSpace(baseName))
            baseName = "調理指示書";
        if (!baseName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            baseName += ".pdf";
        return baseName;
    }
}

