# AIAnatomy Stage 1 (Map, Parts, Whole) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the AIAnatomy WPF app's shell, its from-scratch C# AI engine, and the Map, Parts and Whole sections, which teach a tiny real transformer from start to finish.

**Architecture:** Six projects with one-way dependencies: App (WPF views) → Presentation (WPF-free view-models) → Lessons (lesson content) → Models (transformer parts, toys, inspection) → Core (tensors, automatic differentiation, optimisers). Every lab reads real results from a `ForwardTrace` that the model itself produces. No lab recomputes the math, so nothing on screen is faked.

**Tech Stack:** C# / .NET 10 (`net10.0` for libraries, `net10.0-windows10.0.19041.0` for the app), WPF, CommunityToolkit.Mvvm 8.4.0, WpfMath 2.1.0 (MIT, formula drawing only), xUnit 2.9.3.

**Spec:** `docs/superpowers/specs/2026-11-22-aianatomy-design.md`. Read it before starting any task.

## Global Constraints

- Real computation everywhere. Every value shown on screen comes from running code. No fake animations.
- All AI math is our own code. No ML libraries. The only runtime packages are CommunityToolkit.Mvvm and WpfMath.
- No sound anywhere.
- Deterministic: every random choice goes through `SeededRandom`. The seed is shown in the lab and can be changed.
- Responsiveness: each lab responds within about 1 second. Training runs in the background with live progress and pause/stop, and the UI never freezes.
- Text size: default 20 pt body and 28 pt headings, adjustable from 16 to 32 pt. Light and dark themes. Full keyboard use. Colour is never the only carrier of meaning.
- Visual checks only through `--render-png` (rendering with `RenderTargetBitmap`). Never capture the screen. Keep app launches few, because of the owner's antivirus.
- Lesson claims are textbook material or cite an entry in `content/sources.json`. Claims about Claude are limited to what Anthropic has published. Unknowns are stated as unknown.
- Bundled texts and data are public domain or openly licensed, and listed in `THIRD-PARTY-NOTICES.txt`.
- Git: local only. Work on branch `stage-1`. Never push or create a GitHub repository.
- Numbers: `double` throughout (decision D1 below).
- Decisions made during the build are added to `docs/decisions.md` with a reason.

## Decisions made while planning (copy into `docs/decisions.md` in Task 1)

- **D1** Tensors store `double`, not `float`. Finite-difference gradient checks are then exact enough to trust, and the speed cost is small for scalar C#. Lesson text notes that real models use 16- or 32-bit numbers.
- **D2** A WPF-free `AIAnatomy.Presentation` project holds the view-models, so they can be unit-tested. This refines the spec's App → Lessons chain to App → Presentation → Lessons.
- **D3** Pre-layer-norm blocks as in GPT-2 (`x + Attn(LN1(x))`, then `x + FF(LN2(x))`, then a final LN), with a separate (untied) output matrix, which is easier to explain as "the output layer".
- **D4** The BPE tokenizer works on characters, not bytes, so its pieces stay readable. Words are pre-split with the leading space attached (" the"), as in GPT-2.
- **D5** "Rules vs. learning" task: predict the next letter. A readable list of hand-written rules is compared with a bigram model (a table of next-letter scores) that learns from the fables in a few seconds.
- **D6** The Family and Beyond tabs exist in Stage 1, each showing a "Coming in a later stage" page that lists what it will contain.
- **D7** The training text is *Aesop's Fables* (George Fyler Townsend translation, Project Gutenberg eBook #21, public domain in the US). The Project Gutenberg header, footer and licence are removed, as their terms require when the trademark isn't used. Downloading it needs the owner's okay (asked at plan review). If it is refused, the builder writes 40 short original fables instead.
- **D8** Each forward pass works on one sequence (`[T, D]` tensors). A training batch sums the losses of B sequences. This keeps traces simple to show.

## Review Focus

1. **Characters the model has never seen** (emoji, accents, tabs, curly quotes) typed into any lab must map to the unknown token, be shown as `�`, and never crash. Tested in Task 5 and Task 14.
2. **Input longer than the context length** must use only the last `ContextLength` tokens, and the lab must say so on screen. Tested in Task 6 and Task 14.
3. **Pasted training text that is too short or too large** (shorter than `ContextLength + 2` characters, or over 2,000,000 characters) must be refused with a plain message, not crash or hang. Tested in Task 7.
4. **Learning rate high enough to diverge** must stop training with the message "Training stopped: the loss became too large (try a smaller learning rate)", and must not save a broken model. Tested in Task 7.
5. **A corrupt or incompatible model file** in the user's model folder must be listed as unreadable and skipped, and the app must still start. A corrupt settings file must give the defaults. Tested in Task 7 and Task 12.

---

## File map

```
AIAnatomy.slnx, Directory.Build.props, .gitignore, README.md, THIRD-PARTY-NOTICES.txt
src/AIAnatomy.Core/           Tensor.cs, Ops.*.cs (Elementwise, Matrix, Reductions, Neural), Parameter.cs,
                              SeededRandom.cs, Optimizers/ (IOptimizer, Sgd, Adam), Losses.cs, GradCheck.cs
src/AIAnatomy.Models/         Tokenization/ (ITokenizer, CharTokenizer, BpeTokenizer)
                              Transformer/ (TransformerConfig, Module, TokenEmbedding, IPositionEncoding,
                                LearnedPositions, SinusoidalPositions, AttentionHead, MultiHeadAttention,
                                FeedForward, LayerNormModule, TransformerBlock, Transformer, ForwardOptions,
                                ForwardTrace)
                              Language/ (LanguageModel, Sampling, ModelFile, ModelLibrary)
                              Training/ (Trainer, TrainingSettings, TrainingStepReport, CorpusValidator)
                              Inspection/ (Pca, LogitLens, NeuronProbe, InductionExperiment)
                              Toys/ (Perceptron, LossSurface, ExpressionGraphDemo, RuleBasedWriter, BigramModel)
src/AIAnatomy.Lessons/        Lesson.cs, LessonCatalog.cs, Markdown/ (MarkdownDocument, MarkdownParser), Sources.cs
src/AIAnatomy.Presentation/   Services/ (IActiveModelService, ActiveModelService, ISettingsService,
                                SettingsService, AppPaths), MainViewModel.cs, LabViewModel.cs, LabFactory.cs,
                              Labs/ (one view-model per lab)
src/AIAnatomy.App/            App.xaml, MainWindow.xaml, Themes/ (Light, Dark, Sizes), Controls/ (HeatMap,
                                VectorStrip, ScatterPlot, ProbabilityBars, LineChart, NetworkDiagram,
                                PipelineView, MarkdownView), Views/ (one view per lab), Diagnostics/
tools/AIAnatomy.Pretrain/     Program.cs
content/                      lessons/<section>/<nn>-<id>.md, sources.json, texts/aesop-fables.txt,
                              models/fables-small.aimodel, models/induction-demo.aimodel
tests/                        AIAnatomy.Core.Tests, AIAnatomy.Models.Tests, AIAnatomy.Lessons.Tests,
                              AIAnatomy.Presentation.Tests
docs/                         decisions.md, final-check.md
```

---

### Task 1: Solution scaffold, `Tensor` and `SeededRandom`

