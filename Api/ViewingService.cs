using Demo.Domain;
namespace Demo.Api;
public interface IViewingFacts
{
    Task<bool> Follows(string viewer, string author, CancellationToken ct);
    Task<bool> IsVip(string viewer, CancellationToken ct);
    Task<bool> Purchased(string viewer, string story, CancellationToken ct);
}
public sealed record ViewingResult(bool Allowed, string? Reason, string[] Trace, int FactQueries);
public sealed class ViewingService(IViewingFacts facts)
{
    public async Task<ViewingResult> Evaluate(Story story, string viewer, CancellationToken ct)
    {
        var decision = Viewing.Start(story, viewer);
        var trace = new List<string>();
        var count = 0;
        while (true)
        {
            trace.Add(decision.GetType().Name);
            if (decision is Allowed) return new(true, null, trace.ToArray(), count);
            if (decision is Denied denied) return new(false, denied.Reason, trace.ToArray(), count);
            ct.ThrowIfCancellationRequested();
            count++;
            decision = decision switch
            {
                Allowed a => a,
                Denied d => d,
                NeedFollow n => n.Resume(await facts.Follows(n.Viewer, n.Story.AuthorId, ct)),
                NeedMembership n => n.Resume(await facts.IsVip(n.Viewer, ct)),
                NeedPurchase n => n.Resume(await facts.Purchased(n.Viewer, n.Story.Id, ct))
            };
        }
    }
}
