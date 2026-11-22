namespace AIAnatomy.Models.Transformers;

/// <summary>Interventions a lab can apply during one forward pass.</summary>
public sealed record ForwardOptions
{
    private static readonly IReadOnlySet<(int Layer, int Head)> NoHeads = new HashSet<(int, int)>();

    /// <summary>Heads (layer, head) whose output is replaced by zeros.</summary>
    public IReadOnlySet<(int Layer, int Head)> DisabledHeads { get; init; } = NoHeads;

    /// <summary>Leave out position information: the position vectors become zeros.</summary>
    public bool RemovePositions { get; init; }

    public static ForwardOptions None { get; } = new();
}
