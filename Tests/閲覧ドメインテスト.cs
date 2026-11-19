using Demo.Api;
using Demo.Domain;

namespace Demo.Tests;

public sealed class 閲覧ドメインテスト
{
    [Theory]
    [InlineData("全体公開", "無料")]
    [InlineData("全体公開", "有料")]
    [InlineData("フォロワー限定", "無料")]
    [InlineData("フォロワー限定", "有料")]
    public void 作者本人は公開範囲と料金によらず閲覧できる(string visibility, string price)
    {
        var story = ストーリー.投稿する(
            new ストーリーID("story"),
            new 利用者ID("author"),
            境界値.公開範囲を読む(visibility)!,
            境界値.料金を読む(price)!
        );

        var result = 閲覧.開始(story, new 利用者ID("author"));

        Assert.IsType<許可>(Assert.IsType<判定完了>(result).結果);
    }

    [Fact]
    public void 全体公開の無料作品は追加の事実なしで閲覧できる()
    {
        var story = ストーリー.投稿する(
            new ストーリーID("story"),
            new 利用者ID("author"),
            new 公開範囲.全体公開(),
            new 料金区分.無料()
        );

        var result = 閲覧.開始(story, new 利用者ID("reader"));

        Assert.IsType<許可>(Assert.IsType<判定完了>(result).結果);
    }

    [Fact]
    public void フォロワー限定作品は非フォロワーの閲覧を拒否する()
    {
        var story = ストーリー.投稿する(
            new ストーリーID("story"),
            new 利用者ID("author"),
            new 公開範囲.フォロワー限定(),
            new 料金区分.無料()
        );
        var decision = Assert.IsType<フォロー確認が必要>(
            Assert.IsType<事実取得待ち>(閲覧.開始(story, new 利用者ID("reader"))).要求
        );

        var result = decision.結果(false);

        Assert.Equal(new 拒否(閲覧拒否理由.フォローが必要), Assert.IsType<判定完了>(result).結果);
    }

    [Fact]
    public void 有料作品はVIP会員なら購入確認なしで閲覧できる()
    {
        var story = ストーリー.投稿する(
            new ストーリーID("story"),
            new 利用者ID("author"),
            new 公開範囲.全体公開(),
            new 料金区分.有料()
        );
        var decision = Assert.IsType<会員確認が必要>(
            Assert.IsType<事実取得待ち>(閲覧.開始(story, new 利用者ID("reader"))).要求
        );

        var result = decision.結果(true);

        Assert.IsType<許可>(Assert.IsType<判定完了>(result).結果);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void 有料作品の一般会員は購入済みの場合だけ閲覧できる(bool hasPurchased)
    {
        var story = ストーリー.投稿する(
            new ストーリーID("story"),
            new 利用者ID("author"),
            new 公開範囲.全体公開(),
            new 料金区分.有料()
        );
        var membership = Assert.IsType<会員確認が必要>(
            Assert.IsType<事実取得待ち>(閲覧.開始(story, new 利用者ID("reader"))).要求
        );
        var purchase = Assert.IsType<購入確認が必要>(
            Assert.IsType<事実取得待ち>(membership.結果(false)).要求
        );

        var result = purchase.結果(hasPurchased);

        Assert.Equal<閲覧結果>(
            hasPurchased ? new 許可() : new 拒否(閲覧拒否理由.購入が必要),
            Assert.IsType<判定完了>(result).結果
        );
    }
}
