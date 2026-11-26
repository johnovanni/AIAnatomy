using System.Diagnostics;
using AIAnatomy.Core;
using AIAnatomy.Models.Diffusion;
using AIAnatomy.Models.Inspection;
using AIAnatomy.Models.Language;
using AIAnatomy.Models.Recurrent;
using AIAnatomy.Models.Tokenization;
using AIAnatomy.Models.Training;
using AIAnatomy.Models.Vision;
using AIAnatomy.Models.Transformers;

// Builds the two models that ship with the app. Run in Release (the fables model: 3,000 steps in 517 s with its validation checks, about 0.17 s a step, D20):
//   dotnet run -c Release --project tools/AIAnatomy.Pretrain -- fables --text content/texts/aesop-fables.txt --out content/models/fables-small.aimodel --steps 3000 --seed 1
//   dotnet run -c Release --project tools/AIAnatomy.Pretrain -- induction --out content/models/induction-demo.aimodel --seed 1 [--steps 1500]
// Builds the family networks that ship in content/models/family (Release; the memory network takes about 5 minutes, each diffusion network about 4):
//   dotnet run -c Release --project tools/AIAnatomy.Pretrain -- memory --text content/texts/aesop-fables.txt --model content/models/fables-small.aimodel --out content/models/family/memory.aimodel --seed 1
//   dotnet run -c Release --project tools/AIAnatomy.Pretrain -- diffusion --shape Spiral --out content/models/family/diffusion-spiral.aimodel --seed 1
//   dotnet run -c Release --project tools/AIAnatomy.Pretrain -- digits --train content/data/digits/digits-train.bin.gz --test content/data/digits/digits-test.bin.gz --out content/models/family/digits.aimodel --seed 1
// Builds the handwritten-digits subsets in content/data/digits from the UCI optdigits files (kept outside the repository):
//   dotnet run -c Release --project tools/AIAnatomy.Pretrain -- digits-subset --uci <optdigits.tra> --out content/data/digits/digits-train.bin.gz
//   dotnet run -c Release --project tools/AIAnatomy.Pretrain -- digits-subset --uci <optdigits.tes> --out content/data/digits/digits-test.bin.gz
// Times training steps of a family network (Release, from the repository root), to choose sizes and steps:
//   dotnet run -c Release --project tools/AIAnatomy.Pretrain -- speed --kind memory --hidden 128 [--steps 50]
//   dotnet run -c Release --project tools/AIAnatomy.Pretrain -- speed --kind diffusion --hidden 128 [--steps 50] [--shape Spiral]
//   dotnet run -c Release --project tools/AIAnatomy.Pretrain -- speed --kind digits --config 8,16,64 [--images 1600] [--batch 32] [--seed 1]

const int WarmUpSteps = 30;   // speed: untimed steps first (see TimeSteps)

const string Usage =
    "Usage: pretrain fables --text <file> --out <file> --steps <n> [--seed <n>]\n" +
    "       pretrain induction --out <file> [--steps <n>] [--seed <n>]\n" +
    "       pretrain memory --text <file> --model <fables file> --out <file> [--steps <n>] [--seed <n>]\n" +
    "       pretrain digits --train <file> --test <file> --out <file> [--steps <n>] [--seed <n>]\n" +
    "       pretrain diffusion --shape <preset> --out <file> [--steps <n>] [--seed <n>]\n" +
    "       pretrain digits-subset --uci <optdigits.tra or .tes> --out <file>\n" +
    "       pretrain speed --kind memory --hidden <n> [--steps <n>] [--text <file>] [--model <file>]\n" +
    "       pretrain speed --kind diffusion --hidden <n> [--steps <n>] [--shape <preset>]\n" +
    "       pretrain speed --kind digits --config <f1,f2,hidden> [--images <n>] [--batch <n>] [--seed <n>] [--train <file>] [--test <file>]";

