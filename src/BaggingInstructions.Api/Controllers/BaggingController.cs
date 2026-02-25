using Microsoft.AspNetCore.Mvc;
using BaggingInstructions.Api.Services;
using BaggingInstructions.Api.DTOs;

namespace BaggingInstructions.Api.Controllers;

[ApiController]
[Route("api/bagging")]
public class BaggingController : ControllerBase
{
    private readonly BaggingCalculatorService _baggingCalculator;

    public BaggingController(BaggingCalculatorService baggingCalculator)
    {
        _baggingCalculator = baggingCalculator;
    }

    /// <summary>袋詰指示書またはラベルデータを計算。print_type=label は現行のまま未完成。</summary>
    [HttpPost("calculate")]
    public async Task<IActionResult> Calculate(
        [FromBody] CalculateRequestDto request,
        CancellationToken ct)
    {
        try
        {
            var baggingItems = await _baggingCalculator.CalculateAsync(request.JobordPrkeys, ct);

            if (request.PrintType == "instruction")
                return Ok(new BaggingInstructionResponseDto { Items = baggingItems });

            // ラベル出力: 現行のまま（DTO の値でラベル生成）
            var labelItems = new List<LabelItemDto>();
            foreach (var item in baggingItems)
            {
                labelItems.AddRange(LabelGeneratorService.GenerateStandardLabelsFromDto(
                    item.Itemcd, item.Itemnm, item.Item?.Strtemp, item.Item?.Kikunip, item.Delvedt, item.Shptm, item.StandardBags));
                labelItems.AddRange(LabelGeneratorService.GenerateIrregularLabelsFromDto(
                    item.Itemcd, item.Itemnm, item.Item?.Strtemp, item.Delvedt, item.Shptm, item.Shpctrnm, item.IrregularQuantity));
            }
            return Ok(new LabelResponseDto { Items = labelItems });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { detail = $"計算エラー: {ex.Message}" });
        }
    }
}
