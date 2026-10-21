using Microsoft.AspNetCore.Mvc;
using BaggingInstructions.Api.DTOs;
using BaggingInstructions.Api.Services;

namespace BaggingInstructions.Api.Controllers;

/// <summary>
/// 作業前準備書（計画オーダー）。作業区・倉庫・分類マスタは <c>api/preparation-work</c> の各エンドポイントを共用する。
/// </summary>
[ApiController]
[Route("api/planning-preparation-work")]
public class PlanningPreparationWorkController : ControllerBase
{
    private readonly PlanningPreparationWorkService _service;

    public PlanningPreparationWorkController(PlanningPreparationWorkService service)
    {
        _service = service;
    }

    [HttpGet("manufacturing-routes")]
    public async Task<ActionResult<List<PreparationWorkManufacturingRouteOptionDto>>> ListManufacturingRoutes(
        [FromQuery] string delvedt,
        CancellationToken ct)
    {
        try
        {
            var list = await _service.ListManufacturingRoutesForNeedDateAsync(delvedt, ct);
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

    /// <summary>納期・製造便・作業区・倉庫・品目・大分類・中分類で計画オーダーを集約（日付×大分類×中分類ごとの件数）。</summary>
    [HttpGet("search")]
    public async Task<ActionResult<PreparationWorkSearchResponseDto>> Search(
        [FromQuery] string delvedt,
        [FromQuery(Name = "manufacturing_route_code")] string[]? manufacturingRouteCode,
        [FromQuery] string? itemcd,
        [FromQuery(Name = "majorclassificationid")] long[]? majorclassificationid,
        [FromQuery] long? middleclassificationid,
        [FromQuery(Name = "workcenter_id")] long[]? workcenterId,
        [FromQuery(Name = "warehouse_id")] long[]? warehouseId,
        CancellationToken ct)
    {
        try
        {
            var groups = await _service.SearchGroupsAsync(
                delvedt,
                manufacturingRouteCode ?? Array.Empty<string>(),
                itemcd,
                majorclassificationid,
                middleclassificationid,
                workcenterId ?? Array.Empty<long>(),
                warehouseId ?? Array.Empty<long>(),
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
            var ids = await _service.ResolvePlanningOrderIdsAsync(body, body.GroupKeys, ct);
            if (ids.Count == 0)
                return BadRequest(new { detail = "該当する計画オーダーがありません" });

            var rows = await _service.BuildCsvRowsAsync(ids, ct);
            var bytes = PlanningPreparationWorkService.WriteCsvUtf8Bom(rows);
            return File(bytes, "text/csv; charset=utf-8", "作業前準備書（計画オーダー）.csv");
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
}
