using Microsoft.AspNetCore.Mvc;
using BaggingInstructions.Api.Core;
using BaggingInstructions.Api.DTOs;
using BaggingInstructions.Api.Services;

namespace BaggingInstructions.Api.Controllers;

/// <summary>
/// 現品票印刷（新）：子品目起点の検索・印刷。
/// マスタ系プルダウン（大分類・中分類・作業区・倉庫）は既存の api/product-label/* を流用する。
/// </summary>
[ApiController]
[Route("api/product-label-new")]
public class ProductLabelNewController : ControllerBase
{
    private const int MaxPrintItems = 5000;

    private readonly ProductLabelNewService _service;
    private readonly ProductLabelPdfService _pdfService;
    private readonly IWebHostEnvironment _env;

    public ProductLabelNewController(
        ProductLabelNewService service,
        ProductLabelPdfService pdfService,
        IWebHostEnvironment env)
    {
        _service = service;
        _pdfService = pdfService;
        _env = env;
    }

    /// <summary>便マスタ一覧（納期当日の起点オーダに現れる便のみ）。</summary>
    [HttpGet("slots")]
    public async Task<ActionResult<List<ProductionInstructionSlotOptionDto>>> ListSlots(
        [FromQuery] string? needdate,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(needdate))
            return Ok(new List<ProductionInstructionSlotOptionDto>());
        try
        {
            var list = await _service.ListSlotsAsync(needdate, ct);
            return Ok(list);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = $"便マスタ取得エラー: {ex.Message}" });
        }
    }

    /// <summary>
    /// 現品票印刷（新）：納期の最上位完成品（ordertype='MO' かつ BOM の childitemcode に存在しない品目）から
    /// BOM を再帰探索し、子孫品目（孫以下も含む）を 1行=1子品目 として返す。
    /// 子品目条件は表示対象の子品目そのものへの絞り込み、親品目条件・便は起点オーダへの絞り込み。
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<ProductLabelNewSearchResponseDto>> Search(
        [FromQuery] string needdate,
        [FromQuery] long? majorclassificationid,
        [FromQuery] string? itemcode,
        [FromQuery] long? workcenterid,
        [FromQuery] long? warehouseid,
        [FromQuery] string? childitemcode,
        [FromQuery] long? childmajorclassificationid,
        [FromQuery] long? childmiddleclassificationid,
        [FromQuery] long? childwarehouseid,
        [FromQuery(Name = "slot_code")] string[]? slotCode,
        CancellationToken ct)
    {
        try
        {
            var rows = await _service.SearchAsync(
                needdate,
                majorclassificationid,
                itemcode,
                workcenterid,
                warehouseid,
                childitemcode,
                childmajorclassificationid,
                childmiddleclassificationid,
                childwarehouseid,
                slotCode ?? Array.Empty<string>(),
                ct);
            return Ok(new ProductLabelNewSearchResponseDto { Total = rows.Count, Rows = rows });
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

    /// <summary>
    /// 現品票（調理）1枚.rxz で PDF 生成。画面でチェックされた行（親×子品目）のみを印刷する。
    /// </summary>
    [HttpPost("pdf")]
    public async Task<IActionResult> GeneratePdf([FromBody] ProductLabelNewPrintRequestDto? body, CancellationToken ct)
    {
        if (body?.Items == null || body.Items.Count == 0)
            return BadRequest(new { detail = "印刷する行を選択してください" });

        if (body.Items.Count > MaxPrintItems)
            return BadRequest(new { detail = $"一度に印刷できる件数は{MaxPrintItems}件までです" });

        if (string.IsNullOrWhiteSpace(body.InstructionType))
            return BadRequest(new { detail = "指示書種別を選択してください" });

        var templatePath = Path.Combine(AppContentPaths.TemplatesDirectory(_env), ProductLabelPdfService.TemplateFileName);
        var fullPath = Path.GetFullPath(templatePath);
        if (!System.IO.File.Exists(fullPath))
            return NotFound(new { detail = "現品票（調理）1枚テンプレートが見つかりません" });

        try
        {
            var rows = await _service.BuildPrintRowsAsync(body.Items, body.InstructionType, Math.Max(1, body.LabelCount), ct);
            if (rows.Count == 0)
                return BadRequest(new { detail = "指示書種別に一致する子品目がありません。指示書種別をご確認ください。" });

            var pdfBytes = _pdfService.GeneratePdfFromRows(fullPath, rows.Select(r => (r.Row, r.Count)).ToList());
            if (pdfBytes.Length == 0)
                return BadRequest(new { detail = "印刷対象が見つかりません" });

            return File(pdfBytes, "application/pdf", "現品票（調理）1枚.pdf");
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = $"PDF 出力エラー: {ex.Message}" });
        }
    }
}
