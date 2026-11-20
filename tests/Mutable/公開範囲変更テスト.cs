using Demo.Domain;

namespace Demo.Tests;

public sealed class 公開範囲変更テスト
{
    [Fact]
    public void 作者の変更は元のストーリー自身の公開範囲を変える()
    {
        var story = new ストーリー(
            new ストーリーID("story"),
            new 利用者ID("author"),
            new 公開範囲.全体公開(),
            new 料金区分.有料()
        );

        var result = story.公開範囲を変える(new 公開範囲.フォロワー限定(), new 利用者ID("author"));

        Assert.IsType<変更できた>(result);
        Assert.Equal(new 公開範囲.フォロワー限定(), story.公開範囲);
        Assert.Equal(new ストーリーID("story"), story.ID);
        Assert.Equal(new 利用者ID("author"), story.作者);
        Assert.Equal(new 料金区分.有料(), story.料金);
    }

    [Fact]
    public void 作者以外の変更は拒否され公開範囲は変わらない()
    {
        var story = new ストーリー(
            new ストーリーID("story"),
            new 利用者ID("author"),
            new 公開範囲.全体公開(),
            new 料金区分.無料()
        );

        var result = story.公開範囲を変える(new 公開範囲.フォロワー限定(), new 利用者ID("reader"));

        Assert.Equal(new 変更拒否(変更拒否理由.作者ではない), result);
        Assert.Equal(new 公開範囲.全体公開(), story.公開範囲);
    }

    [Fact]
    public void 同じ変更をもう一度行うと既に変更済みとして拒否される()
    {
        var story = new ストーリー(
            new ストーリーID("story"),
            new 利用者ID("author"),
            new 公開範囲.全体公開(),
            new 料金区分.無料()
        );
        story.公開範囲を変える(new 公開範囲.フォロワー限定(), new 利用者ID("author"));

        var result = story.公開範囲を変える(new 公開範囲.フォロワー限定(), new 利用者ID("author"));

        Assert.Equal(new 変更拒否(変更拒否理由.既にその公開範囲), result);
        Assert.Equal(new 公開範囲.フォロワー限定(), story.公開範囲);
    }

    [Fact]
    public void nullを渡した変更では現在の状態を壊さない()
    {
        var story = new ストーリー(
            new ストーリーID("story"),
            new 利用者ID("author"),
            new 公開範囲.全体公開(),
            new 料金区分.無料()
        );

        Assert.Throws<ArgumentNullException>(() =>
            story.公開範囲を変える(null!, new 利用者ID("author"))
        );
        Assert.Throws<ArgumentNullException>(() =>
            story.公開範囲を変える(new 公開範囲.フォロワー限定(), null!)
        );

        Assert.Equal(new 公開範囲.全体公開(), story.公開範囲);
    }
}
