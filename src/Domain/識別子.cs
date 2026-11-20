using System.Diagnostics.CodeAnalysis;

namespace Demo.Domain;

public sealed record 利用者ID
{
    public string 値 { get; }

    public 利用者ID(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        値 = value;
    }

    public static bool 作成できる(string? value, [NotNullWhen(true)] out 利用者ID? id)
    {
        id = string.IsNullOrWhiteSpace(value) ? null : new(value);
        return id is not null;
    }
}

public sealed record ストーリーID
{
    public string 値 { get; }

    public ストーリーID(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        値 = value;
    }

    public static bool 作成できる(string? value, [NotNullWhen(true)] out ストーリーID? id)
    {
        id = string.IsNullOrWhiteSpace(value) ? null : new(value);
        return id is not null;
    }
}
