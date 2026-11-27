using System.Collections.Immutable;
using Demo.Domain;

namespace Demo.Tests;

internal static class 閲覧の実行
{
    public static 閲覧の観測 観測する(閲覧の入力 入力)
    {
        var story = 入力.作品.作成する();
        var reader = 入力.閲覧者;
        var decision = 閲覧.開始(story, reader.ID);
        var requested = new List<取得する事実>();

        // 期待値を受け取らず、実際の要求にだけ回答する。
        // 3種類を一度ずつ取得すれば十分なため、4回目の要求は失敗にする。
        for (var step = 0; decision is 事実取得待ち waiting; step++)
        {
            Assert.True(step < 3, "閲覧判定が3回の事実取得で完了しませんでした。");
            decision = waiting.要求 switch
            {
                フォロー確認が必要 follow => フォローに答える(follow),
                会員確認が必要 membership => 会員確認に答える(membership),
                購入確認が必要 purchase => 購入確認に答える(purchase),
            };
        }

        return new(Assert.IsType<判定完了>(decision).結果, requested.ToImmutableArray());

        閲覧判定 フォローに答える(フォロー確認が必要 request)
        {
            Assert.Equal(reader.ID, request.閲覧者);
            Assert.Equal(story.作者, request.対象.作者);
            requested.Add(取得する事実.フォロー確認);
            return request.結果(reader.フォローの回答);
        }

        閲覧判定 会員確認に答える(会員確認が必要 request)
        {
            Assert.Equal(reader.ID, request.閲覧者);
            requested.Add(取得する事実.会員確認);
            return request.結果(reader.会員の回答);
        }

        閲覧判定 購入確認に答える(購入確認が必要 request)
        {
            Assert.Equal(reader.ID, request.閲覧者);
            Assert.Equal(story.ID, request.対象.ID);
            requested.Add(取得する事実.購入確認);
            return request.結果(reader.購入の回答);
        }
    }
}
