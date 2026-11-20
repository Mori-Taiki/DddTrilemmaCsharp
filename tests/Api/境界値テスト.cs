using Demo.Api;
using Demo.Domain;

namespace Demo.Tests;

public sealed class 境界値テスト
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("\t\n")]
    public void 空の識別子はドメインに入れない(string? value)
    {
        Assert.False(利用者ID.作成できる(value, out var userId));
        Assert.Null(userId);
        Assert.False(ストーリーID.作成できる(value, out var storyId));
        Assert.Null(storyId);
        Assert.ThrowsAny<ArgumentException>(() => new 利用者ID(value!));
        Assert.ThrowsAny<ArgumentException>(() => new ストーリーID(value!));
    }

    [Fact]
    public void 同じ利用者IDは値で比較され別種のIDとは等しくならない()
    {
        Assert.Equal(new 利用者ID("author"), new 利用者ID("author"));
        Assert.NotEqual(new 利用者ID("author"), new 利用者ID("reader"));
        Assert.False(new 利用者ID("same").Equals((object)new ストーリーID("same")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("99")]
    [InlineData("未定義")]
    public void 不明な選択肢を既存の業務ケースに読み替えない(string? value)
    {
        Assert.Null(境界値.公開範囲を読む(value));
        Assert.Null(境界値.料金を読む(value));
    }
}
