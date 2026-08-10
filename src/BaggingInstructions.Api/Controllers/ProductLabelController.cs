using Microsoft.AspNetCore.Mvc;
using BaggingInstructions.Api.Core;
using BaggingInstructions.Api.DTOs;
using BaggingInstructions.Api.Services;

namespace BaggingInstructions.Api.Controllers;

[ApiController]
[Route("api/product-label")]
public class ProductLabelController : ControllerBase
{
    private const int MaxPrintOrderIds = 5000;

    private readonly SearchService _searchService;
    private readonly ProductLabelPdfService _pdfService;
    private readonly PreparationWorkService _preparationWorkService;
    private readonly IWebHostEnvironment _env;

    public ProductLabelController(
        SearchService searchService,
        ProductLabelPdfService pdfService,
        PreparationWorkService preparationWorkService,
        IWebHostEnvironment env)
    {
        _searchService = searchService;
        _pdfService = pdfService;
        _preparationWorkService = preparationWorkService;
        _env = env;
    }

    /// <summary>大分類マスタ一覧（検索条件のプルダウン用）。</summary>
    [HttpGet("major-classifications")]
    public async Task<ActionResult<List<MajorClassificationOptionDto>>> ListMajorClassifications(CancellationToken ct)
    {
        try
        {
            var list = await _searchService.ListMajorClassificationsAsync(ct);
            return Ok(list);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = $"取得エラー: {ex.Message}" });
        }
    }

    /// <summary>
    /// 中分類マスタ一覧（検索条件のプルダウン用）。
    /// majorclassificationid 指定時はその大分類に紐づく中分類のみ、未指定時は全件。
    /// </summary>
    [HttpGet("middle-classifications")]
    public async Task<ActionResult<List<MiddleClassificationOptionDto>>> ListMiddleClassifications(
        [FromQuery] long? majorclassificationid,
        CancellationToken ct)
    {
        try
        {
            var list = await _searchService.ListMiddleClassificationsAsync(majorclassificationid, ct);
            return Ok(list);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = $"取得エラー: {ex.Message}" });
        }
    }

    /// <summary>作業区マスタ一覧（検索条件のプルダウン用）。</summary>
    [HttpGet("workcenters")]
    public async Task<ActionResult<List<PreparationWorkWorkcenterOptionDto>>> ListWorkcenters(CancellationToken ct)
    {
        try
        {
            var list = await _preparationWorkService.ListWorkcentersAsync(ct);
            return Ok(list);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = $"作業区マスタ取得エラー: {ex.Message}" });
        }
    }

    /// <summary>倉庫マスタ一覧（検索条件のプルダウン用）。</summary>
    [HttpGet("warehouses")]
    public async Task<ActionResult<List<PreparationWorkWarehouseOptionDto>>> ListWarehouses(CancellationToken ct)
    {
        try
        {
            var list = await _preparationWorkService.ListWarehousesAsync(ct);
            return Ok(list);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = $"倉庫マスタ取得エラー: {ex.Message}" });
        }
    }

    /// <summary>
    /// 現品票：ordertable を納期で明細検索。
    /// 対象はMO品目かつBOM childitemcodeに存在しない品目（親品目が存在しない品目）のみ。
    /// 大分類・品目コード・作業区・倉庫はすべて任意。
    /// 子品目条件（子品目コード・子品目大分類・子品目中分類・子品目倉庫）も任意で、
    /// 指定時は BOM を再帰探索して条件に一致する子品目を持つ親のみを抽出する。
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<ProductLabelSearchResponseDto>> Search(
        [FromQuery] string needdate,
        [FromQuery] long? majorclassificationid,
        [FromQuery] string? itemcode,
        [FromQuery] long? workcenterid,
        [FromQuery] long? warehouseid,
        [FromQuery] string? childitemcode,
        [FromQuery] long? childmajorclassificationid,
        [FromQuery] long? childmiddleclassificationid,
        [FromQuery] long? childwarehouseid,
        CancellationToken ct)
    {
        try
        {
            var rows = await _searchService.SearchProductLabelAsync(
                needdate,
                majorclassificationid,
                itemcode,
                workcenterid,
                warehouseid,
                childitemcode,
                childmajorclassificationid,
                childmiddleclassificationid,
                childwarehouseid,
                ct);
            return Ok(new ProductLabelSearchResponseDto { Total = rows.Count, Rows = rows });
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
    /// 現品票：親品目コード一覧を子品目条件で絞り込む。
    /// 調味液配合表（親大分類55）は別ルート（製造指示書検索）で明細を取得するため、
    /// その結果へ子品目条件を後掛けするために使用する。
    /// </summary>
    [HttpPost("filter-by-child")]
    public async Task<ActionResult<ProductLabelChildFilterResponseDto>> FilterByChild(
        [FromBody] ProductLabelChildFilterRequestDto? body,
        CancellationToken ct)
    {
        if (body?.ItemCodes == null || body.ItemCodes.Count == 0)
            return Ok(new ProductLabelChildFilterResponseDto());

        try
        {
            var codes = await _searchService.FilterProductLabelParentsByChildAsync(
                body.ItemCodes,
                body.ChildItemCode,
                body.ChildMajorClassificationId,
                body.ChildMiddleClassificationId,
                body.ChildWarehouseId,
                ct);
            return Ok(new ProductLabelChildFilterResponseDto { ItemCodes = codes });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = $"絞り込みエラー: {ex.Message}" });
        }
    }

    /// <summary>
    /// 現品票（調理）.rxz で PDF 生成。
    /// order_table_ids は ordertableid（BOM最上位のMO品目）。
    /// instruction_type 指定時は BOM を再帰探索し条件に一致する子品目のラベルを生成。
    /// </summary>
    [HttpPost("pdf")]
    public async Task<IActionResult> GeneratePdf([FromBody] ProductLabelPrintRequestDto? body, CancellationToken ct)
    {
        if (body?.OrderTableIds == null || body.OrderTableIds.Count == 0)
            return BadRequest(new { detail = "印刷するオーダを選択してください" });

        if (body.OrderTableIds.Count > MaxPrintOrderIds)
            return BadRequest(new { detail = $"一度に印刷できる件数は{MaxPrintOrderIds}件までです" });

        if (string.IsNullOrWhiteSpace(body.InstructionType))
            return BadRequest(new { detail = "指示書種別を選択してください" });

        var templatePath = Path.Combine(AppContentPaths.TemplatesDirectory(_env), ProductLabelPdfService.TemplateFileName);
        var fullPath = Path.GetFullPath(templatePath);
        if (!System.IO.File.Exists(fullPath))
            return NotFound(new { detail = "現品票（調理）1枚テンプレートが見つかりません" });

        try
        {
            var labelCount = Math.Max(1, body.LabelCount);
            IReadOnlyDictionary<string, int>? perRowCounts = body.PerRowCounts is { Count: > 0 } d ? d : null;
            var pdfBytes = await _pdfService.GeneratePdfAsync(fullPath, body.OrderTableIds, labelCount, body.InstructionType, perRowCounts, ct);
            if (pdfBytes.Length == 0)
                return BadRequest(new { detail = "BOM内に該当する品目が見つかりません。指示書種別をご確認ください。" });

            return File(pdfBytes, "application/pdf", "現品票（調理）1枚.pdf");
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = $"PDF 出力エラー: {ex.Message}" });
        }
    }

    /// <summary>
    /// 受注明細 ID から ordertableid を解決し、現品票（調理）.rxz で PDF 生成。
    /// </summary>
    [HttpPost("pdf-from-sales-order-lines")]
    public async Task<IActionResult> GeneratePdfFromSalesOrderLines(
        [FromBody] ProductLabelFromSalesOrderLinesRequestDto? body,
        CancellationToken ct)
    {
        if (body?.SalesOrderLineIds == null || body.SalesOrderLineIds.Count == 0)
            return BadRequest(new { detail = "受注明細を指定してください" });

        if (body.SalesOrderLineIds.Count > MaxPrintOrderIds)
            return BadRequest(new { detail = $"一度に印刷できる件数は{MaxPrintOrderIds}件までです" });

        var templatePath = Path.Combine(AppContentPaths.TemplatesDirectory(_env), ProductLabelPdfService.BaggingTemplateFileName);
        var fullPath = Path.GetFullPath(templatePath);
        if (!System.IO.File.Exists(fullPath))
            return NotFound(new { detail = "袋詰現品票1枚テンプレートが見つかりません" });

        try
        {
            var orderTableIds = await _searchService.GetOrderTableIdsBySalesOrderLineIdsAsync(body.SalesOrderLineIds, ct);
            if (orderTableIds.Count == 0)
                return BadRequest(new { detail = "現品票に紐づくオーダテーブルが見つかりません。ordertable が未登録の受注明細は印刷対象外です。" });

            var labelCount = Math.Max(1, body.LabelCount);
            var pdfBytes = await _pdfService.GeneratePdfAsync(fullPath, orderTableIds, labelCount, null, null, ct);
            if (pdfBytes.Length == 0)
                return BadRequest(new { detail = "該当するオーダが見つかりません" });

            return File(pdfBytes, "application/pdf", "袋詰現品票1枚.pdf");
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = $"PDF 出力エラー: {ex.Message}" });
        }
    }
}
