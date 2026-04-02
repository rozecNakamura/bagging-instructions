using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;
using BaggingInstructions.Api.DTOs;
using BaggingInstructions.Api.Services;

namespace BaggingInstructions.Api.Controllers;

[ApiController]
[Route("api/inspection-record")]
public class InspectionRecordController : ControllerBase
{
    private readonly InspectionRecordService _service;
    private readonly InspectionRecordPdfService _pdfService;
    private readonly IWebHostEnvironment _env;

    public InspectionRecordController(
        InspectionRecordService service,
        InspectionRecordPdfService pdfService,
        IWebHostEnvironment env)
    {
        _service = service;
        _pdfService = pdfService;
        _env = env;
    }

    [HttpGet("suppliers")]
    public async Task<ActionResult<List<InspectionRecordSupplierOptionDto>>> Suppliers(CancellationToken ct)
    {
        try
        {
            var list = await _service.ListSupplierOptionsAsync(ct);
            return Ok(list);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = $"仕入先一覧取得エラー: {ex.Message}" });
        }
    }

    [HttpGet("search")]
    public async Task<ActionResult<InspectionRecordSearchResponseDto>> Search(
        [FromQuery(Name = "needdate")] string needDate,
        [FromQuery(Name = "supplierCodes")] List<string>? supplierCodes,
        CancellationToken ct)
    {
        try
        {
            var rows = await _service.SearchAsync(needDate, supplierCodes, ct);
            return Ok(new InspectionRecordSearchResponseDto
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

    public sealed class InspectionRecordReportRequestDto
    {
        [JsonPropertyName("needdate")]
        public string NeedDate { get; set; } = "";

        [JsonPropertyName("lineIds")]
        public List<long> LineIds { get; set; } = new();
    }

    [HttpPost("report")]
    public async Task<IActionResult> ExportReport([FromBody] InspectionRecordReportRequestDto body, CancellationToken ct)
    {
        if (body == null || body.LineIds == null || body.LineIds.Count == 0)
            return BadRequest(new { detail = "印刷するデータを選択してください" });

        if (string.IsNullOrEmpty(body.NeedDate) || body.NeedDate.Length != 8)
            return BadRequest(new { detail = "納期はYYYYMMDD形式（8桁）で指定してください。" });

        var templatePath = Path.Combine(_env.ContentRootPath, "..", "..", "static", "templates", "検品記録簿.rxz");
        var fullPath = Path.GetFullPath(templatePath);
        if (!System.IO.File.Exists(fullPath))
            return NotFound(new { detail = "検品記録簿テンプレートが見つかりません" });

        try
        {
            var lines = await _service.BuildPdfLineModelsAsync(body.LineIds, ct);
            if (lines.Count == 0)
                return BadRequest(new { detail = "該当する受注明細がありません" });

            var pdfBytes = _pdfService.GeneratePdf(fullPath, lines);
            if (pdfBytes.Length == 0)
                return BadRequest(new { detail = "印刷する行がありません" });

            return File(pdfBytes, "application/pdf", "検品記録簿.pdf");
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

