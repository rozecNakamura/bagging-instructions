using System.Globalization;

namespace BaggingInstructions.Api.Services;

/// <summary>
/// salesorderlineaddinfo.addinfo05 の値から袋詰指示書ヘッダー用の喫食時間ラベルを決める。
/// </summary>
public static class BaggingEatingTimeLabel
{
    /// <summary>
    /// addinfo05: 1=朝, 2=昼, 3=夜（01 や全角 １２３ も可）。それ以外・空は空文字。
    /// </summary>
    public static string MapFromAddinfo05(string? addinfo05)
    {
        var s = (addinfo05 ?? "").Trim();
        if (s.Length == 0)
            return "";

        s = s
            .Replace('\uFF11', '1')
            .Replace('\uFF12', '2')
            .Replace('\uFF13', '3');

        if (!int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) || n is < 1 or > 3)
            return "";

        return n switch
        {
            1 => "朝",
            2 => "昼",
            3 => "夜",
            _ => ""
        };
    }
}