try
{
    if (args.Length == 0) return Fail(Usage);
    var options = ParseOptions(args.Skip(1).ToArray());
    return args[0] switch
    {
        "fables" => await TrainFablesAsync(options),
        "induction" => await TrainInductionAsync(options),
        "memory" => await TrainMemoryAsync(options),
        "diffusion" => TrainDiffusion(options),
        "digits" => TrainDigits(options),
        "digits-subset" => DigitsSubset(options),
        "speed" => await SpeedAsync(options),
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

// UCI "Optical Recognition of Handwritten Digits" (optdigits.tra / optdigits.tes): one row per image, 64 comma-separated
// values 0-16 (8x8, row by row) then the label 0-9. Takes the first N images of each digit in file order, where N is the
// smallest per-digit count in the file, so the subset is exactly balanced and deterministic. Values are scaled to 0-255
// as round(v * 255 / 16).
static int DigitsSubset(Dictionary<string, string> o)
{
    string input = Required(o, "uci");
    string outPath = Required(o, "out");
    const int Size = 8, Pixels = Size * Size;
    var perDigit = Enumerable.Range(0, 10).Select(_ => new List<byte[]>()).ToArray();
    int lineNumber = 0;
    foreach (string line in File.ReadLines(input))
    {
        lineNumber++;
        if (line.Trim().Length == 0) continue;
        var parts = line.Split(',');
        if (parts.Length != Pixels + 1) throw new InvalidDataException($"'{Path.GetFileName(input)}' line {lineNumber}: expected {Pixels + 1} values, found {parts.Length}.");
        int label = int.Parse(parts[Pixels]);
        if (label is < 0 or > 9) throw new InvalidDataException($"'{Path.GetFileName(input)}' line {lineNumber}: label {label} is outside 0-9.");
        var pixels = new byte[Pixels];
        for (int i = 0; i < Pixels; i++)
        {
            int v = int.Parse(parts[i]);
            if (v is < 0 or > 16) throw new InvalidDataException($"'{Path.GetFileName(input)}' line {lineNumber}: value {v} is outside 0-16.");
            pixels[i] = (byte)((v * 255 + 8) / 16);   // round(v * 255 / 16), integer arithmetic
        }
        perDigit[label].Add(pixels);
    }
    int n = perDigit.Min(l => l.Count);
    if (n == 0) throw new InvalidDataException($"'{Path.GetFileName(input)}' has no images for some digit.");
    Console.WriteLine("per digit in file: " + string.Join(", ", perDigit.Select((l, d) => $"{d}:{l.Count}")));
    var images = new List<(byte Label, byte[] Pixels)>();
    for (int d = 0; d < 10; d++)
        foreach (var px in perDigit[d].Take(n)) images.Add(((byte)d, px));
    DigitSet.Save(outPath, Size, images);
    Console.WriteLine($"kept the first {n} of each digit: {images.Count} images, saved {outPath} ({new FileInfo(outPath).Length} bytes)");
    return 0;
}

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

// The shipped memory network. Timed over the whole training loop with validation at step 1 and every 25 steps, exactly as
// the Fables run's 517 s was (Trainer.RunAsync), so trainSeconds and charactersPerSecond compare like for like.
static async Task<int> TrainMemoryAsync(Dictionary<string, string> o)
{
    string text = await File.ReadAllTextAsync(Required(o, "text"));
    string outPath = Required(o, "out");
    int steps = Number(o, "steps", MemoryTraining.ShippedSteps);
    int seed = Number(o, "seed", 1);
    if (steps < 25 || steps % 25 != 0) throw new ArgumentException("--steps must be a multiple of 25 (validation runs every 25 steps, and the last step must be one).");
    RequireRelease();
    LanguageModel fables;
    using (var stream = File.OpenRead(Required(o, "model"))) fables = ModelFile.Load(stream);

    var source = MemoryTraining.FablesSource(fables.Tokenizer, text);
    var rnn = new ElmanRnn(MemoryTraining.Shipped(fables.Tokenizer.VocabularySize), new SeededRandom(seed));
    var trainer = new RnnTrainer(rnn, source, steps, seed);
    Console.WriteLine($"memory network: hidden {rnn.Config.Hidden}, {rnn.Config.CountParameters()} parameters, {steps} steps, batch {RnnTrainer.DefaultBatchSize}, context {MemoryTraining.ContextLength}");

    double validation = double.NaN;
    var watch = Stopwatch.StartNew();
    for (int step = 1; step <= steps; step++)
    {
        double loss = trainer.Step();
        if (!double.IsFinite(loss)) throw new InvalidDataException(SteppedTraining.DivergedMessage);
        if (step == 1 || step % 25 == 0) validation = trainer.ValidationLoss();
        if (step % 100 == 0) Console.WriteLine($"step {step}: train {loss:F3}, validation {validation:F3}, {watch.Elapsed.TotalSeconds:F0} s");
    }
    double seconds = watch.Elapsed.TotalSeconds;
    double charactersPerSecond = (double)steps * RnnTrainer.DefaultBatchSize * MemoryTraining.ContextLength / seconds;
    Console.WriteLine($"done: {steps} steps in {seconds:F0} s ({charactersPerSecond:F0} characters per second), final validation loss {validation:F3}");

    var info = new ModelInfo("Memory network", "Aesop's Fables (Townsend)", steps, validation, seed, IsShipped: true,
        Results: new Dictionary<string, double>
        {
            ["validationLoss"] = validation,
            ["trainSeconds"] = seconds,
            ["charactersPerSecond"] = charactersPerSecond,
        });
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath))!);
    using (var stream = File.Create(outPath)) rnn.Save(stream, fables.Tokenizer, info);
    Console.WriteLine($"saved {outPath} ({new FileInfo(outPath).Length / 1024} KB)");
    return 0;
}

