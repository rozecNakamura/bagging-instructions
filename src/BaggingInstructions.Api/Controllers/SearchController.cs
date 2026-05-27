using Microsoft.AspNetCore.Mvc;
using BaggingInstructions.Api.Services;
using BaggingInstructions.Api.DTOs;

namespace BaggingInstructions.Api.Controllers;

[ApiController]
[Route("api")]
public class SearchController : ControllerBase
{
    private readonly SearchService _searchService;
    private readonly BaggingSearchExcelService _baggingExcel;

    public SearchController(SearchService searchService, BaggingSearchExcelService baggingExcel)
    {
        _searchService = searchService;
        _baggingExcel = baggingExcel;
    }

    /// <summary>受注明細を検索（製造日・品目コード）。itemcd は部分一致。</summary>
    [HttpGet("search")]
    public async Task<ActionResult<SearchResponseDto>> Search(
        [FromQuery] string prddt,
        [FromQuery] string? itemcd,
        CancellationToken ct)
    {
        try
        {
            var items = await _searchService.SearchAsync(prddt, itemcd, ct);
            return Ok(new SearchResponseDto { Total = items.Count, Items = items });
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

    /// <summary>袋詰用：製造日・品目で受注明細を合算したグループ一覧。is_complete=true/false で完了フィルター。</summary>
    [HttpGet("search/bagging")]
    public async Task<ActionResult<BaggingSearchResponseDto>> SearchBagging(
        [FromQuery] string prddt,
        [FromQuery] string? itemcd,
        [FromQuery(Name = "is_complete")] string? isComplete,
        CancellationToken ct)
    {
        bool? isCompleteFilter = isComplete switch { "true" => true, "false" => false, _ => null };
        try
        {
            var groups = await _searchService.SearchBaggingGroupedAsync(prddt, itemcd, isCompleteFilter, ct);
            return Ok(new BaggingSearchResponseDto { Total = groups.Count, Groups = groups });
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

    /// <summary>袋詰管理画面の検索結果を Excel 出力（検索と同じフィルター条件）。</summary>
    [HttpGet("search/bagging/export")]
    public async Task<IActionResult> ExportBaggingExcel(
        [FromQuery] string prddt,
        [FromQuery] string? itemcd,
        [FromQuery(Name = "is_complete")] string? isComplete,
        CancellationToken ct)
    {
        bool? isCompleteFilter = isComplete switch { "true" => true, "false" => false, _ => null };
        try
        {
            var groups = await _searchService.SearchBaggingGroupedAsync(prddt, itemcd, isCompleteFilter, ct);
            var bytes = _baggingExcel.Build(groups, prddt);
            var fileName = $"袋詰管理_{prddt}.xlsx";
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = $"Excel出力エラー: {ex.Message}" });
        }
    }

    /// <summary>汁仕分表用：喫食日・品目コードで検索。item の中分類コードが 50 または 51 のみ。喫食日・喫食時間・品目でグループ化して返す。</summary>
    [HttpGet("search/juice")]
    public async Task<ActionResult<JuiceSearchGroupResponseDto>> SearchJuice(
        [FromQuery] string delvedt,
        [FromQuery] string? itemcd,
        [FromQuery(Name = "meal_time")] string? mealTime,
        CancellationToken ct)
    {
        try
        {
            var groups = await _searchService.SearchByDeliveryDateGroupedAsync(delvedt, itemcd, mealTime, ct);
            return Ok(new JuiceSearchGroupResponseDto { Total = groups.Count, Groups = groups });
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

    /// <summary>弁当箱盛り付け指示書用：喫食日・品目コードで検索。bentoType=okazu|gohan。</summary>
    [HttpGet("search/bento")]
    public async Task<ActionResult<BentoSearchGroupResponseDto>> SearchBento(
        [FromQuery] string delvedt,
        [FromQuery] string? itemcd,
        [FromQuery(Name = "bento_type")] string? bentoType,
        CancellationToken ct)
    {
        try
        {
            var groups = await _searchService.SearchByDeliveryDateForBentoGroupedAsync(delvedt, itemcd, bentoType, ct);
            return Ok(new BentoSearchGroupResponseDto { Total = groups.Count, Groups = groups });
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

    /// <summary>ご飯盛り付け指示書用：喫食日・品目コードで検索。喫食日・喫食時間・品目でグループ化して返す。</summary>
    [HttpGet("search/gohan")]
    public async Task<ActionResult<BentoSearchGroupResponseDto>> SearchGohan(
        [FromQuery] string delvedt,
        [FromQuery] string? itemcd,
        [FromQuery(Name = "addinfo08_type")] string? addinfo08Type,
        CancellationToken ct)
    {
        try
        {
            var groups = await _searchService.SearchByDeliveryDateForGohanGroupedAsync(delvedt, itemcd, addinfo08Type, ct);
            return Ok(new BentoSearchGroupResponseDto { Total = groups.Count, Groups = groups });
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

    /// <summary>受注明細を検索（全リレーションデータ含む）</summary>
    [HttpPost("search/detail")]
    public async Task<ActionResult<SearchDetailResponseDto>> SearchDetail(
        [FromBody] DetailRequest body,
        CancellationToken ct)
    {
        if (body.Prkeys == null || body.Prkeys.Count == 0)
            return BadRequest(new { detail = "プライマリキーを指定してください" });

        try
        {
            var jobords = await _searchService.SearchDetailByPrkeysAsync(body.Prkeys, ct);
            var items = jobords.Select(EntityToDtoMapper.ToJobordDetailItemDto).ToList();
            return Ok(new SearchDetailResponseDto { Total = items.Count, Items = items });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = $"検索エラー: {ex.Message}" });
        }
    }

    public class DetailRequest
    {
        [System.Text.Json.Serialization.JsonPropertyName("prkeys")]
        public List<long> Prkeys { get; set; } = new();
    }
}
