namespace Demo.Domain;
public enum Visibility { Public, Followers }
public enum Price { Free, Paid }
public sealed class Story
{
    private Story() { }
    public string Id { get; private set; } = "";
    public string AuthorId { get; private set; } = "";
    public Visibility Visibility { get; private set; }
    public Price Price { get; private set; }
    public static Story Create(string id, string authorId, Visibility visibility, Price price)
        => new() { Id = id, AuthorId = authorId, Visibility = visibility, Price = price };
    public ChangeResult ChangeVisibility(Visibility value, string actor)
    {
        if (actor != AuthorId) return new ChangeDenied("NotAuthor");
        if (value == Visibility) return new ChangeDenied("AlreadySet");
        Visibility = value;
        return new Changed();
    }
}
public closed record ChangeResult;
public sealed record Changed : ChangeResult;
public sealed record ChangeDenied(string Reason) : ChangeResult;
public closed record Decision;
public sealed record Allowed : Decision;
public sealed record Denied(string Reason) : Decision;
public sealed record NeedFollow(Story Story, string Viewer) : Decision
{
    public Decision Resume(bool follows) => follows ? Viewing.CheckPrice(Story, Viewer) : new Denied("FollowRequired");
}
public sealed record NeedMembership(Story Story, string Viewer) : Decision
{
    public Decision Resume(bool vip) => vip ? new Allowed() : new NeedPurchase(Story, Viewer);
}
public sealed record NeedPurchase(Story Story, string Viewer) : Decision
{
    public Decision Resume(bool purchased) => purchased ? new Allowed() : new Denied("PurchaseRequired");
}
public static class Viewing
{
    public static Decision Start(Story story, string viewer) => story.AuthorId == viewer ? new Allowed() : story.Visibility switch
    {
        Visibility.Public => CheckPrice(story, viewer),
        Visibility.Followers => new NeedFollow(story, viewer),
        _ => throw new ArgumentOutOfRangeException(nameof(story))
    };
    internal static Decision CheckPrice(Story story, string viewer) => story.Price == Price.Free ? new Allowed() : new NeedMembership(story, viewer);
}
