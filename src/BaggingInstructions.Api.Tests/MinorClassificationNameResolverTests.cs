using BaggingInstructions.Api.Entities;
using BaggingInstructions.Api.Services;

namespace BaggingInstructions.Api.Tests;

public class MinorClassificationNameResolverTests
{
    private static MinorClassification Row(string major, string middle, string minor, string name) =>
        new()
        {
            MajorClassificationCode = major,
            MiddleClassificationCode = middle,
            MinorClassificationCode = minor,
            MinorClassificationName = name
        };

    [Fact]
    public void Resolve_picks_name_matching_full_hierarchy()
    {
        var resolver = new MinorClassificationNameResolver(new[]
        {
            Row("1", "10", "100", "白米"),
            Row("2", "20", "100", "うどん")
        });

        Assert.Equal("白米", resolver.Resolve("1", "10", "100"));
        Assert.Equal("うどん", resolver.Resolve("2", "20", "100"));
    }

    [Fact]
    public void Resolve_returns_empty_when_minor_code_is_ambiguous_and_hierarchy_does_not_match()
    {
        var resolver = new MinorClassificationNameResolver(new[]
        {
            Row("1", "10", "100", "白米"),
            Row("2", "20", "100", "うどん")
        });

        // 品目側に上位分類が無い / 別階層 → 誤った名称を出さず空文字
        Assert.Equal("", resolver.Resolve("", "", "100"));
        Assert.Equal("", resolver.Resolve("3", "30", "100"));
    }

    [Fact]
    public void Resolve_falls_back_to_minor_code_when_name_is_unique()
    {
        var resolver = new MinorClassificationNameResolver(new[]
        {
            Row("1", "10", "100", "白米")
        });

        // 候補が一意なら上位分類が未設定でも従来どおり名称を出す
        Assert.Equal("白米", resolver.Resolve("", "", "100"));
        Assert.Equal("白米", resolver.Resolve("9", "90", "100"));
    }

    [Fact]
    public void Resolve_falls_back_when_duplicated_rows_share_the_same_name()
    {
        var resolver = new MinorClassificationNameResolver(new[]
        {
            Row("1", "10", "100", "白米"),
            Row("2", "20", "100", "白米")
        });

        Assert.Equal("白米", resolver.Resolve("", "", "100"));
    }

    [Fact]
    public void Resolve_trims_codes_and_names()
    {
        var resolver = new MinorClassificationNameResolver(new[]
        {
            Row(" 1 ", " 10 ", " 100 ", " 白米 ")
        });

        Assert.Equal("白米", resolver.Resolve("1", "10", "100"));
    }

    [Fact]
    public void Resolve_returns_empty_for_blank_minor_code()
    {
        var resolver = new MinorClassificationNameResolver(new[] { Row("1", "10", "100", "白米") });

        Assert.Equal("", resolver.Resolve("1", "10", ""));
        Assert.Equal("", resolver.Resolve("1", "10", null));
    }

    [Fact]
    public void Resolve_from_item_uses_item_classification_codes()
    {
        var resolver = new MinorClassificationNameResolver(new[]
        {
            Row("1", "10", "100", "白米"),
            Row("2", "20", "100", "うどん")
        });

        var item = new Item
        {
            MajorClassificationCode = "2",
            MiddleClassificationCode = "20",
            MinorClassificationCode = "100"
        };

        Assert.Equal("うどん", resolver.Resolve(item));
        Assert.Equal("", resolver.Resolve((Item?)null));
    }

    [Fact]
    public void Empty_resolver_returns_empty()
    {
        Assert.Equal("", MinorClassificationNameResolver.Empty.Resolve("1", "10", "100"));
    }
}
