using Demo.Domain;
using static Demo.Tests.取得する事実;

namespace Demo.Tests;

public sealed class 閲覧の性質テスト
{
    [Fact]
    public void 入力は重複なしで全64通りを列挙する()
    {
        var inputs = 閲覧入力の全組合せ.列挙する().ToArray();

        Assert.Equal(64, inputs.Length);
        Assert.Equal(64, inputs.Distinct().Count());
    }

    [Theory]
    [ClassData(typeof(閲覧入力の全組合せ))]
    public void 許可は作者本人か公開範囲と料金の両条件を満たす場合に限られる(閲覧の入力 入力)
    {
        var actual = 閲覧の実行.観測する(入力);

        // 判定を進める手順ではなく、許可される入力の集合を記述する。
        var 作者本人である = 入力.閲覧者.作者との関係 == 作者との関係.作者本人;
        var 公開範囲を満たす =
            入力.作品.公開範囲 is 公開範囲.全体公開
            || 入力.閲覧者.フォロー == フォロー状況.フォロー済み;
        var 料金条件を満たす =
            入力.作品.料金 is 料金区分.無料
            || 入力.閲覧者.会員 == 会員資格.VIP会員
            || 入力.閲覧者.購入 == 購入状況.購入済み;

        Assert.Equal(作者本人である || (公開範囲を満たす && 料金条件を満たす), actual.結果 is 許可);
    }

    [Theory]
    [ClassData(typeof(閲覧入力の全組合せ))]
    public void 拒否理由はフォロー不足を優先して満たさない条件を示す(閲覧の入力 入力)
    {
        var actual = 閲覧の実行.観測する(入力);
        var 作者以外である = 入力.閲覧者.作者との関係 == 作者との関係.作者以外;
        var フォローが不足する =
            作者以外である
            && 入力.作品.公開範囲 is 公開範囲.フォロワー限定
            && 入力.閲覧者.フォロー == フォロー状況.未フォロー;
        var 購入が不足する =
            作者以外である
            && 入力.作品.料金 is 料金区分.有料
            && 入力.閲覧者.会員 == 会員資格.一般会員
            && 入力.閲覧者.購入 == 購入状況.未購入;

        Assert.Equal(フォローが不足する, actual.結果 is 拒否 { 理由: 閲覧拒否理由.フォローが必要 });
        Assert.Equal(
            !フォローが不足する && 購入が不足する,
            actual.結果 is 拒否 { 理由: 閲覧拒否理由.購入が必要 }
        );
    }

    [Theory]
    [ClassData(typeof(閲覧入力の全組合せ))]
    public void 必要な事実だけを一度ずつフォロー会員購入の順に取得する(閲覧の入力 入力)
    {
        var actual = 閲覧の実行.観測する(入力);
        var 作者以外である = 入力.閲覧者.作者との関係 == 作者との関係.作者以外;
        var フォローの確認が必要 = 作者以外である && 入力.作品.公開範囲 is 公開範囲.フォロワー限定;
        var 会員の確認が必要 =
            作者以外である
            && 入力.作品.料金 is 料金区分.有料
            && (
                入力.作品.公開範囲 is 公開範囲.全体公開
                || 入力.閲覧者.フォロー == フォロー状況.フォロー済み
            );
        var 購入の確認が必要 = 会員の確認が必要 && 入力.閲覧者.会員 == 会員資格.一般会員;

        Assert.Equal(フォローの確認が必要 ? 1 : 0, actual.取得順序.Count(x => x == フォロー確認));
        Assert.Equal(会員の確認が必要 ? 1 : 0, actual.取得順序.Count(x => x == 会員確認));
        Assert.Equal(購入の確認が必要 ? 1 : 0, actual.取得順序.Count(x => x == 購入確認));

        // 取得列はこの順序の部分列でなければならない。
        取得する事実[] 順序 = [フォロー確認, 会員確認, 購入確認];
        Assert.Equal<取得する事実>(順序.Where(actual.取得順序.Contains), actual.取得順序);
    }

    [Theory]
    [ClassData(typeof(閲覧入力の全組合せ))]
    public void 取得しなかった事実を変えても結果と取得順序は変わらない(閲覧の入力 入力)
    {
        var before = 閲覧の実行.観測する(入力);
        var reader = 入力.閲覧者;
        (取得する事実 事実, 閲覧者の条件 変更後)[] changes =
        [
            (
                フォロー確認,
                reader with
                {
                    フォロー =
                        reader.フォロー == フォロー状況.未フォロー
                            ? フォロー状況.フォロー済み
                            : フォロー状況.未フォロー,
                }
            ),
            (
                会員確認,
                reader with
                {
                    会員 = reader.会員 == 会員資格.一般会員 ? 会員資格.VIP会員 : 会員資格.一般会員,
                }
            ),
            (
                購入確認,
                reader with
                {
                    購入 = reader.購入 == 購入状況.未購入 ? 購入状況.購入済み : 購入状況.未購入,
                }
            ),
        ];

        foreach (var change in changes.Where(x => !before.取得順序.Contains(x.事実)))
        {
            var after = 閲覧の実行.観測する(入力 with { 閲覧者 = change.変更後 });

            Assert.Equal(before.結果, after.結果);
            Assert.Equal<取得する事実>(before.取得順序, after.取得順序);
        }
    }
}
