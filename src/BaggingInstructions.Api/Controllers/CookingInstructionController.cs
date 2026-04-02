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

    [HttpGet("search")]
    public async Task<ActionResult<CookingInstructionSearchResponseDto>> Search(
        [FromQuery(Name = "needdate")] string needDate,
        [FromQuery(Name = "workplace")] string? workplace,
        [FromQuery(Name = "slot")] string? slot,
        CancellationToken ct)
    {
        try
        {
            var rows = await _service.SearchAsync(needDate, workplace, slot, ct);
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

        [JsonPropertyName("workplace")]
        public string? Workplace { get; set; }

        [JsonPropertyName("slot")]
        public string? Slot { get; set; }

        [JsonPropertyName("lineIds")]
        public List<long> LineIds { get; set; } = new();
    }

    [HttpPost("report")]
    public async Task<IActionResult> ExportReport([FromBody] CookingInstructionReportRequestDto body, CancellationToken ct)
    {
        if (body == null || body.LineIds == null || body.LineIds.Count == 0)
            return BadRequest(new { detail = "印刷するデータを選択してください" });

        var templatePath = Path.Combine(_env.ContentRootPath, "..", "..", "static", "templates", "調理指示書.rxz");
        var fullPath = Path.GetFullPath(templatePath);
        if (!System.IO.File.Exists(fullPath))
            return NotFound(new { detail = "調理指示書テンプレートが見つかりません" });

        try
        {
            var lines = await _service.BuildPdfLineModelsAsync(body.LineIds, ct);
            if (lines.Count == 0)
                return BadRequest(new { detail = "該当する受注明細がありません" });

            var pdfBytes = _pdfService.GeneratePdf(fullPath, lines);
            if (pdfBytes.Length == 0)
                return BadRequest(new { detail = "印刷する行がありません" });

            return File(pdfBytes, "application/pdf", "調理指示書.pdf");
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

