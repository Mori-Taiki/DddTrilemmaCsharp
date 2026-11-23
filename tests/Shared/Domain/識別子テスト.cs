using Demo.Domain;

namespace Demo.Tests;

public sealed class 識別子テスト
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
}
