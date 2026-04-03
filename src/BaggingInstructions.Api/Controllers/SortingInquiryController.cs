using Microsoft.AspNetCore.Mvc;
using BaggingInstructions.Api.DTOs;
using BaggingInstructions.Api.Services;

namespace BaggingInstructions.Api.Controllers;

[ApiController]
[Route("api/sorting-inquiry")]
public class SortingInquiryController : ControllerBase
{
    private readonly SortingInquiryService _service;
    private readonly SortingInquiryExcelService _excelService;

    public SortingInquiryController(SortingInquiryService service, SortingInquiryExcelService excelService)
    {
        _service = service;
        _excelService = excelService;
    }

    [HttpGet("slots")]
    public async Task<ActionResult<List<ProductionInstructionSlotOptionDto>>> ListSlots(CancellationToken ct)
    {
        try
        {
            var list = await _service.ListSlotsAsync(ct);
            return Ok(list);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = $"マスタ取得エラー: {ex.Message}" });
        }
    }

    [HttpGet("search")]
    public async Task<ActionResult<SortingInquirySearchResponseDto>> Search(
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

    [HttpGet("export/shiwake-inquiry")]
    public async Task<IActionResult> ExportShiwakeInquiry(
        [FromQuery(Name = "delvedt")] string delvedt,
        [FromQuery(Name = "slot_code")] string[]? slotCode,
        CancellationToken ct)
    {
        try
        {
            var data = await _service.SearchAsync(delvedt, slotCode, ct);
            var bytes = _excelService.BuildShiwakeInquiryWorkbook(data, delvedt);
            var fileName = $"2_仕分け照会_{delvedt}.xlsx";
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

    [HttpGet("export/journal-adjustment")]
    public async Task<IActionResult> ExportJournalAdjustment(
        [FromQuery(Name = "delvedt")] string delvedt,
        [FromQuery(Name = "slot_code")] string[]? slotCode,
        CancellationToken ct)
    {
        try
        {
            var data = await _service.SearchAsync(delvedt, slotCode, ct);
            var bytes = _excelService.BuildJournalAdjustmentWorkbook(data, delvedt);
            var fileName = $"仕訳表自動調整_{delvedt}.xlsx";
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
