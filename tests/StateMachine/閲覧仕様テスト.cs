using Demo.Domain;

namespace Demo.Tests;

public sealed class 閲覧仕様テスト
{
    [Fact]
    public void 作者本人は全体公開の無料作品を追加取得なしで読める()
    {
        var story = new ストーリー(
            new ストーリーID("story"),
            new 利用者ID("author"),
            new 公開範囲.全体公開(),
            new 料金区分.無料()
        );
        var viewer = new 利用者ID("author");

        var 開始時点 = 閲覧.開始(story, viewer);

        Assert.Equal(new 許可(), Assert.IsType<判定完了>(開始時点).結果);
    }

    [Fact]
    public void 作者本人は全体公開の有料作品を追加取得なしで読める()
    {
        var story = new ストーリー(
            new ストーリーID("story"),
            new 利用者ID("author"),
            new 公開範囲.全体公開(),
            new 料金区分.有料()
        );
        var viewer = new 利用者ID("author");

        var 開始時点 = 閲覧.開始(story, viewer);

        Assert.Equal(new 許可(), Assert.IsType<判定完了>(開始時点).結果);
    }

    [Fact]
    public void 作者本人はフォロワー限定の無料作品を追加取得なしで読める()
    {
        var story = new ストーリー(
            new ストーリーID("story"),
            new 利用者ID("author"),
            new 公開範囲.フォロワー限定(),
            new 料金区分.無料()
        );
        var viewer = new 利用者ID("author");

        var 開始時点 = 閲覧.開始(story, viewer);

        Assert.Equal(new 許可(), Assert.IsType<判定完了>(開始時点).結果);
    }

    [Fact]
    public void 作者本人はフォロワー限定の有料作品を追加取得なしで読める()
    {
        var story = new ストーリー(
            new ストーリーID("story"),
            new 利用者ID("author"),
            new 公開範囲.フォロワー限定(),
            new 料金区分.有料()
        );
        var viewer = new 利用者ID("author");

        var 開始時点 = 閲覧.開始(story, viewer);

        Assert.Equal(new 許可(), Assert.IsType<判定完了>(開始時点).結果);
    }

    [Fact]
    public void 無料公開は作者以外も追加取得なしで読める()
    {
        var story = new ストーリー(
            new ストーリーID("story"),
            new 利用者ID("author"),
            new 公開範囲.全体公開(),
            new 料金区分.無料()
        );
        var viewer = new 利用者ID("reader");

        var 開始時点 = 閲覧.開始(story, viewer);

        Assert.Equal(new 許可(), Assert.IsType<判定完了>(開始時点).結果);
    }

    [Fact]
    public void 無料限定はフォロワーが読める()
    {
        var story = new ストーリー(
            new ストーリーID("story"),
            new 利用者ID("author"),
            new 公開範囲.フォロワー限定(),
            new 料金区分.無料()
        );
        var viewer = new 利用者ID("reader");

        var 開始時点 = 閲覧.開始(story, viewer);
        var フォロー確認 = Assert.IsType<フォロー確認が必要>(
            Assert.IsType<事実取得待ち>(開始時点).要求
        );
        Assert.Equal(viewer, フォロー確認.閲覧者);
        Assert.Equal(story, フォロー確認.対象);
        var フォロー確認後 = フォロー確認.結果(new フォロー確認結果(true));

        Assert.Equal(new 許可(), Assert.IsType<判定完了>(フォロー確認後).結果);
    }

    [Fact]
    public void 無料限定は非フォロワーを拒否する()
    {
        var story = new ストーリー(
            new ストーリーID("story"),
            new 利用者ID("author"),
            new 公開範囲.フォロワー限定(),
            new 料金区分.無料()
        );
        var viewer = new 利用者ID("reader");

        var 開始時点 = 閲覧.開始(story, viewer);
        var フォロー確認 = Assert.IsType<フォロー確認が必要>(
            Assert.IsType<事実取得待ち>(開始時点).要求
        );
        Assert.Equal(viewer, フォロー確認.閲覧者);
        Assert.Equal(story, フォロー確認.対象);
        var フォロー確認後 = フォロー確認.結果(new フォロー確認結果(false));

        Assert.Equal(
            new 拒否(閲覧拒否理由.フォローが必要),
            Assert.IsType<判定完了>(フォロー確認後).結果
        );
    }

