using Demo.Domain;

namespace Demo.Tests;

// 有限な入力空間をすべて生成する。期待値やドメインの判断処理は使わない。
public sealed class 閲覧入力の全組合せ : TheoryData<閲覧の入力>
{
    public 閲覧入力の全組合せ()
    {
        foreach (var input in 列挙する())
            Add(input);
    }

    public static IEnumerable<閲覧の入力> 列挙する()
    {
        公開範囲[] visibilities = [new 公開範囲.全体公開(), new 公開範囲.フォロワー限定()];
        料金区分[] prices = [new 料金区分.無料(), new 料金区分.有料()];

        foreach (var visibility in visibilities)
        foreach (var price in prices)
        foreach (var relation in Enum.GetValues<作者との関係>())
        foreach (var following in Enum.GetValues<フォロー状況>())
        foreach (var membership in Enum.GetValues<会員資格>())
        foreach (var purchase in Enum.GetValues<購入状況>())
            yield return new(
                new(visibility, price),
                new(relation, following, membership, purchase)
            );
    }
}
