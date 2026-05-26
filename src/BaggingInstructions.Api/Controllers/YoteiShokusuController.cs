using Microsoft.AspNetCore.Mvc;
using BaggingInstructions.Api.DTOs;
using BaggingInstructions.Api.Services;

namespace BaggingInstructions.Api.Controllers;

[ApiController]
[Route("api/yotei-shokusu")]
public class YoteiShokusuController : ControllerBase
{
    private readonly YoteiShokusuService _service;
    private readonly YoteiShokusuExcelService _excelService;

    public YoteiShokusuController(YoteiShokusuService service, YoteiShokusuExcelService excelService)
    {
        _service = service;
        _excelService = excelService;
    }

    [HttpGet("search")]
    public async Task<ActionResult<YoteiShokusuResponseDto>> Search(
        [FromQuery(Name = "delvedt")] string delvedt,
        [FromQuery(Name = "meal_time")] string? mealTime,
        [FromQuery(Name = "customer_group")] string[]? customerGroup,
        CancellationToken ct)
    {
        try
        {
            var data = await _service.SearchAsync(delvedt, mealTime, customerGroup, ct);
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

    [HttpGet("export")]
    public async Task<IActionResult> Export(
        [FromQuery(Name = "delvedt")] string delvedt,
        [FromQuery(Name = "meal_time")] string? mealTime,
        [FromQuery(Name = "customer_group")] string[]? customerGroup,
        CancellationToken ct)
    {
        try
        {
            var data = await _service.SearchAsync(delvedt, mealTime, customerGroup, ct);
            var mealTimeLabel = mealTime switch { "1" => "朝", "2" => "昼", "3" => "夕", _ => "" };
            var bytes = _excelService.BuildWorkbook(data, delvedt, mealTimeLabel);
            var suffix = string.IsNullOrEmpty(mealTimeLabel) ? "" : $"_{mealTimeLabel}";
            var fileName = $"5_予定食数_{delvedt}{suffix}.xlsx";
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = $"Excel 出力エラー: {ex.Message}" });
        }
    }
}