static int TrainDigits(Dictionary<string, string> o)
{
    string trainPath = Required(o, "train");
    string testPath = Required(o, "test");
    string outPath = Required(o, "out");
    int steps = Number(o, "steps", DigitTraining.ShippedSteps);
    int seed = Number(o, "seed", 1);
    RequireRelease();
    var train = DigitSet.Load(trainPath);
    var test = DigitSet.Load(testPath);
    var config = DigitTraining.Shipped;
    if (train.ImageSize != config.ImageSize || test.ImageSize != config.ImageSize)
        throw new ArgumentException($"The shipped digits network reads {config.ImageSize}x{config.ImageSize} images; the files hold {train.ImageSize}x{train.ImageSize} and {test.ImageSize}x{test.ImageSize}.");

    var net = new DigitNet(config, new SeededRandom(seed));
    var trainer = new DigitTrainer(net, train, steps, seed);
    Console.WriteLine($"digits network: filters {config.Filters1}, {config.Filters2}, hidden {config.Hidden}, {net.Config.CountParameters()} parameters, {steps} steps, {train.Count} training and {test.Count} test images");

    var watch = Stopwatch.StartNew();
    for (int step = 1; step <= steps; step++)
    {
        double loss = trainer.Step();
        if (!double.IsFinite(loss)) throw new InvalidDataException(SteppedTraining.DivergedMessage);
        if (step % 500 == 0) Console.WriteLine($"step {step}: train {loss:F4}, {watch.Elapsed.TotalSeconds:F0} s");
    }
    double seconds = watch.Elapsed.TotalSeconds;

    double accuracy = DigitTrainer.Accuracy(net, test);
    Console.WriteLine($"done: {steps} steps in {seconds:F1} s, test accuracy {accuracy:F4} on {test.Count} images");

    var info = new ModelInfo("Handwritten digits network", "UCI 8x8 optical digits (bundled subset)", steps, null, seed, IsShipped: true,
        Results: new Dictionary<string, double> { ["testAccuracy"] = accuracy, ["trainSeconds"] = seconds });
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath))!);
    using (var stream = File.Create(outPath)) net.Save(stream, info);
    Console.WriteLine($"saved {outPath} ({new FileInfo(outPath).Length / 1024} KB)");
    return 0;
}

static int TrainDiffusion(Dictionary<string, string> o)
{
    string shapeName = Required(o, "shape");
    string outPath = Required(o, "out");
    int steps = Number(o, "steps", DiffusionTraining.ShippedSteps);
    int seed = Number(o, "seed", 1);
    RequireRelease();
    var preset = Shapes.Presets.FirstOrDefault(p => p.Name == shapeName);
    if (preset.Points is null)
        throw new ArgumentException($"Unknown --shape '{shapeName}' (use {string.Join(", ", Shapes.Presets.Select(p => p.Name))}).");

    var schedule = new NoiseSchedule();
    var net = new DiffusionNet(DiffusionTraining.Shipped, new SeededRandom(seed));
    var trainer = new DiffusionTrainer(net, schedule, preset.Points, steps, seed);
    Console.WriteLine($"diffusion network ({shapeName}): hidden {net.Config.Hidden}, {net.Config.CountParameters()} parameters, {steps} steps");

    var watch = Stopwatch.StartNew();
    for (int step = 1; step <= steps; step++)
    {
        double loss = trainer.Step();
        if (!double.IsFinite(loss)) throw new InvalidDataException(SteppedTraining.DivergedMessage);
        if (step % 5000 == 0) Console.WriteLine($"step {step}: train {loss:F4}, {watch.Elapsed.TotalSeconds:F0} s");
    }
    double seconds = watch.Elapsed.TotalSeconds;

    var result = DiffusionSampler.Sample(net, schedule, 500, new SeededRandom(1))[^1];
    var samples = Enumerable.Range(0, result.GetLength(0)).Select(i => (result[i, 0], result[i, 1])).ToList();
    double distance = Shapes.MeanDistanceToShape(samples, preset.Points);
    double coverage = Shapes.Coverage(samples, preset.Points, 0.05);
    Console.WriteLine($"done: {steps} steps in {seconds:F0} s, mean distance {distance:F4}, coverage at 0.05 {coverage:F3}");

    var info = new ModelInfo($"{shapeName} diffusion network", $"Generated shape: {shapeName}", steps, null, seed, IsShipped: true,
        Results: new Dictionary<string, double> { ["meanDistance"] = distance, ["coverage"] = coverage, ["trainSeconds"] = seconds });
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath))!);
    using (var stream = File.Create(outPath)) net.Save(stream, schedule, shapeName, info);
    Console.WriteLine($"saved {outPath} ({new FileInfo(outPath).Length / 1024} KB)");
    return 0;
}

