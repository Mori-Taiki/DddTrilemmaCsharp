using Demo.Domain;

namespace Demo.Tests;

public sealed class 閲覧ドメインテスト
{
    [Theory]
    [ClassData(typeof(閲覧仕様一覧))]
    public void 共通の仕様どおりに事実を要求して判定する(閲覧仕様 仕様)
    {
        var story = 仕様.入力.作品.作成する();
        var reader = 仕様.入力.閲覧者;
        var decision = 閲覧.開始(story, reader.ID);
        var requested = new List<取得する事実>();

        // 期待する取得回数だけ応答し、その直後に完了することを確認する。
        // 回答する種類は、期待値ではなく実装が実際に返した要求から選ぶ。
        for (var step = 0; step < 仕様.期待.取得順序.Length; step++)
        {
            var request = Assert.IsType<事実取得待ち>(decision).要求;
            decision = request switch
            {
                フォロー確認が必要 follow => フォローに答える(follow),
                会員確認が必要 membership => 会員確認に答える(membership),
                購入確認が必要 purchase => 購入確認に答える(purchase),
            };
        }

        Assert.Equal(仕様.期待.結果, Assert.IsType<判定完了>(decision).結果);
        Assert.Equal<取得する事実>(仕様.期待.取得順序, requested);

        閲覧判定 フォローに答える(フォロー確認が必要 request)
        {
            Assert.Equal(reader.ID, request.閲覧者);
            Assert.Equal(story.作者, request.作者);
            requested.Add(取得する事実.フォロー確認);
            return request.続ける(reader.フォローの回答);
        }

        閲覧判定 会員確認に答える(会員確認が必要 request)
        {
            Assert.Equal(reader.ID, request.閲覧者);
            requested.Add(取得する事実.会員確認);
            return request.続ける(reader.会員の回答);
        }

        閲覧判定 購入確認に答える(購入確認が必要 request)
        {
            Assert.Equal(reader.ID, request.閲覧者);
            Assert.Equal(story.ID, request.作品);
            requested.Add(取得する事実.購入確認);
            return request.続ける(reader.購入の回答);
        }
    }
}
