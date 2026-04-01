using Microsoft.AspNetCore.Mvc;
using BaggingInstructions.Api.DTOs;
using BaggingInstructions.Api.Services;

namespace BaggingInstructions.Api.Controllers;

[ApiController]
[Route("api/aggregate-summary")]
public class AggregateSummaryController : ControllerBase
{
    private readonly AggregateSummaryService _service;
    private readonly AggregateSummaryPdfService _pdfService;
    private readonly IWebHostEnvironment _env;

    public AggregateSummaryController(
        AggregateSummaryService service,
        AggregateSummaryPdfService pdfService,
        IWebHostEnvironment env)
    {
        _service = service;
        _pdfService = pdfService;
        _env = env;
    }

    [HttpGet]
    public async Task<ActionResult<AggregateSummarySearchResponseDto>> Search(
        [FromQuery(Name = "from_date")] string fromDate,
        [FromQuery(Name = "to_date")] string? toDate,
        [FromQuery(Name = "item_code")] string? itemCode,
        [FromQuery(Name = "major_class")] string? majorClass,
        [FromQuery(Name = "middle_class")] string? middleClass,
        CancellationToken ct)
    {
        try
        {
            var rows = await _service.SearchSummaryAsync(fromDate, toDate, itemCode, majorClass, middleClass, ct);
            return Ok(new AggregateSummarySearchResponseDto
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

    [HttpPost("report")]
    public async Task<IActionResult> ExportReport([FromBody] AggregateSummaryReportRequestDto body, CancellationToken ct)
    {
        if (body == null || body.SummaryKeys == null || body.SummaryKeys.Count == 0)
            return BadRequest(new { detail = "印刷するグループを選択してください" });

        var templatePath = Path.Combine(_env.ContentRootPath, "..", "..", "static", "templates", "集計表.rxz");
        var fullPath = Path.GetFullPath(templatePath);
        if (!System.IO.File.Exists(fullPath))
            return NotFound(new { detail = "集計表テンプレートが見つかりません" });

        try
        {
            var lines = await _service.BuildPdfLineModelsForSummaryAsync(body.Filter, body.SummaryKeys, ct);
            if (lines.Count == 0)
                return BadRequest(new { detail = "該当する受注明細がありません" });

            var pdfBytes = _pdfService.GeneratePdf(fullPath, lines);
            if (pdfBytes.Length == 0)
                return BadRequest(new { detail = "印刷する行がありません" });

            return File(pdfBytes, "application/pdf", "集計表.pdf");
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

