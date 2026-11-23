namespace AIAnatomy.Presentation.Tests;

/// <summary>
/// Tests that train a model use all cores, so they run one at a time, apart from the other tests.
/// Mark a test class with <c>[Collection(TrainingCollection.Name)]</c>.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class TrainingCollection
{
    public const string Name = "Training";
}
