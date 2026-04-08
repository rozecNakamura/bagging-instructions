using Microsoft.AspNetCore.Mvc;
using BaggingInstructions.Api.Services;
using BaggingInstructions.Api.DTOs;

namespace BaggingInstructions.Api.Controllers;

[ApiController]
[Route("api/bagging")]
public class BaggingController : ControllerBase
{
    private readonly BaggingCalculatorService _baggingCalculator;
    private readonly BaggingInputService _baggingInputService;

    public BaggingController(BaggingCalculatorService baggingCalculator, BaggingInputService baggingInputService)
    {
        _baggingCalculator = baggingCalculator;
        _baggingInputService = baggingInputService;
    }

    /// <summary>登録済み投入量を取得。</summary>
    [HttpGet("input")]
    public async Task<ActionResult<BaggingInputGetResponseDto>> GetInput(
        [FromQuery] string prddt,
        [FromQuery] string itemcd,
        [FromQuery(Name = "jobord_prkeys")] List<long>? jobordPrkeys,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(prddt) || string.IsNullOrWhiteSpace(itemcd))
            return BadRequest(new { detail = "prddt と itemcd を指定してください。" });
        var r = await _baggingInputService.GetAsync(prddt.Trim(), itemcd.Trim(), jobordPrkeys, ct);
        return r == null ? BadRequest(new { detail = "製造日の形式が不正です。" }) : Ok(r);
    }

    /// <summary>外部（中間品完成量等）から投入行を取り込み、<see cref="SaveInput"/> と同様に保存する。</summary>
    [HttpPost("input/import")]
    public async Task<IActionResult> ImportInput([FromBody] BaggingInputSaveRequestDto body, CancellationToken ct)
    {
        if (body.Payload?.Lines == null || body.Payload.Lines.Count == 0)
            return BadRequest(new { detail = "payload.lines に1行以上指定してください。" });
        try
        {
            await _baggingInputService.SaveAsync(body, ct);
            return Ok(new { ok = true, imported_lines = body.Payload.Lines.Count });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
    }

    /// <summary>投入量を登録（UPSERT）。</summary>
    [HttpPut("input")]
    public async Task<IActionResult> SaveInput([FromBody] BaggingInputSaveRequestDto body, CancellationToken ct)
    {
        try
        {
            await _baggingInputService.SaveAsync(body, ct);
            return Ok(new { ok = true });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
    }

    /// <summary>必要量セット用：受注合計に対する BOM 既定総数量。</summary>
    [HttpPost("required-quantities")]
    public async Task<ActionResult<BaggingRequiredQuantitiesResponseDto>> RequiredQuantities(
        [FromBody] BaggingRequiredQuantitiesRequestDto body,
        CancellationToken ct)
    {
        if (body.JobordPrkeys == null || body.JobordPrkeys.Count == 0)
            return BadRequest(new { detail = "jobord_prkeys を指定してください。" });
        var r = await _baggingCalculator.GetRequiredQuantitiesAsync(body.JobordPrkeys, ct);
        return Ok(r);
    }

    /// <summary>袋詰指示書またはラベルデータを計算。ラベルは殺菌温度・時間（steritime 秒）を含む。</summary>
    [HttpPost("calculate")]
    public async Task<IActionResult> Calculate(
        [FromBody] CalculateRequestDto request,
        CancellationToken ct)
    {
        try
        {
            var calc = await _baggingCalculator.CalculateFullAsync(request, ct);
            var baggingItems = calc.Items;

            if (request.PrintType == "instruction")
                return Ok(new BaggingInstructionResponseDto
                {
                    Items = baggingItems,
                    IngredientDisplayRows = calc.IngredientDisplayRows
                });

            var labelItems = new List<LabelItemDto>();
            foreach (var item in baggingItems)
            {
                var fillQty = BaggingDivisorResolver.ResolveFromItemDetail(item.Item);
                var st = item.Item?.Strtemp ?? item.Item?.Steritemprange;
                var steri = item.Item?.Steritime;
                labelItems.AddRange(LabelGeneratorService.GenerateStandardLabelsFromDto(
                    item.Itemcd, item.Itemnm, st, steri, item.Item?.Kikunip, item.Delvedt, item.Shptm, item.StandardBags, fillQty));
                labelItems.AddRange(LabelGeneratorService.GenerateIrregularLabelsFromDto(
                    item.Itemcd, item.Itemnm, st, steri, item.Delvedt, item.Shptm, item.Shpctrnm, item.IrregularQuantity));
            }
            return Ok(new LabelResponseDto { Items = labelItems });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = $"計算エラー: {ex.Message}" });
        }
    }
}
