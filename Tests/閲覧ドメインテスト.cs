using Demo.Domain;

namespace Demo.Tests;

public sealed class 閲覧ドメインテスト
{
    [Theory]
    [InlineData(公開範囲.全体公開, 料金区分.無料)]
    [InlineData(公開範囲.全体公開, 料金区分.有料)]
    [InlineData(公開範囲.フォロワー限定, 料金区分.無料)]
    [InlineData(公開範囲.フォロワー限定, 料金区分.有料)]
    public void 作者本人は公開範囲と料金によらず閲覧できる(公開範囲 visibility, 料金区分 price)
    {
        var story = ストーリー.投稿する("story", "author", visibility, price);

        var result = 閲覧.開始(story, "author");

        Assert.IsType<許可>(result);
    }

    [Fact]
    public void 全体公開の無料作品は追加の事実なしで閲覧できる()
    {
        var story = ストーリー.投稿する("story", "author", 公開範囲.全体公開, 料金区分.無料);

        var result = 閲覧.開始(story, "reader");

        Assert.IsType<許可>(result);
    }

    [Fact]
    public void フォロワー限定作品は非フォロワーの閲覧を拒否する()
    {
        var story = ストーリー.投稿する("story", "author", 公開範囲.フォロワー限定, 料金区分.無料);
        var decision = Assert.IsType<フォロー確認が必要>(閲覧.開始(story, "reader"));

        var result = decision.結果(false);

        Assert.Equal(new 拒否(拒否理由.フォローが必要), result);
    }

    [Fact]
    public void 有料作品はVIP会員なら購入確認なしで閲覧できる()
    {
        var story = ストーリー.投稿する("story", "author", 公開範囲.全体公開, 料金区分.有料);
        var decision = Assert.IsType<会員確認が必要>(閲覧.開始(story, "reader"));

        var result = decision.結果(true);

        Assert.IsType<許可>(result);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void 有料作品の一般会員は購入済みの場合だけ閲覧できる(bool hasPurchased)
    {
        var story = ストーリー.投稿する("story", "author", 公開範囲.全体公開, 料金区分.有料);
        var membership = Assert.IsType<会員確認が必要>(閲覧.開始(story, "reader"));
        var purchase = Assert.IsType<購入確認が必要>(membership.結果(false));

        var result = purchase.結果(hasPurchased);

        Assert.Equal<閲覧判定>(hasPurchased ? new 許可() : new 拒否(拒否理由.購入が必要), result);
    }
}
