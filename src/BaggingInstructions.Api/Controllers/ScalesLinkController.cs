using BaggingInstructions.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace BaggingInstructions.Api.Controllers;

[ApiController]
[Route("api/scales-link")]
public class ScalesLinkController : ControllerBase
{
    private readonly ScalesLinkService _service;

    public ScalesLinkController(ScalesLinkService service)
    {
        _service = service;
    }

    [HttpGet("master/item")]
    public async Task<IActionResult> DownloadItemMaster(CancellationToken ct)
    {
        try
        {
            var bytes = await _service.BuildItemCsvAsync(ct);
            return File(bytes, "text/csv; charset=utf-8", "ITEM.csv");
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = $"CSV 出力エラー: {ex.Message}" });
        }
    }

    [HttpGet("master/mbom")]
    public async Task<IActionResult> DownloadMbomMaster(CancellationToken ct)
    {
        try
        {
            var bytes = await _service.BuildMbomCsvAsync(ct);
            return File(bytes, "text/csv; charset=utf-8", "MBOM.csv");
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = $"CSV 出力エラー: {ex.Message}" });
        }
    }

    [HttpGet("orders")]
    public async Task<IActionResult> SearchOrders(
        [FromQuery(Name = "releaseDateFrom")] DateOnly? releaseDateFrom,
        [FromQuery(Name = "releaseDateTo")] DateOnly? releaseDateTo,
        CancellationToken ct)
    {
        try
        {
            var data = await _service.SearchOrdersAsync(releaseDateFrom, releaseDateTo, ct);
            return Ok(data);
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

    [HttpGet("orders/export")]
    public async Task<IActionResult> ExportOrders(
        [FromQuery(Name = "releaseDateFrom")] DateOnly? releaseDateFrom,
        [FromQuery(Name = "releaseDateTo")] DateOnly? releaseDateTo,
        CancellationToken ct)
    {
        try
        {
            var bytes = await _service.BuildOrderCsvAsync(releaseDateFrom, releaseDateTo, ct);
            return File(bytes, "text/csv; charset=utf-8", "ORDER.csv");
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
