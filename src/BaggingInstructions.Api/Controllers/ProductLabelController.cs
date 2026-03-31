using Microsoft.AspNetCore.Mvc;
using BaggingInstructions.Api.DTOs;
using BaggingInstructions.Api.Services;

namespace BaggingInstructions.Api.Controllers;

[ApiController]
[Route("api/product-label")]
public class ProductLabelController : ControllerBase
{
    private readonly SearchService _searchService;

    public ProductLabelController(SearchService searchService)
    {
        _searchService = searchService;
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

    /// <summary>現品票：ordertable を納期で検索（納期・品目で数量合算済み）。大分類省略時は絞り込みなし。</summary>
    [HttpGet("search")]
    public async Task<ActionResult<ProductLabelSearchResponseDto>> Search(
        [FromQuery] string needdate,
        [FromQuery] long? majorclassificationid,
        CancellationToken ct)
    {
        try
        {
            var rows = await _searchService.SearchProductLabelAsync(needdate, majorclassificationid, ct);
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
}
