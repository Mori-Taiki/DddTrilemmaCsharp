namespace Demo.Tests;

public sealed class 閲覧ドメインテスト
{
    [Theory]
    [ClassData(typeof(閲覧仕様一覧))]
    public void 共通の仕様どおりに事実を要求して判定する(閲覧仕様 仕様)
    {
        var actual = 閲覧の実行.観測する(仕様.入力);

        Assert.Equal(仕様.期待.結果, actual.結果);
        Assert.Equal<取得する事実>(仕様.期待.取得順序, actual.取得順序);
    }
}
