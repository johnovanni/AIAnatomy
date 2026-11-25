namespace AIAnatomy.Models.Rules;

/// <summary>
/// The default rules, in the style of the textbook animal-identification expert system, and eight animals
/// described by the facts that are true of them. The bat is the one the rules never considered.
/// </summary>
public static class AnimalRules
{
    public static IReadOnlyList<string> DefaultRuleTexts { get; } = new[]
    {
        "if has hair then mammal",
        "if gives milk then mammal",
        "if has feathers then bird",
        "if flies and lays eggs then bird",
        "if mammal and eats meat then carnivore",
        "if mammal and has pointed teeth and has claws and has forward-facing eyes then carnivore",
        "if mammal and has hooves then ungulate",
        "if mammal and chews cud then ungulate",
        "if carnivore and tawny and dark spots then cheetah",
        "if carnivore and tawny and black stripes then tiger",
        "if ungulate and long neck and long legs and dark spots then giraffe",
        "if ungulate and black stripes then zebra",
        "if bird and not flies and long neck and long legs and black-and-white then ostrich",
        "if bird and not flies and swims and black-and-white then penguin",
        "if bird and good flier then albatross",
    };

    public static IReadOnlyList<(string Animal, IReadOnlySet<string> Yes)> Descriptions { get; } = new (string, IReadOnlySet<string>)[]
    {
        ("cheetah", Set("has hair", "eats meat", "tawny", "dark spots")),
        ("tiger", Set("has hair", "eats meat", "tawny", "black stripes")),
        ("giraffe", Set("has hair", "has hooves", "long neck", "long legs", "dark spots")),
        ("zebra", Set("has hair", "has hooves", "black stripes")),
        ("ostrich", Set("has feathers", "long neck", "long legs", "black-and-white")),
        ("penguin", Set("has feathers", "swims", "black-and-white")),
        ("albatross", Set("has feathers", "flies", "lays eggs", "good flier")),
        ("bat", Set("has hair", "gives milk", "flies")),
    };

    private static IReadOnlySet<string> Set(params string[] facts) => new HashSet<string>(facts);
}
