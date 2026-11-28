using Demo.Domain;
using Microsoft.EntityFrameworkCore;

namespace Demo.Api;

public static class ストーリーAPI
{
    public static void ストーリーAPIを登録する(this WebApplication app)
    {
        app.MapGet("/stories", async (AppDb db, CancellationToken ct) =>
        {
            var stories = await db.ストーリー一覧.AsNoTracking().OrderBy(x => x.ID).ToListAsync(ct);
            return stories.Select(story => new
            {
                id = story.ID.値,
                authorId = story.作者.値,
                visibility = 境界変換.文字列にする(story.公開範囲),
                price = 境界変換.文字列にする(story.料金区分),
            });
        });

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
                    !利用者ID.作成を試みる(viewer, out var viewerId)
                    || !ストーリーID.作成を試みる(id, out var storyId)
                )
                    return Results.BadRequest(new { error = "viewer is required" });

                // Queryでは追跡せず、このオブジェクトを判定の途中で変更しない。
                var story = await db
                    .ストーリー一覧.AsNoTracking()
                    .SingleOrDefaultAsync(x => x.ID == storyId, ct);
                if (story is null)
                    return Results.NotFound();

                var result = await service.判定する(story, viewerId, ct);
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
                var visibility = 境界変換.公開範囲に変換する(request.公開範囲);
                if (
                    !利用者ID.作成を試みる(request.実行者, out var actor)
                    || !ストーリーID.作成を試みる(id, out var storyId)
                    || visibility is null
                )
                    return Results.BadRequest(
                        new { error = "actor and valid visibility are required" }
                    );

                // 同じDbContextで取得・変更・保存する。AttachやUpdateは不要。
                var story = await db.ストーリー一覧.SingleOrDefaultAsync(x => x.ID == storyId, ct);
                if (story is null)
                    return Results.NotFound();

                var result = story.公開範囲を変える(visibility, actor);
                if (result is 変更完了)
                    await db.SaveChangesAsync(ct);

                return result switch
                {
                    変更完了 => Results.Ok(
                        new { id = story.ID.値, visibility = 境界変換.文字列にする(story.公開範囲) }
                    ),
                    変更拒否 denied => Results.Json(
                        new { reason = denied.理由 },
                        statusCode: denied.理由 == 変更拒否理由.作者ではない ? 403 : 409
                    ),
                };
            }
        );
    }
}