**Files:**
- Create: `AIAnatomy.slnx`, `Directory.Build.props` (Version 0.1.0, Nullable enable, ImplicitUsings enable, as in ASLCoach), `.gitignore` (bin, obj, .vs, *.user, private/), `README.md` (one paragraph), `docs/decisions.md` (D1–D8 above)
- Create: all six `src` projects and four `tests` projects (test csproj as in `C:\repos\ASLCoach\tests\ASLCoach.Core.Tests\ASLCoach.Core.Tests.csproj`, with the same package versions and `<Using Include="Xunit" />`). The App project targets `net10.0-windows10.0.19041.0` with `UseWPF`. Every other project targets `net10.0`.
- Create: `src/AIAnatomy.Core/Tensor.cs`, `src/AIAnatomy.Core/SeededRandom.cs`
- Test: `tests/AIAnatomy.Core.Tests/TensorTests.cs`
- Also: add the line `private/` to `.git/info/exclude`, and create branch `stage-1`

**Interfaces — Produces:**
```csharp
namespace AIAnatomy.Core;
public sealed class Tensor {
    public int[] Shape { get; }            // e.g. [T, D]
    public int Size { get; }               // product of Shape
    public double[] Data { get; }          // row-major
    public double[]? Grad { get; }         // null until Backward reaches it
    public bool RequiresGrad { get; }
    public string Op { get; }              // "leaf", "add", "matmul", …
    public IReadOnlyList<Tensor> Parents { get; }
    public string? Label { get; set; }
    public double this[params int[] index] { get; set; }
    public static Tensor Zeros(params int[] shape);
    public static Tensor FromArray(double[] data, params int[] shape);   // copies data
    public static Tensor Scalar(double value);
    public static Tensor RandomNormal(SeededRandom rng, double std, params int[] shape);
    public Tensor AsParameter();           // copy with RequiresGrad = true
    public double Item();                  // value of a 1-element tensor
    public void Backward();                // needs Size == 1; seeds grad 1; topological order; accumulates
    public void ZeroGrad();
    public double[] Row(int r);            // copy of row r of a 2-D tensor
}
public sealed class SeededRandom {
    public SeededRandom(int seed);
    public int Seed { get; }
    public double NextDouble();            // [0,1)
    public int NextInt(int maxExclusive);
    public double NextGaussian();          // Box–Muller
}
```
`Backward` and the internal constructor `Tensor(double[] data, int[] shape, string op, Tensor[] parents, Action<Tensor> backward)` are used by the `Ops` classes in Task 2. The constructor is `internal`, with `InternalsVisibleTo` set for the Core tests.

- [ ] **Step 1: Write failing tests** `TensorTests`: `FromArray_RowMajorIndexing` (`FromArray([1,2,3,4,5,6], 2,3)[1,0] == 4`); `Zeros_HasSizeAndZeros`; `FromArray_WrongLength_Throws` (`ArgumentException`); `SeededRandom_SameSeedSameSequence` (two generators with seed 7 give 100 identical `NextDouble`s); `RandomNormal_HasRoughStd` (10,000 values with std 0.02 → sample std within 0.0185–0.0215); `Backward_OnNonScalar_Throws` (`InvalidOperationException`).
- [ ] **Step 2: Run** `dotnet test tests/AIAnatomy.Core.Tests`. Expected: FAIL (types missing).
- [ ] **Step 3: Implement** the scaffold, `Tensor` and `SeededRandom` (wrap `System.Random(seed)`).
- [ ] **Step 4: Run** `dotnet build AIAnatomy.slnx` then `dotnet test tests/AIAnatomy.Core.Tests`. Expected: build succeeds with 0 warnings. PASS.
- [ ] **Step 5: Commit** `git add -A && git commit -m "Scaffold solution; add Tensor and SeededRandom"`

### Task 2: Automatic differentiation for elementwise, matrix and reduction operations

**Files:**
- Create: `src/AIAnatomy.Core/Ops.Elementwise.cs`, `Ops.Matrix.cs`, `Ops.Reductions.cs`, `GradCheck.cs`
- Test: `tests/AIAnatomy.Core.Tests/OpsGradientTests.cs`

**Interfaces — Produces** (`public static partial class Ops`):
```csharp
Tensor Add(Tensor a, Tensor b);        // same shape, or b of shape [D] broadcast over rows of a [N,D]
Tensor Sub(Tensor a, Tensor b);  Tensor Mul(Tensor a, Tensor b);  // same broadcasting as Add
Tensor Scale(Tensor a, double s);
Tensor MatMul(Tensor a, Tensor b);     // [N,K]·[K,M] → [N,M]
Tensor Transpose(Tensor a);            // 2-D
Tensor Sum(Tensor a);  Tensor Mean(Tensor a);   // → scalar
Tensor SliceColumns(Tensor a, int start, int count);   // [N,D] → [N,count]
Tensor ConcatColumns(IReadOnlyList<Tensor> parts);     // [N,d_i] → [N,Σd_i]
Tensor Rows(Tensor table, IReadOnlyList<int> ids);     // gather rows: [V,D] → [n,D] (embedding lookup)
```
```csharp
public static class GradCheck {
    // compares analytic Grad with central differences (h = 1e-5); returns max relative error
    public static double MaxRelativeError(Func<Tensor[], Tensor> scalarFn, params Tensor[] inputs);
}
```
- [ ] **Step 1: Write failing tests.** `[Theory]` over every op above: random inputs (seed 1, shapes 3×4 and 4×2) → `GradCheck.MaxRelativeError(...) < 1e-6`. Plus value tests: `MatMul_KnownValues` (`[[1,2],[3,4]]·[[5],[6]] == [[17],[39]]`); `Rows_RepeatedId_AccumulatesGradient` (ids `[1,1]`, loss = `Sum` → grad of row 1 is all 2s); `Add_BroadcastsBiasOverRows`; `Backward_TwiceWithoutZeroGrad_Accumulates`.
- [ ] **Step 2: Run** `dotnet test tests/AIAnatomy.Core.Tests --filter OpsGradientTests`. Expected: FAIL.
- [ ] **Step 3: Implement** each op as a forward value plus a backward closure that adds into the parents' `Grad`.
- [ ] **Step 4: Run** the same command. Expected: PASS.
- [ ] **Step 5: Commit** `git commit -am "Add automatic differentiation for core ops"` (add new files first).

### Task 3: Neural-network operations and losses

**Files:**
- Create: `src/AIAnatomy.Core/Ops.Neural.cs`, `src/AIAnatomy.Core/Losses.cs`
- Test: `tests/AIAnatomy.Core.Tests/NeuralOpsTests.cs`

