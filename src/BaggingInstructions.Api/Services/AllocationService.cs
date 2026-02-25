namespace BaggingInstructions.Api.Services;

public static class AllocationService
{
    /// <summary>規格袋数を計算</summary>
    public static int CalculateStandardBags(decimal quantity, decimal kikunip)
    {
        if (kikunip <= 0) return 0;
        return (int)(quantity / kikunip);
    }

    /// <summary>端数量を計算</summary>
    public static decimal CalculateIrregularQuantity(decimal quantity, decimal kikunip)
    {
        if (kikunip <= 0) return quantity;
        return quantity % kikunip;
    }
}
