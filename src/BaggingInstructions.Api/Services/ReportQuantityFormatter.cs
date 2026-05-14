using System.Globalization;

namespace BaggingInstructions.Api.Services;

public static class ReportQuantityFormatter
{
    public static string FormatCeilingQuantity(decimal quantity) =>
        Math.Ceiling(quantity).ToString("0", CultureInfo.InvariantCulture);
}