**Interfaces — Produces:**
```csharp
// Ops (partial)
Tensor Relu(Tensor a);
Tensor Gelu(Tensor a);                       // 0.5x(1+tanh(√(2/π)(x+0.044715x³))), as GPT-2
Tensor SoftmaxRows(Tensor a);                // softmax over the last dimension of [N,M]; max-subtracted
Tensor LogSoftmaxRows(Tensor a);
Tensor CausalMask(Tensor scores);            // [T,T]: entries with column > row become -∞ (softmax → 0)
Tensor LayerNormRows(Tensor x, Tensor gamma, Tensor beta, double eps = 1e-5);  // biased variance
// Losses (static class)
Tensor CrossEntropy(Tensor logits, IReadOnlyList<int> targets);   // [N,V], mean over N
Tensor MeanSquaredError(Tensor prediction, Tensor target);
```
- [ ] **Step 1: Write failing tests.** Gradient checks (`< 1e-6`) for Relu (inputs kept away from 0), Gelu, SoftmaxRows, LogSoftmaxRows, LayerNormRows (x, gamma and beta), CrossEntropy and MSE. Value tests: `SoftmaxRows_RowsSumToOne`; `SoftmaxRows_LargeInputsStable` (`[1000, 1001]` → `[0.2689…, 0.7311…]`, no NaN); `CausalMask_SoftmaxGivesZeroAboveDiagonal`; `LayerNorm_OutputMeanZeroVarianceOne` (gamma 1, beta 0, tolerance 1e-6); `CrossEntropy_UniformLogits_IsLogV` (V = 8 → ln 8 = 2.0794…).
- [ ] **Step 2: Run** `dotnet test tests/AIAnatomy.Core.Tests --filter NeuralOpsTests`. Expected: FAIL.
- [ ] **Step 3: Implement.** CausalMask's backward passes gradient only through unmasked entries.
- [ ] **Step 4: Run.** Expected: PASS.
- [ ] **Step 5: Commit** `"Add neural-network ops and losses"`.

### Task 4: Parameters and optimisers

**Files:**
- Create: `src/AIAnatomy.Core/Parameter.cs`, `Optimizers/IOptimizer.cs`, `Optimizers/Sgd.cs`, `Optimizers/Adam.cs`
- Test: `tests/AIAnatomy.Core.Tests/OptimizerTests.cs`

**Interfaces — Produces:**
```csharp
public sealed class Parameter { public Parameter(string name, Tensor value); public string Name { get; } public Tensor Value { get; } }
public interface IOptimizer { void Step(); void ZeroGrad(); double LearningRate { get; set; } }
public sealed class Sgd : IOptimizer { public Sgd(IReadOnlyList<Parameter> ps, double learningRate); }
public sealed class Adam : IOptimizer { public Adam(IReadOnlyList<Parameter> ps, double learningRate,
    double beta1 = 0.9, double beta2 = 0.999, double eps = 1e-8); }
```
- [ ] **Step 1: Write failing tests.** `Sgd_Step_MovesAgainstGradient` (w = 3, loss = w², lr 0.1 → w = 2.4); `Adam_FirstStep_IsLearningRateTimesSign` (grad 5 → change −lr·5/(5+1e-8)); `Sgd_FitsLinearData` (y = 2x+1 from 20 points, 500 steps, lr 0.05 → w within 0.01 of 2 and b within 0.01 of 1); `Step_LeavesParameterIdentityUnchanged` (the same `Tensor` instance is updated in place).
- [ ] **Step 2: Run.** Expected: FAIL. **Step 3: Implement.** **Step 4: Run.** Expected: PASS.
- [ ] **Step 5: Commit** `"Add parameters, SGD and Adam"`.

### Task 5: Tokenizers

**Files:**
- Create: `src/AIAnatomy.Models/Tokenization/ITokenizer.cs`, `CharTokenizer.cs`, `BpeTokenizer.cs`
- Test: `tests/AIAnatomy.Models.Tests/TokenizerTests.cs`

**Interfaces — Produces:**
```csharp
public interface ITokenizer {
    int VocabularySize { get; }
    IReadOnlyList<int> Encode(string text);
    string Decode(IReadOnlyList<int> ids);
    string TokenText(int id);                  // display text; unknown → "�"
    int UnknownId { get; }                     // always 0
}
public sealed class CharTokenizer : ITokenizer {
    public static CharTokenizer FromText(string text);   // id 0 = unknown, then distinct chars in ordinal order
    public static CharTokenizer FromVocabulary(IReadOnlyList<char> chars);  // for loading models
    public IReadOnlyList<char> Vocabulary { get; }
}
public sealed class BpeTokenizer : ITokenizer {
    public static BpeTokenizer Train(string text, int merges);
    public IReadOnlyList<(string Left, string Right)> Merges { get; }   // in the order learned
}
```
Text is normalised before use (`\r\n` → `\n`) by a shared `TextNormalizer.Normalize(string)` in the same folder. BPE: pre-split into words with the leading space attached; repeatedly merge the most frequent adjacent pair; break ties by first occurrence in the text.
- [ ] **Step 1: Write failing tests.** `Char_RoundTrip` (`Decode(Encode(s)) == s` for text made of known characters); `Char_UnknownCharacter_MapsToZeroAndDecodesAsReplacement` (input `"héllo 🙂"` with a vocabulary built from `"hello "` → ids contain 0, decode contains `"�"`, no exception); `Char_EmojiCountsAsOneUnknownToken` (surrogate pair → one id 0, not two); `Bpe_FirstMergeOfAaabdaaabac_IsAA`; `Bpe_RoundTrip` on 2 KB of fable text with 50 merges; `Bpe_ReducesTokenCount` (50 merges give fewer tokens than characters); `Bpe_IsDeterministic`.
- [ ] **Step 2: Run** `dotnet test tests/AIAnatomy.Models.Tests --filter TokenizerTests`. Expected: FAIL.
- [ ] **Step 3: Implement.** CharTokenizer walks text by `Rune`, so a surrogate pair counts as one unit.
- [ ] **Step 4: Run.** Expected: PASS. **Step 5: Commit** `"Add character and BPE tokenizers"`.

### Task 6: Transformer parts, forward trace and interventions

**Files:**
- Create: everything in `src/AIAnatomy.Models/Transformer/` (see the file map)
- Test: `tests/AIAnatomy.Models.Tests/TransformerTests.cs`

**Interfaces — Produces:**
```csharp
public enum PositionKind { Learned, Sinusoidal }
public sealed record TransformerConfig(int VocabularySize, int ContextLength = 64, int ModelWidth = 64,
    int Layers = 2, int Heads = 4, int FeedForwardWidth = 256, PositionKind Positions = PositionKind.Learned,
    bool UseFeedForward = true) {
    public int HeadWidth => ModelWidth / Heads;     // constructor of Transformer throws if not divisible
    public long CountParameters();                  // equals Transformer.Parameters total size
}
public abstract class Module { public abstract IEnumerable<Parameter> Parameters(); }
public sealed record ForwardOptions {
    public IReadOnlySet<(int Layer, int Head)> DisabledHeads { get; init; }   // output of those heads = 0
    public bool RemovePositions { get; init; }
    public static ForwardOptions None { get; }
}
public sealed class HeadTrace { public Tensor Queries, Keys, Values, Scores /*masked, scaled*/, Weights /*[T,T]*/, Output; }
public sealed class BlockTrace { public Tensor ResidualIn, Norm1Out, ConcatenatedHeads /*[T,D] before output projection*/,
    AttentionOut /*after output projection*/, ResidualMid, Norm2Out, FeedForwardHidden /*after GELU; null if no FF*/,
    FeedForwardOut, ResidualOut; public IReadOnlyList<HeadTrace> Heads; }
public sealed class ForwardTrace { public IReadOnlyList<int> TokenIds; public Tensor TokenEmbeddings, PositionEmbeddings, Input;
    public IReadOnlyList<BlockTrace> Blocks; public Tensor FinalNorm, Logits /*[T,V]*/; }
public sealed record ForwardResult(Tensor Logits, ForwardTrace Trace);
public sealed class Transformer : Module {
    public Transformer(TransformerConfig config, SeededRandom rng);   // weights N(0, 0.02), biases 0, LN gamma 1 / beta 0
    public TransformerConfig Config { get; }
    public ForwardResult Forward(IReadOnlyList<int> tokenIds, ForwardOptions? options = null); // throws if count > ContextLength or 0
    public IReadOnlyDictionary<string, long> ParameterCountsByPart();  // keys: "Token embeddings", "Positions", "Attention", "Feed-forward", "Layer norms", "Output layer"
    public Transformer Clone();                                        // deep copy of weights
    public LayerNormModule FinalNorm { get; }  public Parameter OutputWeights { get; }  public Parameter OutputBias { get; }
}
```
Sinusoidal: PE(pos, 2i) = sin(pos / 10000^(2i/d)), PE(pos, 2i+1) = cos(pos / 10000^(2i/d)), with no parameters. Attention scores are scaled by 1/√HeadWidth. Block layout follows D3.
- [ ] **Step 1: Write failing tests.** `Forward_LogitsShapeIsTByV`; `AttentionWeightRows_SumToOne`; `CausalMask_ChangingLaterTokenDoesNotChangeEarlierLogits` (change token 5 of 8 → logits rows 0–4 identical); `GradCheck_TinyModel` (V 5, width 4, 1 layer, 2 heads, context 3 → `< 1e-5` on 10 sampled parameters); `DisabledHead_OutputIsZero`; `RemovePositions_MakesPermutedInputGiveSameLastRowUpToPermutation` (with positions removed, swapping tokens 0 and 1 leaves the last row of logits unchanged); `CountParameters_MatchesActualParameters` for 3 configs; `Forward_TooManyTokens_Throws`; `Sinusoidal_KnownValues` (pos 1, i 0 → sin 1); `Clone_IsIndependent`.
- [ ] **Step 2: Run** `--filter TransformerTests`. Expected: FAIL. **Step 3: Implement.** **Step 4: Run.** Expected: PASS.
- [ ] **Step 5: Commit** `"Add transformer parts with forward trace and interventions"`.