// Each kind of network builds its own stepped trainer; the timing is shared.
static async Task<int> SpeedAsync(Dictionary<string, string> o)
{
    string kind = Required(o, "kind");
    var trainer = kind switch
    {
        "memory" => await MemorySpeedTrainerAsync(o),
        "diffusion" => DiffusionSpeedTrainer(o),
        "digits" => DigitsSpeedTrainer(o),
        _ => throw new ArgumentException($"Unknown --kind '{kind}' (use memory, diffusion or digits).\n{Usage}"),
    };
    double msPerStep = TimeSteps(trainer.Trainer, trainer.TimedSteps ?? Number(o, "steps", 50), trainer.Plans);
    trainer.AfterTiming?.Invoke(msPerStep);
    return 0;
}

static async Task<(ISteppedTrainer Trainer, (string Name, int Steps)[] Plans, int? TimedSteps, Action<double>? AfterTiming)> MemorySpeedTrainerAsync(Dictionary<string, string> o)
{
    int hidden = Number(o, "hidden", 0);
    if (hidden < 1) throw new ArgumentException("--hidden must be at least 1.");
    string text = await File.ReadAllTextAsync(o.TryGetValue("text", out var t) ? t : "content/texts/aesop-fables.txt");
    LanguageModel fables;
    using (var stream = File.OpenRead(o.TryGetValue("model", out var m) ? m : "content/models/fables-small.aimodel")) fables = ModelFile.Load(stream);

    var source = MemoryTraining.FablesSource(fables.Tokenizer, text);
    var rnn = new ElmanRnn(new RnnConfig(fables.Tokenizer.VocabularySize, hidden), new SeededRandom(1));
    var trainer = new RnnTrainer(rnn, source, int.MaxValue, seed: 1);
    Console.WriteLine($"memory network: hidden {hidden}, {rnn.Config.CountParameters()} parameters, batch {RnnTrainer.DefaultBatchSize}, context {MemoryTraining.ContextLength}");
    return (trainer,
        new[] { ("retrain", MemoryTraining.RetrainSteps), ("shipped", MemoryTraining.ShippedSteps) }, null,
        _ => Console.WriteLine($"validation loss after {trainer.StepsTaken} steps: {trainer.ValidationLoss():F3}"));
}

static (ISteppedTrainer Trainer, (string Name, int Steps)[] Plans, int? TimedSteps, Action<double>? AfterTiming) DiffusionSpeedTrainer(Dictionary<string, string> o)
{
    int hidden = Number(o, "hidden", 0);
    if (hidden < 1) throw new ArgumentException("--hidden must be at least 1.");
    string shapeName = o.TryGetValue("shape", out var n) ? n : "Spiral";
    var preset = Shapes.Presets.FirstOrDefault(p => p.Name == shapeName);
    if (preset.Points is null)
        throw new ArgumentException($"Unknown --shape '{shapeName}' (use {string.Join(", ", Shapes.Presets.Select(p => p.Name))}).");

    var schedule = new NoiseSchedule();
    var net = new DiffusionNet(new DiffusionConfig(hidden), new SeededRandom(1));
    var trainer = new DiffusionTrainer(net, schedule, preset.Points, int.MaxValue, seed: 1);
    Console.WriteLine($"diffusion network: hidden {hidden}, {net.Config.CountParameters()} parameters, batch 128, {schedule.Steps} noise steps, shape {shapeName}");
    return (trainer,
        new[] { ("retrain", DiffusionTraining.RetrainSteps), ("shipped", DiffusionTraining.ShippedSteps) }, null,
        _ =>
        {
            var result = DiffusionSampler.Sample(net, schedule, 1000, new SeededRandom(2))[^1];
            var samples = Enumerable.Range(0, result.GetLength(0)).Select(i => (result[i, 0], result[i, 1])).ToList();
            Console.WriteLine($"mean distance to the shape of 1,000 samples after {trainer.StepsTaken} steps: {Shapes.MeanDistanceToShape(samples, preset.Points):F3}");
        });
}

