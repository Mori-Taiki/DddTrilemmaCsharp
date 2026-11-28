using Demo.Domain;

namespace Demo.Tests;

public sealed class ドメイン入力テスト
{
    [Theory]
    [InlineData("id")]
    [InlineData("author")]
    [InlineData("visibility")]
    [InlineData("price")]
    public void 必須の値が欠けたストーリーは作成できない(string missingParameter)
    {
        Assert.Throws<ArgumentNullException>(missingParameter, () =>
            new ストーリー(
                missingParameter == "id" ? null! : new ストーリーID("story"),
                missingParameter == "author" ? null! : new 利用者ID("author"),
                missingParameter == "visibility" ? null! : new 公開範囲.全体公開(),
                missingParameter == "price" ? null! : new 料金区分.無料()
            )
        );
    }

    [Theory]
    [InlineData("story")]
    [InlineData("viewer")]
    public void 必須の値が欠けている場合は閲覧判定を開始できない(string missingParameter)
    {
        var story = ストーリーを作成する();

        Assert.Throws<ArgumentNullException>(missingParameter, () =>
            閲覧判定.開始(
                missingParameter == "story" ? null! : story,
                missingParameter == "viewer" ? null! : new 利用者ID("reader")
            )
        );
    }

    [Theory]
    [InlineData("newVisibility")]
    [InlineData("actor")]
    public void 公開範囲の変更にnullを渡しても現在の状態を壊さない(string missingParameter)
    {
        var story = ストーリーを作成する();

        Assert.Throws<ArgumentNullException>(missingParameter, () =>
            story.公開範囲を変える(
                missingParameter == "newVisibility" ? null! : new 公開範囲.フォロワー限定(),
                missingParameter == "actor" ? null! : new 利用者ID("author")
            )
        );

        Assert.Equal(new ストーリーID("story"), story.ID);
        Assert.Equal(new 利用者ID("author"), story.作者);
        Assert.Equal(new 公開範囲.全体公開(), story.公開範囲);
        Assert.Equal(new 料金区分.無料(), story.料金区分);
    }

    private static ストーリー ストーリーを作成する() =>
        new(
            new ストーリーID("story"),
            new 利用者ID("author"),
            new 公開範囲.全体公開(),
            new 料金区分.無料()
        );
}