### Task 7: Language model, sampling, training, model files and library

**Files:**
- Create: `src/AIAnatomy.Models/Language/*.cs`, `src/AIAnatomy.Models/Training/*.cs`
- Test: `tests/AIAnatomy.Models.Tests/SamplingTests.cs`, `TrainerTests.cs`, `ModelFileTests.cs`

**Interfaces — Produces:**
```csharp
public sealed class LanguageModel {
    public LanguageModel(Transformer transformer, CharTokenizer tokenizer, ModelInfo info);
    public Transformer Transformer { get; }  public CharTokenizer Tokenizer { get; }  public ModelInfo Info { get; }
    public PredictionResult Predict(string text, ForwardOptions? options = null);   // uses the last ContextLength tokens
}
public sealed record ModelInfo(string Name, string TrainingTextName, int StepsTrained, double? ValidationLoss, int Seed, bool IsShipped);
public sealed record PredictionResult(ForwardResult Forward, IReadOnlyList<int> UsedTokenIds, bool WasTruncated, int DroppedTokenCount);
public sealed record SamplingSettings(double Temperature = 1.0, int TopK = 0, double TopP = 1.0);  // TopK 0 = off; Temperature 0 = always the top choice
public static class Sampling {
    public static double[] Probabilities(double[] logits, SamplingSettings s);  // after temperature, top-k and top-p, renormalised
    public static int Sample(double[] probabilities, SeededRandom rng);
    public static string Generate(LanguageModel m, string prompt, int count, SamplingSettings s, SeededRandom rng); // empty prompt starts from "\n"
}
public sealed record TrainingSettings(int BatchSize = 8, double LearningRate = 3e-3, int Seed = 1, double ValidationFraction = 0.1);
public sealed record TrainingStepReport(int Step, double TrainLoss, double? ValidationLoss, string? StoppedReason);
public static class CorpusValidator { public const int MaxCharacters = 2_000_000;
    public static string? Problem(string text, int contextLength); }   // null = fine; else a plain-language message
public sealed class Trainer {
    public Trainer(Transformer model, IReadOnlyList<int> corpusTokens, TrainingSettings settings);  // trains model in place
    public TrainingStepReport Step();                       // one batch, one Adam step; validation loss every 25 steps
    public Task RunAsync(int steps, IProgress<TrainingStepReport> progress, CancellationToken ct);  // pause = cancel; can run again
    public bool HasDiverged { get; }
}
public static class ModelFile {     // one UTF-8 JSON header line, "\n", then little-endian doubles in Parameters() order
    public static void Save(LanguageModel m, Stream s);
    public static LanguageModel Load(Stream s);              // throws InvalidDataException for bad or incompatible data
}
public sealed record ModelEntry(string Name, string Path, bool IsShipped, bool IsReadable, string? Problem);
public sealed class ModelLibrary {
    public ModelLibrary(string shippedFolder, string userFolder);
    public IReadOnlyList<ModelEntry> List();                 // shipped first, then user's by name; unreadable ones included with Problem
    public LanguageModel Load(ModelEntry e);
    public ModelEntry Save(LanguageModel m, string name);    // file name made safe from name; overwrite asks nothing (caller confirms)
    public void Delete(ModelEntry e);                        // throws InvalidOperationException for shipped
}
```
The diverged message is exactly: `"Training stopped: the loss became too large (try a smaller learning rate)"` (when the loss is NaN, infinite, or above 1e4).
- [ ] **Step 1: Write failing tests.**
  - `Sampling_TemperatureZero_IsArgmax`; `Sampling_TopK1_IsArgmax`; `Sampling_HighTemperature_FlattensDistribution` (entropy rises); `Sampling_TopP_KeepsSmallestSetAboveP` (`[0.5,0.3,0.2]`, p 0.7 → `[0.625,0.375,0]`); `Generate_EmptyPrompt_Works`.
  - `Predict_LongInput_UsesLastContextTokensAndReportsTruncation`.
  - `Trainer_OverfitsTinyText` (text "the cat sat on the mat. " ×4, config width 32, 1 layer, 2 heads, context 16, 300 steps → train loss < 0.3); `Trainer_SameSeed_SameLosses` (5 steps twice); `Trainer_HugeLearningRate_StopsWithMessage` (lr 50 → a report with the exact StoppedReason, `HasDiverged` true); `Trainer_RunAsync_Cancels` (cancel after the first report → task completes without throwing, step count < requested).
  - `CorpusValidator_TooShort_GivesMessage`; `CorpusValidator_TooLarge_GivesMessage`; `CorpusValidator_Normal_IsNull`.
  - `ModelFile_RoundTrip_GivesIdenticalLogits`; `ModelFile_Truncated_ThrowsInvalidData`; `Library_CorruptUserFile_ListedUnreadable` (write garbage bytes → entry with `IsReadable` false and a Problem, no throw); `Library_DeleteShipped_Throws`.
- [ ] **Step 2: Run** `dotnet test tests/AIAnatomy.Models.Tests`. Expected: the new tests FAIL.
- [ ] **Step 3: Implement.** Batches are random windows of `ContextLength + 1` tokens from the first 90% of the corpus. Validation uses fixed windows from the last 10%.
- [ ] **Step 4: Run.** Expected: PASS. The overfit test runs in under 30 s. If it doesn't, speed up `MatMul` (loop order, spans) before loosening anything.
- [ ] **Step 5: Commit** `"Add language model, sampling, trainer and model library"`.

### Task 8: Inspection tools and the induction experiment

