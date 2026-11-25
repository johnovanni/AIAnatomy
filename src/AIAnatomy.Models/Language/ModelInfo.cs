using System.Text.Json.Serialization;

namespace AIAnatomy.Models.Language;

/// <summary>
/// What a saved model remembers about itself. <c>ValidationLoss</c> is null when it was never measured.
/// <c>Results</c> holds measured numbers by name (for example "testAccuracy"); it is null for language models and is
/// left out of the file when null, so version 1 files are written exactly as before. Two infos are equal when their
/// results hold the same names and numbers, in any order.
/// </summary>
public sealed record ModelInfo(string Name, string TrainingTextName, int StepsTrained, double? ValidationLoss, int Seed, bool IsShipped,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyDictionary<string, double>? Results = null)
{
    public bool Equals(ModelInfo? other) =>
        other is not null
        && Name == other.Name && TrainingTextName == other.TrainingTextName && StepsTrained == other.StepsTrained
        && Nullable.Equals(ValidationLoss, other.ValidationLoss) && Seed == other.Seed && IsShipped == other.IsShipped
        && SameResults(Results, other.Results);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Name);
        hash.Add(TrainingTextName);
        hash.Add(StepsTrained);
        hash.Add(ValidationLoss);
        hash.Add(Seed);
        hash.Add(IsShipped);
        hash.Add(Results?.Count ?? -1); // order-free: only the count goes in
        return hash.ToHashCode();
    }

    private static bool SameResults(IReadOnlyDictionary<string, double>? a, IReadOnlyDictionary<string, double>? b)
    {
        if (a is null || b is null) return ReferenceEquals(a, b);
        if (a.Count != b.Count) return false;
        foreach (var (key, value) in a)
            if (!b.TryGetValue(key, out double other) || !value.Equals(other)) return false;
        return true;
    }
}
