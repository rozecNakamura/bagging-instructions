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
        [FromQuery(Name = "slot_code")] string[]? slotCode,
        CancellationToken ct)
    {
        try
        {
            var data = await _service.SearchAsync(delvedt, slotCode, ct);
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
        [FromQuery(Name = "slot_code")] string[]? slotCode,
        CancellationToken ct)
    {
        try
        {
            var data = await _service.SearchAsync(delvedt, slotCode, ct);
            var bytes = _excelService.BuildWorkbook(data, delvedt);
            var fileName = $"5_予定食数_{delvedt}.xlsx";
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
