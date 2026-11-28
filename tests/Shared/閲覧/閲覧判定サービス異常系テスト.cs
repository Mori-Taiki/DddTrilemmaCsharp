using Demo.Api;
using Demo.Domain;

namespace Demo.Tests;

public sealed class 閲覧判定サービス異常系テスト
{
    [Fact]
    public async Task 事実取得の障害は業務上の拒否に変換されない()
    {
        var story = new ストーリー(
            new ストーリーID("story"),
            new 利用者ID("author"),
            new 公開範囲.全体公開(),
            new 料金区分.有料()
        );
        var service = new 閲覧判定サービス(new 障害を起こす事実取得());

        await Assert.ThrowsAsync<IOException>(() =>
            service.判定する(story, new 利用者ID("reader"), default)
        );
    }

    [Theory]
    [InlineData("全体公開", "有料")]
    [InlineData("フォロワー限定", "無料")]
    public async Task キャンセル済みなら事実取得を開始しない(string visibility, string price)
    {
        var story = new ストーリー(
            new ストーリーID("story"),
            new 利用者ID("author"),
            境界変換.公開範囲に変換する(visibility)!,
            境界変換.料金区分に変換する(price)!
        );
        // 呼び出されるとIOExceptionになる取得先でも、先にキャンセルされる。
        var service = new 閲覧判定サービス(new 障害を起こす事実取得());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.判定する(story, new 利用者ID("reader"), cancellation.Token)
        );

        Assert.Equal(cancellation.Token, exception.CancellationToken);
    }

    // 障害・キャンセルの経路を安定して再現する。呼び出し順序は検証しない。
    private sealed class 障害を起こす事実取得 : 閲覧事実取得
    {
        public Task<フォロー確認結果> フォローしているか(
            利用者ID viewer,
            利用者ID author,
            CancellationToken ct
        ) => throw new IOException();

        public Task<VIP会員確認結果> VIP会員か(利用者ID viewer, CancellationToken ct) =>
            throw new IOException();

        public Task<購入確認結果> 購入済みか(
            利用者ID viewer,
            ストーリーID storyId,
            CancellationToken ct
        ) => throw new IOException();
    }
}
