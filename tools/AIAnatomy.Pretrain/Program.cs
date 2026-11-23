using System.Diagnostics;
using AIAnatomy.Core;
using AIAnatomy.Models.Inspection;
using AIAnatomy.Models.Language;
using AIAnatomy.Models.Tokenization;
using AIAnatomy.Models.Training;
using AIAnatomy.Models.Transformers;

// Builds the two models that ship with the app. Run in Release (about 120 ms per step):
//   dotnet run -c Release --project tools/AIAnatomy.Pretrain -- fables --text content/texts/aesop-fables.txt --out content/models/fables-small.aimodel --steps 3000 --seed 1
//   dotnet run -c Release --project tools/AIAnatomy.Pretrain -- induction --out content/models/induction-demo.aimodel --seed 1 [--steps 1500]

const string Usage =
    "Usage: pretrain fables --text <file> --out <file> --steps <n> [--seed <n>]\n" +
    "       pretrain induction --out <file> [--steps <n>] [--seed <n>]";

try
{
    if (args.Length == 0) return Fail(Usage);
    var options = ParseOptions(args.Skip(1).ToArray());
    return args[0] switch
    {
        "fables" => await TrainFablesAsync(options),
        "induction" => await TrainInductionAsync(options),
        _ => Fail(Usage),
    };
}
catch (Exception e) when (e is ArgumentException or InvalidDataException or IOException or FormatException)
{
    return Fail(e.Message);
}

static int Fail(string message)
{
    Console.Error.WriteLine(message);
    return 1;
}

static Dictionary<string, string> ParseOptions(string[] args)
{
    var options = new Dictionary<string, string>();
    for (int i = 0; i < args.Length; i += 2)
    {
        if (!args[i].StartsWith("--") || i + 1 >= args.Length) throw new ArgumentException($"Unexpected argument '{args[i]}'.\n{Usage}");
        options[args[i][2..]] = args[i + 1];
    }
    return options;
}

static string Required(Dictionary<string, string> o, string key)
    => o.TryGetValue(key, out var v) ? v : throw new ArgumentException($"Missing --{key}.\n{Usage}");

static int Number(Dictionary<string, string> o, string key, int fallback)
    => o.TryGetValue(key, out var v) ? int.Parse(v) : fallback;

static async Task<int> TrainFablesAsync(Dictionary<string, string> o)
{
    string text = TextNormalizer.Normalize(await File.ReadAllTextAsync(Required(o, "text")));
    string outPath = Required(o, "out");
    int steps = Number(o, "steps", 0);
    int seed = Number(o, "seed", 1);
    if (steps < 25) throw new ArgumentException("--steps must be at least 25 (validation runs every 25 steps).");

    var tokenizer = CharTokenizer.FromText(text);
    var model = new Transformer(new TransformerConfig(tokenizer.VocabularySize), new SeededRandom(seed));
    Console.WriteLine($"{text.Length} characters, {tokenizer.VocabularySize} tokens, {model.Config.CountParameters()} parameters");
    var trainer = new Trainer(model, tokenizer.Encode(text), new TrainingSettings(Seed: seed));
    var last = await RunAsync(trainer, steps);
    if (last.StoppedReason is not null) throw new InvalidDataException(last.StoppedReason);

    var info = new ModelInfo("Fables model", "Aesop's Fables (Townsend)", steps, trainer.LatestValidationLoss, seed, IsShipped: true);
    Save(new LanguageModel(model, tokenizer, info), outPath);
    return 0;
}

static async Task<int> TrainInductionAsync(Dictionary<string, string> o)
{
    string outPath = Required(o, "out");
    int steps = Number(o, "steps", InductionExperiment.DefaultSteps);
    int seed = Number(o, "seed", 1);
    var watch = Stopwatch.StartNew();
    var progress = new InlineProgress(r => { if (r.Step % 100 == 0) Console.WriteLine($"step {r.Step}: train {r.TrainLoss:F3}"); });
    var transformer = await InductionExperiment.TrainAsync(steps, seed, progress, CancellationToken.None);
    Console.WriteLine($"trained in {watch.Elapsed.TotalSeconds:F1} s");

    double best = 0;
    for (int layer = 0; layer < InductionExperiment.Config.Layers; layer++)
        for (int head = 0; head < InductionExperiment.Config.Heads; head++)
        {
            double score = InductionExperiment.InductionScore(transformer, layer, head, seed: 9);
            Console.WriteLine($"layer {layer + 1} head {head + 1}: induction score {score:F3}");
            if (layer == 1) best = Math.Max(best, score);
        }
    Console.WriteLine($"best layer-2 induction score {best:F3}");

    var info = new ModelInfo("Induction demo", "Repeated letter sequences", steps, null, seed, IsShipped: true);
    Save(new LanguageModel(transformer, InductionExperiment.Tokenizer, info), outPath);
    return 0;
}

static async Task<TrainingStepReport> RunAsync(Trainer trainer, int steps)
{
    var watch = Stopwatch.StartNew();
    TrainingStepReport last = new(0, double.NaN, null, null);
    var progress = new InlineProgress(r =>
    {
        last = r;
        if (r.Step % 100 == 0)
            Console.WriteLine($"step {r.Step}: train {r.TrainLoss:F3}, validation {(r.ValidationLoss is { } v ? v.ToString("F3") : "-")}, {watch.Elapsed.TotalSeconds:F0} s");
    });
    await trainer.RunAsync(steps, progress, CancellationToken.None);
    Console.WriteLine($"done: {trainer.StepsTaken} steps in {watch.Elapsed.TotalSeconds:F0} s, final validation loss {trainer.LatestValidationLoss:F3}");
    return last;
}

static void Save(LanguageModel model, string path)
{
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
    using (var stream = File.Create(path)) ModelFile.Save(model, stream);
    Console.WriteLine($"saved {path} ({new FileInfo(path).Length / 1024} KB)");
}

/// <summary>Reports on the training thread, in order (Progress&lt;T&gt; would post to the thread pool and can reorder).</summary>
sealed class InlineProgress(Action<TrainingStepReport> report) : IProgress<TrainingStepReport>
{
    public void Report(TrainingStepReport value) => report(value);
}
