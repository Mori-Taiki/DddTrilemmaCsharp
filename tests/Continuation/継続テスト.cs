using Demo.Domain;

namespace Demo.Tests;

public sealed class 継続テスト
{
    [Fact]
    public void 同じ継続に異なる事実を渡して独立に判断を再開できる()
    {
        var story = new ストーリー(
            new ストーリーID("story"),
            new 利用者ID("author"),
            new 公開範囲.フォロワー限定(),
            new 料金区分.有料()
        );
        var follow = Assert.IsType<フォロー確認が必要>(
            Assert.IsType<事実取得待ち>(閲覧判定.開始(story, new 利用者ID("reader"))).要求
        );

        var denied = follow.再開する(new フォロー確認結果(false));
        var membership = Assert.IsType<VIP会員確認が必要>(
            Assert.IsType<事実取得待ち>(follow.再開する(new フォロー確認結果(true))).要求
        );
        var allowed = membership.再開する(new VIP会員確認結果(true));
        var purchase = Assert.IsType<購入確認が必要>(
            Assert.IsType<事実取得待ち>(membership.再開する(new VIP会員確認結果(false))).要求
        );

        Assert.Equal(new 利用者ID("reader"), follow.閲覧者);
        Assert.Equal(new 利用者ID("author"), follow.作者);
        Assert.Equal(new 利用者ID("reader"), membership.閲覧者);
        Assert.Equal(new 利用者ID("reader"), purchase.閲覧者);
        Assert.Equal(new ストーリーID("story"), purchase.ストーリーID);
        Assert.Equal(new 拒否(閲覧拒否理由.フォローが必要), Assert.IsType<判定完了>(denied).結果);
        Assert.IsType<許可>(Assert.IsType<判定完了>(allowed).結果);
        Assert.Equal(denied, follow.再開する(new フォロー確認結果(false)));
        Assert.Equal(allowed, membership.再開する(new VIP会員確認結果(true)));
    }

    [Fact]
    public void 同じ種類の要求でもストーリーごとに異なる続きを保持する()
    {
        var free = new ストーリー(
            new ストーリーID("free"),
            new 利用者ID("free-author"),
            new 公開範囲.フォロワー限定(),
            new 料金区分.無料()
        );
        var paid = new ストーリー(
            new ストーリーID("paid"),
            new 利用者ID("paid-author"),
            new 公開範囲.フォロワー限定(),
            new 料金区分.有料()
        );
        var freeFollow = Assert.IsType<フォロー確認が必要>(
            Assert.IsType<事実取得待ち>(閲覧判定.開始(free, new 利用者ID("first"))).要求
        );
        var paidFollow = Assert.IsType<フォロー確認が必要>(
            Assert.IsType<事実取得待ち>(閲覧判定.開始(paid, new 利用者ID("second"))).要求
        );

        // 作成と逆の順番で再開しても、別の判定の値が混ざらない。
        var membership = Assert.IsType<VIP会員確認が必要>(
            Assert.IsType<事実取得待ち>(paidFollow.再開する(new フォロー確認結果(true))).要求
        );
        var freeResult = freeFollow.再開する(new フォロー確認結果(true));
        var purchase = Assert.IsType<購入確認が必要>(
            Assert.IsType<事実取得待ち>(membership.再開する(new VIP会員確認結果(false))).要求
        );

        Assert.IsType<許可>(Assert.IsType<判定完了>(freeResult).結果);
        Assert.Equal(new 利用者ID("second"), purchase.閲覧者);
        Assert.Equal(new ストーリーID("paid"), purchase.ストーリーID);
        Assert.Equal(new 利用者ID("free-author"), freeFollow.作者);
        Assert.Equal(new 利用者ID("paid-author"), paidFollow.作者);
    }
}
