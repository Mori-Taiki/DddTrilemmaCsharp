using Demo.Domain;

namespace Demo.Tests;

public sealed class 公開範囲変更テスト
{
    [Fact]
    public void 作者は公開範囲を変更できる()
    {
        var story = ストーリー.投稿する("story", "author", 公開範囲.全体公開, 料金区分.無料);

        var result = story.公開範囲を変える(公開範囲.フォロワー限定, "author");

        Assert.IsType<変更できた>(result);
        Assert.Equal(公開範囲.フォロワー限定, story.公開範囲);
    }

    [Fact]
    public void 作者以外の変更は拒否され公開範囲は変わらない()
    {
        var story = ストーリー.投稿する("story", "author", 公開範囲.全体公開, 料金区分.無料);

        var result = story.公開範囲を変える(公開範囲.フォロワー限定, "reader");

        Assert.Equal(new 変更拒否(変更拒否理由.作者ではない), result);
        Assert.Equal(公開範囲.全体公開, story.公開範囲);
    }

    [Fact]
    public void 同じ公開範囲への変更は拒否され状態は変わらない()
    {
        var story = ストーリー.投稿する("story", "author", 公開範囲.フォロワー限定, 料金区分.有料);

        var result = story.公開範囲を変える(公開範囲.フォロワー限定, "author");

        Assert.Equal(new 変更拒否(変更拒否理由.既にその公開範囲), result);
        Assert.Equal(公開範囲.フォロワー限定, story.公開範囲);
    }
}