    [Fact]
    public void 有料公開はVIPなら購入確認なしで読める()
    {
        var story = new ストーリー(
            new ストーリーID("story"),
            new 利用者ID("author"),
            new 公開範囲.全体公開(),
            new 料金区分.有料()
        );
        var viewer = new 利用者ID("reader");

        var 開始時点 = 閲覧.開始(story, viewer);
        var 会員確認 = Assert.IsType<会員確認が必要>(Assert.IsType<事実取得待ち>(開始時点).要求);
        Assert.Equal(viewer, 会員確認.閲覧者);
        Assert.Equal(story, 会員確認.対象);
        var 会員確認後 = 会員確認.結果(new 会員確認結果(true));

        Assert.Equal(new 許可(), Assert.IsType<判定完了>(会員確認後).結果);
    }

    [Fact]
    public void 有料公開は一般会員も購入済みなら読める()
    {
        var story = new ストーリー(
            new ストーリーID("story"),
            new 利用者ID("author"),
            new 公開範囲.全体公開(),
            new 料金区分.有料()
        );
        var viewer = new 利用者ID("reader");

        var 開始時点 = 閲覧.開始(story, viewer);
        var 会員確認 = Assert.IsType<会員確認が必要>(Assert.IsType<事実取得待ち>(開始時点).要求);
        Assert.Equal(viewer, 会員確認.閲覧者);
        Assert.Equal(story, 会員確認.対象);
        var 会員確認後 = 会員確認.結果(new 会員確認結果(false));
        var 購入確認 = Assert.IsType<購入確認が必要>(Assert.IsType<事実取得待ち>(会員確認後).要求);
        Assert.Equal(viewer, 購入確認.閲覧者);
        Assert.Equal(story, 購入確認.対象);
        var 購入確認後 = 購入確認.結果(new 購入確認結果(true));

        Assert.Equal(new 許可(), Assert.IsType<判定完了>(購入確認後).結果);
    }

    [Fact]
    public void 有料公開は未購入の一般会員を拒否する()
    {
        var story = new ストーリー(
            new ストーリーID("story"),
            new 利用者ID("author"),
            new 公開範囲.全体公開(),
            new 料金区分.有料()
        );
        var viewer = new 利用者ID("reader");

        var 開始時点 = 閲覧.開始(story, viewer);
        var 会員確認 = Assert.IsType<会員確認が必要>(Assert.IsType<事実取得待ち>(開始時点).要求);
        Assert.Equal(viewer, 会員確認.閲覧者);
        Assert.Equal(story, 会員確認.対象);
        var 会員確認後 = 会員確認.結果(new 会員確認結果(false));
        var 購入確認 = Assert.IsType<購入確認が必要>(Assert.IsType<事実取得待ち>(会員確認後).要求);
        Assert.Equal(viewer, 購入確認.閲覧者);
        Assert.Equal(story, 購入確認.対象);
        var 購入確認後 = 購入確認.結果(new 購入確認結果(false));

        Assert.Equal(new 拒否(閲覧拒否理由.購入が必要), Assert.IsType<判定完了>(購入確認後).結果);
    }

    [Fact]
    public void 有料限定はフォロー済みVIPなら購入確認なしで読める()
    {
        var story = new ストーリー(
            new ストーリーID("story"),
            new 利用者ID("author"),
            new 公開範囲.フォロワー限定(),
            new 料金区分.有料()
        );
        var viewer = new 利用者ID("reader");

        var 開始時点 = 閲覧.開始(story, viewer);
        var フォロー確認 = Assert.IsType<フォロー確認が必要>(
            Assert.IsType<事実取得待ち>(開始時点).要求
        );
        Assert.Equal(viewer, フォロー確認.閲覧者);
        Assert.Equal(story, フォロー確認.対象);
        var フォロー確認後 = フォロー確認.結果(new フォロー確認結果(true));
        var 会員確認 = Assert.IsType<会員確認が必要>(
            Assert.IsType<事実取得待ち>(フォロー確認後).要求
        );
        Assert.Equal(viewer, 会員確認.閲覧者);
        Assert.Equal(story, 会員確認.対象);
        var 会員確認後 = 会員確認.結果(new 会員確認結果(true));

        Assert.Equal(new 許可(), Assert.IsType<判定完了>(会員確認後).結果);
    }