// Times forward plus backward (and the Adam step) of the image network on batches of the bundled training digits.
// --images is how many images are timed after the warm-up (rounded up to whole batches); the report gives ms per image,
// the time for one pass over the training images, and the test accuracy after all the steps taken (warm-up included).
static (ISteppedTrainer Trainer, (string Name, int Steps)[] Plans, int? TimedSteps, Action<double>? AfterTiming) DigitsSpeedTrainer(Dictionary<string, string> o)
{
    var parts = Required(o, "config").Split(',');
    if (parts.Length != 3) throw new ArgumentException("--config must be three numbers: filters1,filters2,hidden (for example 8,16,64).");
    var numbers = parts.Select(int.Parse).ToArray();
    int batch = Number(o, "batch", DigitTrainer.DefaultBatchSize);
    if (batch < 1) throw new ArgumentException("--batch must be at least 1.");
    int images = Number(o, "images", 50 * batch);
    if (images < 1) throw new ArgumentException("--images must be at least 1.");
    var train = DigitSet.Load(o.TryGetValue("train", out var tr) ? tr : "content/data/digits/digits-train.bin.gz");
    var test = DigitSet.Load(o.TryGetValue("test", out var te) ? te : "content/data/digits/digits-test.bin.gz");

    var config = new DigitNetConfig(train.ImageSize, numbers[0], numbers[1], numbers[2]);
    int seed = Number(o, "seed", 1);
    var net = new DigitNet(config, new SeededRandom(seed));
    var trainer = new DigitTrainer(net, train, int.MaxValue, seed, batchSize: batch);
    int steps = (images + batch - 1) / batch;
    Console.WriteLine($"image network: {train.ImageSize}x{train.ImageSize}, filters {config.Filters1} and {config.Filters2}, hidden {config.Hidden}, " +
        $"{config.CountParameters()} parameters, batch {batch}; timing {steps} steps ({steps * batch} images)");
    return (trainer,
        new[] { ("retrain", DigitTraining.RetrainSteps), ("shipped", DigitTraining.ShippedSteps) }, steps,
        msPerStep =>
        {
            double msPerImage = msPerStep / batch;
            Console.WriteLine($"{msPerImage:F3} ms per image (forward, backward and Adam); one pass over the {train.Count} training images about {msPerImage * train.Count / 1000:F1} s");
            Console.WriteLine($"test accuracy after {trainer.StepsTaken} steps: {DigitTrainer.Accuracy(net, test):F4} on {test.Count} images");
        });
}

static bool IsOptimised()
{
    var debuggable = typeof(ISteppedTrainer).Assembly.GetCustomAttributes(typeof(DebuggableAttribute), false)
        .OfType<DebuggableAttribute>().FirstOrDefault();
    return !(debuggable?.IsJITOptimizerDisabled ?? false);
}

// A shipped file records its training time, so a Debug run must not write one.
static void RequireRelease()
{
    if (!IsOptimised()) throw new ArgumentException("This is not an optimised (Release) build: its times would be wrong in a shipped file. Run with -c Release.");
}

static double TimeSteps(ISteppedTrainer trainer, int steps, (string Name, int Steps)[] plans)
{
    if (steps < 1) throw new ArgumentException("--steps must be at least 1.");
    if (!IsOptimised())
        Console.WriteLine("WARNING: this is not an optimised (Release) build, so these times are not the app's. Do not log them; run with -c Release.");
    // Warm up past .NET's tiered compilation: a method called about 30 times is recompiled with full optimisation.
    for (int i = 0; i < WarmUpSteps; i++) trainer.Step();
    var watch = Stopwatch.StartNew();
    double loss = double.NaN;
    for (int i = 0; i < steps; i++) loss = trainer.Step();
    watch.Stop();
    double ms = watch.Elapsed.TotalMilliseconds / steps;
    Console.WriteLine($"{WarmUpSteps} warm-up steps, then {steps} steps in {watch.Elapsed.TotalSeconds:F2} s: {ms:F2} ms per step (last train loss {loss:F3})");
    foreach (var (name, planned) in plans)
        Console.WriteLine($"  {name}: {planned} steps would take about {ms * planned / 1000:F0} s at this size");
    return ms;
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
