using System.Collections.Immutable;
using Demo.Domain;

namespace Demo.Tests;

internal sealed record 閲覧の観測(閲覧結果 結果, ImmutableArray<取得する事実> 取得順序);
