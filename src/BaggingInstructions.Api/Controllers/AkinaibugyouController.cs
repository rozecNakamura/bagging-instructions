using Microsoft.AspNetCore.Mvc;
using BaggingInstructions.Api.Services;

namespace BaggingInstructions.Api.Controllers;

[ApiController]
[Route("api/cstmeat")]
public class AkinaibugyouController : ControllerBase
{
    private readonly AkinaibugyouService _service;

    public AkinaibugyouController(AkinaibugyouService service)
    {
        _service = service;
    }

    /// <summary>cstmeat を範囲検索して件数を返す。</summary>
    [HttpGet("search")]
    public async Task<ActionResult<object>> Search(
        [FromQuery(Name = "slip_type")] string? slipType,
        [FromQuery(Name = "date_from")] string dateFrom,
        [FromQuery(Name = "time_from")] string? timeFrom,
        [FromQuery(Name = "date_to")]   string dateTo,
        [FromQuery(Name = "time_to")]   string? timeTo,
        [FromQuery(Name = "customer")]  string? customer,
        [FromQuery(Name = "store")]     string? store,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dateFrom) || dateFrom.Length != 8)
            return BadRequest(new { detail = "date_from はYYYYMMDD形式（8桁）で指定してください。" });
        if (string.IsNullOrWhiteSpace(dateTo) || dateTo.Length != 8)
            return BadRequest(new { detail = "date_to はYYYYMMDD形式（8桁）で指定してください。" });

        try
        {
            var filter = new AkinaibugyouFilter(slipType ?? "", dateFrom, timeFrom ?? "1", dateTo, timeTo ?? "3", customer, store);
            var count = await _service.CountAsync(filter, ct);
            return Ok(new { count });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = $"検索エラー: {ex.Message}" });
        }
    }

    /// <summary>cstmeat を範囲検索してテキストファイルとしてダウンロードする。</summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export(
        [FromQuery(Name = "slip_type")] string? slipType,
        [FromQuery(Name = "date_from")] string dateFrom,
        [FromQuery(Name = "time_from")] string? timeFrom,
        [FromQuery(Name = "date_to")]   string dateTo,
        [FromQuery(Name = "time_to")]   string? timeTo,
        [FromQuery(Name = "customer")]  string? customer,
        [FromQuery(Name = "store")]     string? store,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dateFrom) || dateFrom.Length != 8)
            return BadRequest(new { detail = "date_from はYYYYMMDD形式（8桁）で指定してください。" });
        if (string.IsNullOrWhiteSpace(dateTo) || dateTo.Length != 8)
            return BadRequest(new { detail = "date_to はYYYYMMDD形式（8桁）で指定してください。" });

        try
        {
            var filter = new AkinaibugyouFilter(slipType ?? "", dateFrom, timeFrom ?? "1", dateTo, timeTo ?? "3", customer, store);
            var bytes = await _service.BuildTextBytesAsync(filter, ct);
            var filename = $"商奉行出力_{dateFrom}_{dateTo}.txt";
            return File(bytes, "text/plain; charset=utf-8", filename);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = $"出力エラー: {ex.Message}" });
        }
    }
}
