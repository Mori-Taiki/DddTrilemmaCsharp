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

    [Theory]
    [InlineData("不明な公開範囲", "無料")]
    [InlineData("全体公開", "不明な料金")]
    public void 不正な保存値からドメインを復元しない(string visibility, string price)
    {
        var row = new ストーリー行
        {
            ID = "story",
            作者 = "author",
            公開範囲 = visibility,
            料金 = price,
        };

        Assert.Throws<InvalidOperationException>(() => row.ドメインに戻す());
    }

    [Fact]
    public void ドメインで計算しただけでは追跡対象は変わらず明示的な適用で変わる()
    {
        var before = ストーリー.投稿する(
            new ストーリーID("story"),
            new 利用者ID("author"),
            new 公開範囲.全体公開(),
            new 料金区分.無料()
        );
        var row = ストーリー行.作成する(before);

        var result = Assert.IsType<変更できた>(
            before.公開範囲を変える(new 公開範囲.フォロワー限定(), new 利用者ID("author"))
        );

        Assert.Equal("全体公開", row.公開範囲);
        row.適用する(result.変更後);
        Assert.Equal("フォロワー限定", row.公開範囲);
        Assert.Equal(result.変更後, row.ドメインに戻す());
        Assert.IsType<公開範囲.全体公開>(before.公開範囲);
    }
}
