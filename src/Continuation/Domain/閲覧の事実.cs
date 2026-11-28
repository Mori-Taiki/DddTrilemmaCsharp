namespace Demo.Domain;

public readonly record struct フォロー確認結果(bool フォローしている);

public readonly record struct VIP会員確認結果(bool VIPである);

public readonly record struct 購入確認結果(bool 購入済み);
