using Demo.Domain;
using Microsoft.EntityFrameworkCore;

namespace Demo.Api;

public static class ストーリーAPI
{
    public static void 登録する(this WebApplication app)
    {
        app.MapGet("/stories", async (AppDb db, CancellationToken ct) => await db
                .ストーリー一覧.AsNoTracking()
                .OrderBy(x => x.ID)
                .Select(x => new
                {
                    id = x.ID,
                    authorId = x.作者,
                    visibility = x.公開範囲,
                    price = x.料金,
                })
                .ToListAsync(ct));

        app.MapGet(
            "/stories/{id}/view",
            async Task<IResult> (
                string id,
                string? viewer,
                AppDb db,
                閲覧判定サービス service,
                ILogger<閲覧判定サービス> log,
                CancellationToken ct
            ) =>
            {
                if (
                    !利用者ID.作成できる(viewer, out var viewerId)
                    || !ストーリーID.作成できる(id, out var storyId)
                )
                    return Results.BadRequest(new { error = "viewer is required" });

                var story = await db
                    .ストーリー一覧.AsNoTracking()
                    .SingleOrDefaultAsync(x => x.ID == storyId.値, ct);
                if (story is null)
                    return Results.NotFound();

                var result = await service.判定する(story.ドメインに戻す(), viewerId, ct);

                log.LogInformation(
                    "Decision {Trace}; fact queries={Count} (story load excluded)",
                    string.Join(" -> ", result.判定経路),
                    result.事実取得回数
                );

                return Results.Json(result, statusCode: result.許可 ? 200 : 403);
            }
        );

        app.MapPut(
            "/stories/{id}/visibility",
            async Task<IResult> (
                string id,
                公開範囲変更要求 request,
                AppDb db,
                CancellationToken ct
            ) =>
            {
                var visibility = 境界値.公開範囲を読む(request.公開範囲);
                if (
                    !利用者ID.作成できる(request.実行者, out var actor)
                    || !ストーリーID.作成できる(id, out var storyId)
                    || visibility is null
                )
                    return Results.BadRequest(
                        new { error = "actor and valid visibility are required" }
                    );

                var row = await db.ストーリー一覧.SingleOrDefaultAsync(x => x.ID == storyId.値, ct);
                if (row is null)
                    return Results.NotFound();

                var story = row.ドメインに戻す();
                var result = story.公開範囲を変える(visibility, actor);
                if (result is 変更できた changed)
                {
                    row.適用する(changed.変更後);
                    await db.SaveChangesAsync(ct);
                }

                return result switch
                {
                    変更できた success => Results.Ok(
                        new
                        {
                            id = success.変更後.ID.値,
                            visibility = 境界値.文字列にする(success.変更後.公開範囲),
                        }
                    ),
                    変更拒否 d => Results.Json(
                        new { reason = d.理由 },
                        statusCode: d.理由 == 変更拒否理由.作者ではない ? 403 : 409
                    ),
                };
            }
        );
    }
}
