using Demo.Api;
using Demo.Domain;

namespace Demo.Tests;

public sealed class ストーリー行のマッピングテスト
{
    [Theory]
    [InlineData("不明な公開範囲", "無料")]
    [InlineData("全体公開", "不明な料金区分")]
    public void 不正な保存値からはストーリーを復元できない(string visibility, string price)
    {
        var row = new ストーリー行
        {
            ID = "story",
            作者 = "author",
            公開範囲 = visibility,
            料金区分 = price,
        };

        Assert.Throws<InvalidOperationException>(() => row.ストーリーに復元する());
    }

    [Fact]
    public void 公開範囲を変更しただけではストーリー行は変わらず反映すると更新される()
    {
        var before = new ストーリー(
            new ストーリーID("story"),
            new 利用者ID("author"),
            new 公開範囲.全体公開(),
            new 料金区分.無料()
        );
        var row = new ストーリー行(before);

        var result = Assert.IsType<変更完了>(
            before.公開範囲を変える(new 公開範囲.フォロワー限定(), new 利用者ID("author"))
        );

        Assert.Equal("全体公開", row.公開範囲);
        row.反映する(result.変更後);
        Assert.Equal("フォロワー限定", row.公開範囲);
        Assert.Equal(result.変更後, row.ストーリーに復元する());
        Assert.IsType<公開範囲.全体公開>(before.公開範囲);
    }
}