**Files:**
- Create: `src/AIAnatomy.Models/Inspection/Pca.cs`, `LogitLens.cs`, `NeuronProbe.cs`, `InductionExperiment.cs`
- Test: `tests/AIAnatomy.Models.Tests/InspectionTests.cs`

**Interfaces — Produces:**
```csharp
public static class Pca { public static (double X, double Y)[] Project2D(double[][] rows, out double explainedFraction); }
  // centred; power iteration with deflation; each component's sign set so its largest-magnitude entry is positive
public static class LogitLens {
    // entry 0 = after embeddings; entry i = after block i; each = softmax(unembed(finalNorm(residual)))[position]
    public static IReadOnlyList<double[]> PerLayerProbabilities(Transformer m, ForwardTrace t, int position);
}
public sealed record NeuronActivation(int Position, double Value, string Context);   // Context = up to 12 chars ending at Position
public static class NeuronProbe { public static IReadOnlyList<NeuronActivation> TopContexts(LanguageModel m, string text, int layer, int neuron, int count); }
public static class InductionExperiment {
    public static TransformerConfig Config { get; }   // V 16, context 16, width 32, 2 layers, 2 heads, UseFeedForward false
    public static IReadOnlyList<int> MakeSequence(SeededRandom rng);   // 8 random tokens from 1..15, then the same 8 again
    public static double InductionScore(Transformer m, int layer, int head, int seed, int samples = 50);
      // mean attention weight from position i (8..15) to position i − 7 (the token after the earlier copy)
    public static Task<Transformer> TrainAsync(int steps, int seed, IProgress<TrainingStepReport> p, CancellationToken ct);
}
```
- [ ] **Step 1: Write failing tests.** `Pca_PointsOnALine_ExplainNearlyAll` (> 0.999); `Pca_IsDeterministic`; `LogitLens_LastEntryEqualsModelOutput` (to 1e-12); `LogitLens_HasLayersPlusOneEntries`; `NeuronProbe_ReturnsSortedDescending`; `Induction_UntrainedScoreIsLow` (best head < 0.3); `Induction_TrainedModelHasInductionHead` (`[Trait("Category","Slow")]`: train 1,500 steps, seed 1 → best layer-2 head score > 0.5).
- [ ] **Step 2: Run.** Expected: FAIL. **Step 3: Implement.**
- [ ] **Step 4: Run.** Expected: PASS. If the slow test needs more than 120 s, or doesn't reach 0.5 after tuning lr/steps, stop and report it. Never lower the threshold to pass.
- [ ] **Step 5: Commit** `"Add PCA, logit lens, neuron probe and induction experiment"`.

### Task 9: Teaching toys

**Files:**
- Create: `src/AIAnatomy.Models/Toys/Perceptron.cs`, `LossSurface.cs`, `ExpressionGraphDemo.cs`, `RuleBasedWriter.cs`, `BigramModel.cs`
- Test: `tests/AIAnatomy.Models.Tests/ToysTests.cs`

**Interfaces — Produces:**
```csharp
public sealed record LabeledPoint(double X1, double X2, int Label);   // Label 0/1
public sealed class Perceptron {   // step activation, classic perceptron rule
    public double W1 { get; } public double W2 { get; } public double Bias { get; }
    public int Predict(double x1, double x2);
    public IReadOnlyList<PerceptronEpoch> Train(IReadOnlyList<LabeledPoint> data, double rate, int maxEpochs); // stops early at 0 errors
    public static IReadOnlyList<LabeledPoint> And { get; } public static IReadOnlyList<LabeledPoint> Or { get; } public static IReadOnlyList<LabeledPoint> Xor { get; }
}
public sealed record PerceptronEpoch(int Epoch, double W1, double W2, double Bias, int Errors);
public static class LossSurface {   // f(w1,w2) = w1² + 10·w2²
    public static double Loss(double w1, double w2);
    public static IReadOnlyList<(double W1, double W2, double Loss)> Descend(double w1, double w2, double rate, int steps);
}
public sealed class ExpressionGraphDemo {   // L = (a·b + c)·f, built from Core Ops, labelled nodes
    public ExpressionGraphDemo(double a, double b, double c, double f);
    public IReadOnlyList<Tensor> Nodes { get; }   // topological order, each with Label, Data[0] and Grad[0] after Run
    public void Run();                             // forward + Backward
}
public sealed record WritingRule(string Description, Func<string, char?> Apply);
public sealed class RuleBasedWriter { public IReadOnlyList<WritingRule> Rules { get; } public string Write(string start, int count); }  // first rule that applies wins
public sealed class BigramModel {   // logits table [V,V] trained with Core cross-entropy
    public BigramModel(CharTokenizer tokenizer, int seed);
    public double TrainSteps(IReadOnlyList<int> corpus, int steps, double rate);   // returns last loss
    public string Generate(string start, int count, SeededRandom rng);
}
```
- [ ] **Step 1: Write failing tests.** `Perceptron_LearnsAnd`; `Perceptron_LearnsOr`; `Perceptron_NeverSolvesXor` (after 100 epochs, errors > 0 on every epoch); `LossSurface_SmallRateConverges` (rate 0.04, 100 steps → loss < 1e-3); `LossSurface_RateAbove0_1Diverges` (rate 0.11 → loss grows); `ExpressionGraph_KnownGradients` (a 2, b −3, c 10, f −2 → L = −8, dL/da = 6, dL/db = −4, dL/dc = −2, dL/df = 4); `RuleWriter_IsDeterministic`; `Bigram_LossFallsWithTraining` (on 5 KB of fable text, loss after 200 steps < loss at step 1 − 0.5).
- [ ] **Step 2: Run.** Expected: FAIL. **Step 3: Implement.** RuleBasedWriter has 8–12 readable rules (for example "after q, write u", "after a full stop, write a space", "after a space, write t").
- [ ] **Step 4: Run.** Expected: PASS. **Step 5: Commit** `"Add teaching toys"`.

### Task 10: Training text, pretrain tool and shipped models

**Files:**
- Create: `content/texts/aesop-fables.txt`, `tools/AIAnatomy.Pretrain/Program.cs` (+ csproj, added to the slnx), `content/models/fables-small.aimodel`, `content/models/induction-demo.aimodel`, `THIRD-PARTY-NOTICES.txt`
- Test: `tests/AIAnatomy.Models.Tests/ShippedModelTests.cs`

**Interfaces — Consumes:** `Trainer`, `ModelFile`, `InductionExperiment.TrainAsync` (Tasks 7–8).

