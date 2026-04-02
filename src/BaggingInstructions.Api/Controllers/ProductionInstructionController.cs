using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;
using BaggingInstructions.Api.DTOs;
using BaggingInstructions.Api.Services;

namespace BaggingInstructions.Api.Controllers;

[ApiController]
[Route("api/production-instruction")]
public class ProductionInstructionController : ControllerBase
{
    private readonly ProductionInstructionService _service;
    private readonly ProductionInstructionPdfService _pdfService;
    private readonly IWebHostEnvironment _env;

    public ProductionInstructionController(
        ProductionInstructionService service,
        ProductionInstructionPdfService pdfService,
        IWebHostEnvironment env)
    {
        _service = service;
        _pdfService = pdfService;
        _env = env;
    }

    [HttpGet("search")]
    public async Task<ActionResult<ProductionInstructionSearchResponseDto>> Search(
        [FromQuery(Name = "needdate")] string needDate,
        [FromQuery(Name = "workcenter")] string? workcenter,
        [FromQuery(Name = "slot")] string? slot,
        CancellationToken ct)
    {
        try
        {
            var rows = await _service.SearchAsync(needDate, workcenter, slot, ct);
            return Ok(new ProductionInstructionSearchResponseDto
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

    public sealed class ProductionInstructionReportRequestDto
    {
        [JsonPropertyName("needdate")]
        public string NeedDate { get; set; } = "";

        [JsonPropertyName("workcenter")]
        public string? Workcenter { get; set; }

        [JsonPropertyName("slot")]
        public string? Slot { get; set; }

        [JsonPropertyName("orderIds")]
        public List<long> OrderIds { get; set; } = new();
    }

    [HttpPost("report")]
    public async Task<IActionResult> ExportReport([FromBody] ProductionInstructionReportRequestDto body, CancellationToken ct)
    {
        if (body == null || body.OrderIds == null || body.OrderIds.Count == 0)
            return BadRequest(new { detail = "印刷するデータを選択してください" });

        // needdate フォーマット簡易チェック（検索時と同じ YYYYMMDD 前提）
        if (!string.IsNullOrEmpty(body.NeedDate) && body.NeedDate.Length != 8)
            return BadRequest(new { detail = "納期はYYYYMMDD形式（8桁）で指定してください。" });

        var templatePath = Path.Combine(_env.ContentRootPath, "..", "..", "static", "templates", "調味液配合表.rxz");
        var fullPath = Path.GetFullPath(templatePath);
        if (!System.IO.File.Exists(fullPath))
            return NotFound(new { detail = "調味液配合表テンプレートが見つかりません" });

        try
        {
            var lines = await _service.BuildPdfLineModelsAsync(body.OrderIds, ct);
            if (lines.Count == 0)
                return BadRequest(new { detail = "該当する受注明細がありません" });

            var pdfBytes = _pdfService.GeneratePdf(fullPath, lines);
            if (pdfBytes.Length == 0)
                return BadRequest(new { detail = "印刷する行がありません" });

            return File(pdfBytes, "application/pdf", "調味液配合表.pdf");
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

