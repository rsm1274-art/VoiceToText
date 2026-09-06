namespace Murmur.Dictionary;

public sealed record DictionaryEntry(long Id, string Term, string AliasesCsv)
{
    public IReadOnlyList<string> Aliases => AliasesCsv.Split(
        ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
