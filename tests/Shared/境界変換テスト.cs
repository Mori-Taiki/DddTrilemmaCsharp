using Demo.Api;

namespace Demo.Tests;

public sealed class 境界変換テスト
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("99")]
    [InlineData("未定義")]
    public void 未定義の文字列はドメイン型に変換しない(string? value)
    {
        Assert.Null(境界変換.公開範囲に変換する(value));
        Assert.Null(境界変換.料金区分に変換する(value));
    }
}
