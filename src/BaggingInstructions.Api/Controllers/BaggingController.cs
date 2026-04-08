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

    /// <summary>
    /// 投入量を登録（UPSERT）。
    /// <c>jobord_prkeys</c> が1件以上ある場合は craftlineaxother の <c>baggedquantity</c> へ保存（袋詰画面の通常動作）。
    /// キーなしの場合のみ AppDb の JSON 登録へ保存する。
    /// </summary>
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

    /// <summary>袋詰指示書またはラベルデータを計算。ラベルは殺菌温度（strtemp）を含む。</summary>
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
                labelItems.AddRange(LabelGeneratorService.GenerateStandardLabelsFromDto(
                    item.Itemcd, item.Itemnm, st, item.Item?.Kikunip, item.Delvedt, item.Shptm, item.StandardBags, fillQty));
                labelItems.AddRange(LabelGeneratorService.GenerateIrregularLabelsFromDto(
                    item.Itemcd, item.Itemnm, st, item.Delvedt, item.Shptm, item.Shpctrnm, item.IrregularQuantity));
            }
            return Ok(new LabelResponseDto { Items = labelItems });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = $"計算エラー: {ex.Message}" });
        }
    }
}
