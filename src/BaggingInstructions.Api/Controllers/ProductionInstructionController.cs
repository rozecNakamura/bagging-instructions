using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;
using BaggingInstructions.Api.DTOs;
using BaggingInstructions.Api.Services;
using static BaggingInstructions.Api.ProductionInstructionReportKinds;

namespace BaggingInstructions.Api.Controllers;

[ApiController]
[Route("api/production-instruction")]
public class ProductionInstructionController : ControllerBase
{
    private readonly ProductionInstructionService _service;
    private readonly ProductionInstructionPdfService _pdfService;
    private readonly HoikoloProductionInstructionPdfService _hoikoloPdfService;
    private readonly IWebHostEnvironment _env;

    public ProductionInstructionController(
        ProductionInstructionService service,
        ProductionInstructionPdfService pdfService,
        HoikoloProductionInstructionPdfService hoikoloPdfService,
        IWebHostEnvironment env)
    {
        _service = service;
        _pdfService = pdfService;
        _hoikoloPdfService = hoikoloPdfService;
        _env = env;
    }

    [HttpGet("workcenters")]
    public async Task<ActionResult<List<ProductionInstructionWorkcenterOptionDto>>> ListWorkcenters(CancellationToken ct)
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
    public async Task<ActionResult<List<ProductionInstructionSlotOptionDto>>> ListSlots(CancellationToken ct)
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
    public async Task<ActionResult<ProductionInstructionSearchResponseDto>> Search(
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

        [JsonPropertyName("workcenter_id")]
        public List<long>? WorkcenterId { get; set; }

        [JsonPropertyName("slot_code")]
        public List<string>? SlotCode { get; set; }

        [JsonPropertyName("orderIds")]
        public List<long> OrderIds { get; set; } = new();

        /// <summary>省略または "chomi" = 調味液配合表。"hoikolo" = 生産指示書_ホイコーロー。</summary>
        [JsonPropertyName("report_variant")]
        public string? ReportVariant { get; set; }
    }

    [HttpPost("report")]
    public async Task<IActionResult> ExportReport([FromBody] ProductionInstructionReportRequestDto body, CancellationToken ct)
    {
        if (body == null || body.OrderIds == null || body.OrderIds.Count == 0)
            return BadRequest(new { detail = "印刷するデータを選択してください" });

        // needdate フォーマット簡易チェック（検索時と同じ YYYYMMDD 前提）
        if (!string.IsNullOrEmpty(body.NeedDate) && body.NeedDate.Length != 8)
            return BadRequest(new { detail = "納期はYYYYMMDD形式（8桁）で指定してください。" });

        var variant = (body.ReportVariant ?? "").Trim().ToLowerInvariant();
        if (IsInvalidVariant(variant))
            return BadRequest(new { detail = "report_variant は 'chomi' または 'hoikolo' を指定してください。" });

        var isHoikolo = IsHoikolo(variant);
        var templateFile = isHoikolo ? HoikoloTemplateFileName : ChomiTemplateFileName;
        var templatePath = Path.Combine(_env.ContentRootPath, "..", "..", "static", "templates", templateFile);
        var fullPath = Path.GetFullPath(templatePath);
        if (!System.IO.File.Exists(fullPath))
            return NotFound(new { detail = isHoikolo ? "生産指示書_ホイコーローテンプレートが見つかりません" : "調味液配合表テンプレートが見つかりません" });

        try
        {
            var lines = await _service.BuildPdfLineModelsAsync(body.OrderIds, ct);
            if (lines.Count == 0)
                return BadRequest(new { detail = "該当する受注明細がありません" });

            byte[] pdfBytes;
            string downloadName;
            if (isHoikolo)
            {
                pdfBytes = _hoikoloPdfService.GeneratePdf(fullPath, lines);
                downloadName = HoikoloDownloadFileName;
            }
            else
            {
                pdfBytes = _pdfService.GeneratePdf(fullPath, lines);
                downloadName = ChomiDownloadFileName;
            }

            if (pdfBytes.Length == 0)
                return BadRequest(new { detail = "印刷する行がありません" });

            return File(pdfBytes, "application/pdf", downloadName);
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

