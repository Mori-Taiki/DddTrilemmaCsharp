using Demo.Api;

namespace Demo.Tests;

public sealed class 境界値変換テスト
{
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