- [ ] **Step 1: Get the text** (only after the owner's okay, D7): Project Gutenberg eBook #21. Check that the title page says Aesop's Fables translated by George Fyler Townsend. Remove the Gutenberg header, footer and licence, normalise line endings, and record the source in `THIRD-PARTY-NOTICES.txt`.
- [ ] **Step 2: Write failing tests** `ShippedModelTests`: `FablesModel_Loads_AndIsMarkedShipped`; `FablesModel_ValidationLossBelow1_9` (reads `ModelInfo.ValidationLoss`); `FablesModel_GeneratesMostlyKnownWords` (temperature 0.8, seed 1, 300 chars → at least 40% of space-separated words appear in the training text); `InductionModel_HasHeadAbove0_6`.
- [ ] **Step 3: Implement the Pretrain tool:** `dotnet run --project tools/AIAnatomy.Pretrain -- fables --text content/texts/aesop-fables.txt --out content/models/fables-small.aimodel --steps <n> --seed 1` and `-- induction --out content/models/induction-demo.aimodel --seed 1`. It prints the loss every 100 steps. The fables model uses the default `TransformerConfig`. Choose `--steps` so the validation loss is below 1.9 (log the steps and time in decisions.md).
- [ ] **Step 4: Run both** (in the background; they may take up to about 30 minutes), then `dotnet test tests/AIAnatomy.Models.Tests --filter ShippedModelTests`. Expected: PASS. Test projects copy `content/**` to their output folder, as ASLCoach does.
- [ ] **Step 5: Commit** `"Add training text, pretrain tool and shipped models"`.

### Task 11: Lessons, Markdown subset and sources

**Files:**
- Create: `src/AIAnatomy.Lessons/Lesson.cs`, `LessonCatalog.cs`, `Sources.cs`, `Markdown/MarkdownDocument.cs`, `Markdown/MarkdownParser.cs`; `content/sources.json`; one placeholder lesson file per lesson (full text comes in Task 20)
- Test: `tests/AIAnatomy.Lessons.Tests/MarkdownParserTests.cs`, `LessonCatalogTests.cs`

**Interfaces — Produces:**
```csharp
public enum Section { Map, Parts, Whole, Family, Beyond }
public sealed record Lesson(string Id, Section Section, int Order, string Title, string LabId,
    MarkdownDocument Explanation, MarkdownDocument? Formula, MarkdownDocument? BrainsAndModels, IReadOnlyList<string> SourceIds);
public sealed class LessonCatalog { public static LessonCatalog Load(string contentFolder);
    public IReadOnlyList<Lesson> All { get; } public IReadOnlyList<Lesson> InSection(Section s); public Lesson Get(string id); }
public sealed record Source(string Id, string Citation, string? Url);
public sealed class SourceList { public static SourceList Load(string path); public IReadOnlyList<Source> All { get; } public Source? Find(string id); }
// Markdown: blocks Heading(int Level, inlines), Paragraph(inlines), BulletList(items), FormulaBlock(string Latex);
// inlines Text, Bold, Italic, LessonLink(string LessonId, string Text) from [text](lesson:id)
public sealed record MarkdownDocument(IReadOnlyList<MarkdownBlock> Blocks);
public static class MarkdownParser { public static MarkdownDocument Parse(string markdown); }
```
Lesson file format: front matter between `---` lines (`id`, `section`, `order`, `title`, `lab`, `sources` as a comma list), then the body. The body is split at the headings `## Formula` and `## Brains and models` (both optional). Lessons and labs for Stage 1, in order:

| Section | id → lab |
|---|---|
| Map | `rules-vs-learning` → RulesVsLearningLab; `architecture-map` → ArchitectureMapLab |
| Parts | `tokens` → TokensLab; `embeddings` → EmbeddingsLab; `position` → PositionLab; `attention` → AttentionLab; `multi-head` → MultiHeadLab; `feed-forward` → FeedForwardLab; `residual-stream` → ResidualLab; `output-sampling` → SamplingLab; `learning` → LearningLab |
| Whole | `train` → TrainLab; `step-through` → StepThroughLab; `scale` → ScaleLab |
| Family | `family-coming` → ComingLaterLab |
| Beyond | `beyond-coming` → ComingLaterLab |

`sources.json` starts with the spec's list (section 7.3). The ids used are `vaswani2017, radford2019, brown2020, olsson2022, geva2021, nostalgebraist2020, sennrich2016, ouyang2022, bai2022, ho2020, lecun1998, suttonbarto2018`.
- [ ] **Step 1: Write failing tests.** Parser: `Parses_BoldItalicLink`; `Parses_FormulaBlock`; `Parses_BulletList`; `UnclosedBold_IsLiteralText` (no throw). Catalog: `Loads_AllStage1Lessons` (the 16 ids above, in order); `EveryLessonLink_PointsToExistingLesson`; `EverySourceId_Resolves`; `MissingOptionalSections_AreNull`; `BadFrontMatter_ThrowsWithFileName`.
- [ ] **Step 2: Run** `dotnet test tests/AIAnatomy.Lessons.Tests`. Expected: FAIL. **Step 3: Implement.** **Step 4: Run.** Expected: PASS.
- [ ] **Step 5: Commit** `"Add lesson catalogue, Markdown subset and sources"`.

### Task 12: Presentation services and main view-model

**Files:**
- Create: `src/AIAnatomy.Presentation/Services/*.cs`, `MainViewModel.cs`, `LabViewModel.cs`, `LabFactory.cs`, `Labs/ComingLaterLabViewModel.cs`
- Test: `tests/AIAnatomy.Presentation.Tests/MainViewModelTests.cs`, `SettingsServiceTests.cs`, `ActiveModelServiceTests.cs`

**Interfaces — Produces:**
```csharp
public sealed record AppSettings(double BodyFontSize = 20, double HeadingFontSize = 28, string Theme = "Light", string? ActiveModelName = null);
public interface ISettingsService { AppSettings Current { get; } void Save(AppSettings s); }   // clamps body 16–32; heading = body × 1.4
public sealed class SettingsService : ISettingsService { public SettingsService(string filePath); }  // corrupt or missing file → defaults
public interface IActiveModelService { LanguageModel Current { get; } ModelLibrary Library { get; }
    void SetActive(ModelEntry e); event EventHandler? Changed; }
public static class AppPaths { public static string UserData { get; } /* %LOCALAPPDATA%\AIAnatomy */ public static string Content { get; } /* beside the exe */ }
public abstract partial class LabViewModel : ObservableObject {
    public abstract string LabId { get; }
    [ObservableProperty] private int seed = 1;
    public abstract void Reset();                       // back to defaults
    public virtual void OnActiveModelChanged() { }
}
public sealed class LabFactory { public LabFactory(IActiveModelService models, LessonCatalog lessons, Func<string, Task> navigate);
    public LabViewModel Create(string labId); }         // unknown id → ArgumentException
public sealed partial class MainViewModel : ObservableObject {
    // Sections, SelectedSection, Lessons (of section), SelectedLesson, CurrentLab, ModelEntries, SelectedModelEntry,
    // Settings; commands NavigateToLesson(string id), ShowSources, ResetLab
}
```
- [ ] **Step 1: Write failing tests.** `Settings_CorruptFile_GivesDefaults`; `Settings_ClampsFontSize` (40 → 32, 10 → 16); `Settings_RoundTrip`; `ActiveModel_DefaultsToShippedFablesModel`; `ActiveModel_SavedNameMissing_FallsBackToShipped`; `ActiveModel_SetActive_RaisesChanged`; `Main_SelectingSection_ShowsItsLessons`; `Main_SelectingLesson_CreatesItsLab`; `Main_NavigateToLesson_SwitchesSection`; `Main_ModelChange_NotifiesCurrentLab`.
- [ ] **Step 2: Run** `dotnet test tests/AIAnatomy.Presentation.Tests`. Expected: FAIL. **Step 3: Implement.** **Step 4: Run.** Expected: PASS.
- [ ] **Step 5: Commit** `"Add presentation services and main view-model"`.

### Task 13: WPF shell, themes, visual controls and diagnostics

**Files:**
- Create: `src/AIAnatomy.App/App.xaml(.cs)`, `MainWindow.xaml(.cs)`, `Themes/Light.xaml`, `Themes/Dark.xaml`, `Themes/Shared.xaml`, `Controls/*.cs`, `Views/ComingLaterView.xaml`, `Views/ControlGallery.xaml`, `Views/SourcesView.xaml`, `Views/SettingsView.xaml`, `Diagnostics/CommandLine.cs`, `Diagnostics/PngRenderer.cs`, `Diagnostics/SelfTests.cs`
- Test: `tests/AIAnatomy.Presentation.Tests/CommandLineTests.cs` (the parser lives in Presentation as `DiagnosticOptions.Parse(string[])`)

**Interfaces — Produces:**
- Controls (all `FrameworkElement`s drawing with `OnRender`, bound to plain data, each with a `ToolTip`/selected-cell readout showing the exact number):
  `HeatMap` (`double[,] Values`, `RowLabels`, `ColumnLabels`, `Minimum`, `Maximum`, `SelectedCell`); `VectorStrip` (`double[] Values`); `ScatterPlot` (`IReadOnlyList<PlotPoint>` with label and group); `ProbabilityBars` (`IReadOnlyList<LabeledValue>`, highlights the chosen item); `LineChart` (one or two series); `NetworkDiagram` (nodes and edges with labels and values); `PipelineView` (`IReadOnlyList<PipelineStage>`); `MarkdownView` (`MarkdownDocument Document`, renders to a `FlowDocument`, `FormulaBlock` via WpfMath `FormulaControl` in a `BlockUIContainer`, lesson links raise `NavigateRequested`). The shared data records `PlotPoint`, `LabeledValue` and `PipelineStage(string Title, string Description, double[]? Values, IReadOnlyList<LabeledValue>? Items)` live in `AIAnatomy.Presentation`.
- Shell layout as in spec 5.1. The header has the section tabs, the active-model `ComboBox` (unreadable entries shown disabled, with their problem in a tooltip), and Sources and Settings buttons. Lab views are chosen by view-model type through `DataTemplate`s in `MainWindow.Resources`.
- Font sizes come from `DynamicResource BodyFontSize` / `HeadingFontSize`. Theme brushes come from the swapped dictionary.
- `DiagnosticOptions.Parse`: `--render-png <lessonId|gallery|sources|settings> <file.png> [--theme Light|Dark] [--text-size N] [--width 1600] [--height 1000]` and `--selftest <all-labs|train>`. The app exits with 0 on success, or 1 with a message on stderr.

- [ ] **Step 1: Write failing tests** `CommandLineTests`: `ParsesRenderPngWithDefaults`; `ParsesThemeAndTextSize`; `UnknownOption_GivesError`; `NoArguments_MeansNormalStart`.
- [ ] **Step 2: Run.** Expected: FAIL. **Step 3: Implement** the shell, themes, controls, `ControlGallery` (every control with fixed sample data) and diagnostics. Add the WpfMath 2.1.0 package to App, and its MIT notice to `THIRD-PARTY-NOTICES.txt`.
- [ ] **Step 4: Verify.** Run the tests (Expected: PASS), then `dotnet build` and, in one launch each: `AIAnatomy.App.exe --render-png gallery %TEMP%\aia-gallery.png` and `--render-png gallery %TEMP%\aia-gallery-dark.png --theme Dark --text-size 32`. Open both PNGs with the Read tool. Expected: every control is visible, legible in both themes, with no clipped text.
- [ ] **Step 5: Commit** `"Add WPF shell, themes, visual controls and diagnostics"`.

### Tasks 14–19: The labs

Every lab task has the same shape:
- **Files:** `src/AIAnatomy.Presentation/Labs/<Lab>ViewModel.cs`, `src/AIAnatomy.App/Views/<Lab>View.xaml`, a registration in `LabFactory` and a `DataTemplate`; tests in `tests/AIAnatomy.Presentation.Tests/Labs/<Lab>Tests.cs`.
- **Consumes:** `IActiveModelService.Current` (Task 12) and the Models APIs named in the task. View-models read numbers from `ForwardTrace` or the toy objects. They never recompute the math.
- **Steps:** (1) write the failing view-model tests listed; (2) run `dotnet test tests/AIAnatomy.Presentation.Tests --filter <Lab>` and expect FAIL; (3) implement the view-model and view; (4) run the tests (expect PASS), then render each of the task's lessons once with `--render-png <lessonId> %TEMP%\aia-<lessonId>.png` and read the PNGs (labels legible, numbers visible, nothing clipped); (5) commit `"Add <lab names>"`.
- Every lab: a **Reset** command restoring the documented defaults; a **Seed** box wherever randomness is used; and an empty-input state that shows "Type some text to begin" rather than an error.

### Task 14: Map labs, Tokens and Position

- **RulesVsLearningLab** (`RuleBasedWriter`, `BigramModel`): the rule list beside the rule writer's output; a "Learn" button training the bigram model in 20-step chunks (background) with output regenerated after each chunk, so the user watches gibberish become letter patterns; loss shown. Tests: `RuleOutput_ShownWithRules`; `Learn_LowersLoss_AndChangesOutput`; `Reset_RestoresUntrainedModel`.
- **ArchitectureMapLab** (`LanguageModel.Predict`): `NetworkDiagram` of the pipeline boxes (Tokens → Embeddings + Position → Block × N (Attention, Feed-forward) → Final norm → Output → Probabilities), each box linking to its lesson; a text box with live top-10 next-character probabilities. Tests: `TypingText_UpdatesTop10`; `UnknownCharacters_ShownAsReplacement_NoThrow`; `LongText_ShowsTruncationNote` (text: "Only the last 64 characters are used: that is this model's context length."); `ClickingBox_NavigatesToLesson`.
- **TokensLab** (`CharTokenizer`, `BpeTokenizer.Train` on the fables text with a merges slider 0–500, default 200): tokens as chips with ids for both tokenizers, token counts, a "count the letters" panel for a chosen word showing which pieces the BPE sees. Tests: `BothTokenizationsShown`; `MergesSlider_ChangesBpeTokenCount`; `EmojiInput_ShowsUnknownChip`.
- **PositionLab** (`ForwardOptions.RemovePositions`, `SinusoidalPositions`): heat maps of learned position vectors and of the sine/cosine scheme; top-5 predictions for the user's text with and without position information side by side. Tests: `RemovingPositions_ChangesPredictions`; `SinusoidalHeatMap_HasContextByWidthCells`.

### Task 15: Embeddings and Attention

- **EmbeddingsLab** (`Pca`, embeddings table): `ScatterPlot` of all token vectors (labels = characters; groups: letters, digits, punctuation, space) for the active model and for an untrained model with the same config and seed; a `VectorStrip` for the selected token; explained-variance shown ("this flat picture keeps N% of the spread"). Tests: `PlotHasOnePointPerToken`; `SelectingPoint_ShowsItsVector`.
- **AttentionLab**:
  - toy part: query and two to four key/value vectors in 2-D, draggable and with numeric boxes as a keyboard alternative. Dot products, scaled scores, softmax weights and the weighted sum are computed with Core `Ops` and shown live.
  - real part: layer and head pickers with a `HeatMap` of `HeadTrace.Weights` for the user's text (future cells drawn as "blocked").
  - induction experiment: "Show trained demo" loads `induction-demo.aimodel`; "Train it yourself" runs `InductionExperiment.TrainAsync` in the background with a progress bar and the live induction score of every head; the sequence and the heat map are shown with the "after the earlier copy" cells outlined.
  - Tests: `Toy_WeightsSumToOne`; `Toy_MovingQueryTowardKey_RaisesItsWeight`; `Real_HeatMapRowsSumToOne`; `Real_FutureCellsBlocked`; `Induction_DemoShowsHeadAbove0_6`; `Induction_TrainCanBeCancelled`.

### Task 16: Multi-head, Feed-forward and Residual stream

- **MultiHeadLab**: all heads of a chosen layer side by side; a check box per head that disables it (`DisabledHeads`) with the top-5 predictions updating; at a chosen position, `VectorStrip`s of each head's output, their concatenation (`ConcatenatedHeads`) and the result after the output projection (`AttentionOut`). Tests: `ShowsOneHeatMapPerHead`; `DisablingHead_ChangesPredictions`; `ConcatenationWidthEqualsModelWidth`.
- **FeedForwardLab** (`NeuronProbe`): layer and neuron pickers; the neuron's activation along the user's text as a strip; the top 10 contexts over the first 20,000 characters of the fables text (computed in the background, cached per model, layer and neuron); a note that this is a research view (Geva et al., 2021). Tests: `TopContextsSortedDescending`; `ChangingNeuron_UpdatesContexts`.
- **ResidualLab** (`LogitLens`, `LayerNormRows`): bars of the residual vector's size after each layer; the logit lens table (rows: after embeddings, after block 1…N; columns: top 5 guesses with probabilities) at a chosen position; and a layer-norm demo showing a vector before and after, with mean and spread printed. Tests: `LensRowsEqualLayersPlusOne`; `LastLensRowMatchesModelOutput`; `LayerNormDemo_MeanZero`.

### Task 17: Output and sampling, and Learning

- **SamplingLab** (`Sampling`): logits list and `ProbabilityBars` for the user's text; temperature slider 0–2 (default 1), top-k 0–50, top-p 0.1–1; "Sample 100 times" giving a frequency chart beside the probabilities; "Generate 200 characters" output. Tests: `TemperatureZero_AllSamplesSame`; `SampleFrequencies_SumTo100`; `TopK1_OnlyOneBarNonZero`.
- **LearningLab**, four tabs:
  - (a) Perceptron with AND / OR / XOR data, the decision line drawn per epoch, step and run buttons, and the epoch table.
  - (b) `LossSurface` contour plot with the descent path, a learning-rate slider 0.01–0.15 (default 0.04), and the message "Too large: each step overshoots further" when it diverges.
  - (c) `ExpressionGraphDemo` in a `NetworkDiagram` with value and gradient on every node, and editable a, b, c, f.
  - (d) cross-entropy for one prediction of the active model: the right next character, its probability p, and the loss −ln p.
  - Tests: `Xor_NeverReachesZeroErrors`; `HighRate_ShowsDivergenceMessage`; `GraphGradients_MatchKnownValues`; `CrossEntropy_EqualsMinusLogP`.

### Task 18: Whole — Train

- **TrainLab** (`Trainer`, `CorpusValidator`, `ModelLibrary`):
  - text source: the fables (default) or pasted text, with `CorpusValidator.Problem` shown in place of the Start button when there is one.
  - hyperparameter boxes: layers 1–4, heads 1–8 (must divide width; message otherwise), width {16, 32, 48, 64, 96}, context 16–128, learning rate, steps, batch, seed. Live parameter count with `ParameterCountsByPart`.
  - Start, Pause and Resume, Stop. Background `RunAsync` on a `Clone` or a fresh model; never the active model.
  - `LineChart` of train and validation loss; a 200-character sample every 50 steps; the diverged message shown as is.
  - "Save to library…" (name box, confirmation on overwrite) and "Use in the labs" (saves if needed, then `SetActive`). Disabled after divergence.
  - Tests: `InvalidHeads_ShowsMessage`; `TooShortText_DisablesStart`; `Start_ProducesReports_AndChartPoints`; `Pause_ThenResume_ContinuesStepCount`; `Diverged_DisablesSave`; `UseInLabs_ChangesActiveModel`; `TrainingDoesNotTouchActiveModel` (active model's logits identical before and after 5 steps).
- Also implement `--selftest train` (5 steps through the view-model, exit 0) and `--selftest all-labs` (create every lab, call Reset, exit 0). Run both.

### Task 19: Whole — Step-through and Scale

- **StepThroughLab**: text plus position picker; `PipelineView` with one stage per step (tokens with ids; token embedding; position embedding; their sum; for each block: LN1, each head's weights for this position, attention output, residual after attention, LN2, feed-forward hidden (top 10 by size), feed-forward output, residual after; final norm; logits; probabilities top 10). All values come from the `ForwardTrace`. Next and Previous buttons step through the stages with the keyboard. Tests: `StageCount_Is4Plus8PerBlockPlus3` (stages = 4 + 8·Layers + 3); `ResidualStageEqualsTraceValue`; `ProbabilitiesStageMatchesPredict`.
- **ScaleLab**: our parameter count by part; a log-scale bar chart with our model, GPT-2 small (124 M; the GPT-2 paper's table lists 117 M, and the released model has 124 M: say both), and GPT-3 (175 B, Brown et al. 2020); a "Claude: size not published" row with no bar. Tests: `IncludesUnpublishedClaudeRowWithoutValue`; `OurCountMatchesConfig`.

### Task 20: Lesson texts and Sources page

**Files:**
- Modify: every `content/lessons/**/*.md` (full text), `content/sources.json`
- Test: `tests/AIAnatomy.Lessons.Tests/LessonContentTests.cs`

- [ ] **Step 1: Write failing tests** `EveryLessonHasExplanationOver80Words`; `EveryPartsAndWholeLessonHasFormula`; `EveryLessonCitesAtLeastOneSourceOrIsMarkedTextbook` (front-matter `sources: textbook` allowed); `EveryFormulaParsesInWpfMath` (in Presentation.Tests, using `WpfMath.Parsers.TexFormulaParser`).
- [ ] **Step 2: Run.** Expected: FAIL.
- [ ] **Step 3: Write the lessons:** plain words first, pitched for a curious, technically minded adult new to AI. Each has a "Brains and models" note where a comparison is meaningful, marking open questions as open. Hold to spec 7.2. Check every citation (authors, year, title) against the paper itself before using it, and record anything that couldn't be checked in `docs/decisions.md`.
- [ ] **Step 4: Run** the tests (expect PASS), then render 4 lesson pages (`tokens`, `attention`, `train`, `scale`) at 20 pt and 32 pt and read them for clipping and readability.
- [ ] **Step 5: Commit** `"Write Stage 1 lesson texts and sources"`.

### Task 21: Final verification

- [ ] **Step 1: Run** `dotnet test AIAnatomy.slnx` (slow tests included). Expected: all PASS, with the count recorded.
- [ ] **Step 2: Run** `--selftest all-labs` and `--selftest train`. Expected: exit 0.
- [ ] **Step 3: Render** every lesson in Light at 20 pt, and five chosen lessons in Dark at 32 pt, in as few launches as possible (add a `--render-png all <folder>` mode if that saves launches). Read every PNG.
- [ ] **Step 4: Write** `docs/final-check.md`: test counts, what was checked visually, what could not be checked without the owner (dragging in the attention toy, the feel of training speed, keyboard-only use), and a "Try these yourself" list.
- [ ] **Step 5: Commit** `"Stage 1 final check"`. Do not merge into `main` until the owner says so.