    [Fact]
    public void 有料限定はフォロー済み購入者が読める()
    {
        var story = new ストーリー(
            new ストーリーID("story"),
            new 利用者ID("author"),
            new 公開範囲.フォロワー限定(),
            new 料金区分.有料()
        );
        var viewer = new 利用者ID("reader");

        var 開始時点 = 閲覧.開始(story, viewer);
        var フォロー確認 = Assert.IsType<フォロー確認が必要>(
            Assert.IsType<事実取得待ち>(開始時点).要求
        );
        Assert.Equal(viewer, フォロー確認.閲覧者);
        Assert.Equal(story, フォロー確認.対象);
        var フォロー確認後 = フォロー確認.結果(new フォロー確認結果(true));
        var 会員確認 = Assert.IsType<会員確認が必要>(
            Assert.IsType<事実取得待ち>(フォロー確認後).要求
        );
        Assert.Equal(viewer, 会員確認.閲覧者);
        Assert.Equal(story, 会員確認.対象);
        var 会員確認後 = 会員確認.結果(new 会員確認結果(false));
        var 購入確認 = Assert.IsType<購入確認が必要>(Assert.IsType<事実取得待ち>(会員確認後).要求);
        Assert.Equal(viewer, 購入確認.閲覧者);
        Assert.Equal(story, 購入確認.対象);
        var 購入確認後 = 購入確認.結果(new 購入確認結果(true));

        Assert.Equal(new 許可(), Assert.IsType<判定完了>(購入確認後).結果);
    }

    [Fact]
    public void 有料限定はフォロー済みでも未購入の一般会員を拒否する()
    {
        var story = new ストーリー(
            new ストーリーID("story"),
            new 利用者ID("author"),
            new 公開範囲.フォロワー限定(),
            new 料金区分.有料()
        );
        var viewer = new 利用者ID("reader");

        var 開始時点 = 閲覧.開始(story, viewer);
        var フォロー確認 = Assert.IsType<フォロー確認が必要>(
            Assert.IsType<事実取得待ち>(開始時点).要求
        );
        Assert.Equal(viewer, フォロー確認.閲覧者);
        Assert.Equal(story, フォロー確認.対象);
        var フォロー確認後 = フォロー確認.結果(new フォロー確認結果(true));
        var 会員確認 = Assert.IsType<会員確認が必要>(
            Assert.IsType<事実取得待ち>(フォロー確認後).要求
        );
        Assert.Equal(viewer, 会員確認.閲覧者);
        Assert.Equal(story, 会員確認.対象);
        var 会員確認後 = 会員確認.結果(new 会員確認結果(false));
        var 購入確認 = Assert.IsType<購入確認が必要>(Assert.IsType<事実取得待ち>(会員確認後).要求);
        Assert.Equal(viewer, 購入確認.閲覧者);
        Assert.Equal(story, 購入確認.対象);
        var 購入確認後 = 購入確認.結果(new 購入確認結果(false));

        Assert.Equal(new 拒否(閲覧拒否理由.購入が必要), Assert.IsType<判定完了>(購入確認後).結果);
    }

    [Fact]
    public void 有料限定の非フォロワーは会員や購入を確認せず拒否する()
    {
        var story = new ストーリー(
            new ストーリーID("story"),
            new 利用者ID("author"),
            new 公開範囲.フォロワー限定(),
            new 料金区分.有料()
        );
        var viewer = new 利用者ID("reader");

        var 開始時点 = 閲覧.開始(story, viewer);
        var フォロー確認 = Assert.IsType<フォロー確認が必要>(
            Assert.IsType<事実取得待ち>(開始時点).要求
        );
        Assert.Equal(viewer, フォロー確認.閲覧者);
        Assert.Equal(story, フォロー確認.対象);
        var フォロー確認後 = フォロー確認.結果(new フォロー確認結果(false));

        Assert.Equal(
            new 拒否(閲覧拒否理由.フォローが必要),
            Assert.IsType<判定完了>(フォロー確認後).結果
        );
    }
}
