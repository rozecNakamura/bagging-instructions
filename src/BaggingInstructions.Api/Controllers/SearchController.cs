using Microsoft.AspNetCore.Mvc;
using BaggingInstructions.Api.Services;
using BaggingInstructions.Api.DTOs;

namespace BaggingInstructions.Api.Controllers;

[ApiController]
[Route("api")]
public class SearchController : ControllerBase
{
    private readonly SearchService _searchService;

    public SearchController(SearchService searchService)
    {
        _searchService = searchService;
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
