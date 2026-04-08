using BaggingInstructions.Api.DTOs;
using BaggingInstructions.Api.Services;

namespace BaggingInstructions.Api.Tests;

public class BaggingInputPayloadValidatorTests
{
    [Fact]
    public void EmptyBom_skips_validation()
    {
        var payload = new BaggingInputPayloadDto
        {
            Lines = new List<BaggingInputLineDto> { new() { Citemcd = "ANY", TotalQty = 1 } }
        };
        var ex = Record.Exception(() => BaggingInputPayloadValidator.ValidateLinesAgainstBom(Array.Empty<string>(), payload));
        Assert.Null(ex);
    }

    [Fact]
    public void Lines_must_match_bom_when_bom_exists()
    {
        var payload = new BaggingInputPayloadDto
        {
            Lines = new List<BaggingInputLineDto> { new() { Citemcd = "A", TotalQty = 1 } }
        };
        BaggingInputPayloadValidator.ValidateLinesAgainstBom(new[] { "A", "B" }, payload);
    }

    [Fact]
    public void Unknown_child_code_throws()
    {
        var payload = new BaggingInputPayloadDto
        {
            Lines = new List<BaggingInputLineDto> { new() { Citemcd = "X", TotalQty = 1 } }
        };
        Assert.Throws<ArgumentException>(() =>
            BaggingInputPayloadValidator.ValidateLinesAgainstBom(new[] { "A" }, payload));
    }

    [Fact]
    public void Empty_citemcd_throws_when_bom_exists()
    {
        var payload = new BaggingInputPayloadDto
        {
            Lines = new List<BaggingInputLineDto> { new() { Citemcd = "  ", TotalQty = 1 } }
        };
        Assert.Throws<ArgumentException>(() =>
            BaggingInputPayloadValidator.ValidateLinesAgainstBom(new[] { "A" }, payload));
    }

    [Fact]
    public void Case_insensitive_match()
    {
        var payload = new BaggingInputPayloadDto
        {
            Lines = new List<BaggingInputLineDto> { new() { Citemcd = "abc", TotalQty = 1 } }
        };
        BaggingInputPayloadValidator.ValidateLinesAgainstBom(new[] { "ABC" }, payload);
    }
}
