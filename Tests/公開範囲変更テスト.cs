using Demo.Domain;

namespace Demo.Tests;

public sealed class 公開範囲変更テスト
{
    [Fact]
    public void 作者の変更は新しい状態を返し元のストーリーを変更しない()
    {
        var story = ストーリー.投稿する(
            new ストーリーID("story"),
            new 利用者ID("author"),
            new 公開範囲.全体公開(),
            new 料金区分.無料()
        );

        var result = story.公開範囲を変える(new 公開範囲.フォロワー限定(), new 利用者ID("author"));

        var changed = Assert.IsType<変更できた>(result);
        Assert.Equal(new 公開範囲.フォロワー限定(), changed.変更後.公開範囲);
        Assert.Equal(new 公開範囲.全体公開(), story.公開範囲);
        Assert.Equal(story.ID, changed.変更後.ID);
        Assert.Equal(story.作者, changed.変更後.作者);
        Assert.Equal(story.料金, changed.変更後.料金);
        Assert.Equal(
            result,
            story.公開範囲を変える(new 公開範囲.フォロワー限定(), new 利用者ID("author"))
        );
    }

    [Fact]
    public void 作者以外の変更は拒否され公開範囲は変わらない()
    {
        var story = ストーリー.投稿する(
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
    public void 同じ公開範囲への変更は拒否され状態は変わらない()
    {
        var story = ストーリー.投稿する(
            new ストーリーID("story"),
            new 利用者ID("author"),
            new 公開範囲.フォロワー限定(),
            new 料金区分.有料()
        );

        var result = story.公開範囲を変える(new 公開範囲.フォロワー限定(), new 利用者ID("author"));

        Assert.Equal(new 変更拒否(変更拒否理由.既にその公開範囲), result);
        Assert.Equal(new 公開範囲.フォロワー限定(), story.公開範囲);
    }
}
