using Microsoft.AspNetCore.Mvc;
using BaggingInstructions.Api.Core;
using BaggingInstructions.Api.DTOs;
using BaggingInstructions.Api.Services;

namespace BaggingInstructions.Api.Controllers;

[ApiController]
[Route("api/preparation-work")]
public class PreparationWorkController : ControllerBase
{
    private readonly PreparationWorkService _preparationWorkService;
    private readonly PreparationWorkPdfService _preparationWorkPdfService;
    private readonly IWebHostEnvironment _env;

    public PreparationWorkController(
        PreparationWorkService preparationWorkService,
        PreparationWorkPdfService preparationWorkPdfService,
        IWebHostEnvironment env)
    {
        _preparationWorkService = preparationWorkService;
        _preparationWorkPdfService = preparationWorkPdfService;
        _env = env;
    }

    /// <summary>大分類に紐づく中分類マスタ（プルダウン用）。</summary>
    [HttpGet("middle-classifications")]
    public async Task<ActionResult<List<MiddleClassificationOptionDto>>> ListMiddleClassifications(
        [FromQuery] long majorclassificationid,
        CancellationToken ct)
    {
        if (majorclassificationid <= 0)
            return BadRequest(new { detail = "majorclassificationid を指定してください" });
        var list = await _preparationWorkService.ListMiddleClassificationsAsync(majorclassificationid, ct);
        return Ok(list);
    }

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

    [HttpGet("manufacturing-routes")]
    public async Task<ActionResult<List<PreparationWorkManufacturingRouteOptionDto>>> ListManufacturingRoutes(
        [FromQuery] string delvedt,
        CancellationToken ct)
    {
        try
        {
            var list = await _preparationWorkService.ListManufacturingRoutesForNeedDateAsync(delvedt, ct);
            return Ok(list);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = $"製造便一覧取得エラー: {ex.Message}" });
        }
    }

    /// <summary>納期・製造便・作業区・倉庫・品目・大分類・中分類で受注明細を集約（日付×大分類×中分類ごとの件数）。</summary>
    [HttpGet("search")]
    public async Task<ActionResult<PreparationWorkSearchResponseDto>> Search(
        [FromQuery] string delvedt,
        [FromQuery(Name = "manufacturing_route_code")] string[]? manufacturingRouteCode,
        [FromQuery] string? itemcd,
        [FromQuery] long? majorclassificationid,
        [FromQuery] long? middleclassificationid,
        [FromQuery(Name = "workcenter_id")] long[]? workcenterId,
        [FromQuery(Name = "warehouse_id")] long[]? warehouseId,
        CancellationToken ct)
    {
        try
        {
            var mfg = manufacturingRouteCode ?? Array.Empty<string>();
            var wc = workcenterId ?? Array.Empty<long>();
            var wh = warehouseId ?? Array.Empty<long>();
            var groups = await _preparationWorkService.SearchGroupsAsync(
                delvedt,
                mfg,
                itemcd,
                majorclassificationid,
                middleclassificationid,
                wc,
                wh,
                ct);
            return Ok(new PreparationWorkSearchResponseDto { Total = groups.Count, Groups = groups });
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

    /// <summary>選択グループの BOM 展開 CSV（UTF-8 BOM）。</summary>
    [HttpPost("csv")]
    public async Task<IActionResult> ExportCsv([FromBody] PreparationWorkExportRequestDto body, CancellationToken ct)
    {
        if (body?.GroupKeys == null || body.GroupKeys.Count == 0)
            return BadRequest(new { detail = "出力するグループを選択してください" });

        try
        {
            var lineIds = await _preparationWorkService.ResolveLineIdsAsync(body, body.GroupKeys, ct);
            if (lineIds.Count == 0)
                return BadRequest(new { detail = "該当する受注明細がありません" });

            var rows = await _preparationWorkService.BuildCsvRowsAsync(lineIds, ct);
            var bytes = PreparationWorkService.WriteCsvUtf8Bom(rows);
            return File(bytes, "text/csv; charset=utf-8", "作業前準備書.csv");
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = $"CSV 出力エラー: {ex.Message}" });
        }
    }

    /// <summary>選択グループの帳票 PDF（中分類別・rxz テンプレート）。</summary>
    [HttpPost("pdf")]
    public async Task<IActionResult> ExportPdf([FromBody] PreparationWorkExportRequestDto body, CancellationToken ct)
    {
        if (body?.GroupKeys == null || body.GroupKeys.Count == 0)
            return BadRequest(new { detail = "印刷するグループを選択してください" });

        var templatePath = Path.Combine(AppContentPaths.TemplatesDirectory(_env), "作業前準備書.rxz");
        var fullPath = Path.GetFullPath(templatePath);
        if (!System.IO.File.Exists(fullPath))
            return NotFound(new { detail = "作業前準備書テンプレートが見つかりません" });

        try
        {
            var lineIds = await _preparationWorkService.ResolveLineIdsAsync(body, body.GroupKeys, ct);
            if (lineIds.Count == 0)
                return BadRequest(new { detail = "該当する受注明細がありません" });

            var pdfLines = await _preparationWorkService.BuildPdfLineModelsAsync(lineIds, ct);
            var pdfBytes = _preparationWorkPdfService.GeneratePdf(fullPath, pdfLines);
            if (pdfBytes.Length == 0)
                return BadRequest(new { detail = "印刷する行がありません" });

            return File(pdfBytes, "application/pdf", "作業前準備書.pdf");
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
