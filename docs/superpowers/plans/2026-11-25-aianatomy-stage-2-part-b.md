# AIAnatomy Stage 2, Part B (lessons 4.6–4.10) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The five neural members of section 4 get working lessons: image networks (filters, then the whole network), a memory network beside the transformer, and diffusion (shapes, then the steps). Each network ships already trained, with "train it again".

**Architecture:** The Core engine gains tanh, reshape, 2-D convolution and max pooling, each gradient-checked. Models gains `Vision`, `Recurrent` and `Diffusion` namespaces, a version 2 of the model file for the new network kinds, and a shared stepped trainer. `tools/AIAnatomy.Pretrain` trains and ships the networks to `content/models/family/`. Presentation and App add one lab per lesson, following Part A's patterns: screens, `BeginRun`, the generation counter, render views, and `--selftest all-labs`.

**Tech Stack:** C# / WPF on .NET 10, CommunityToolkit.Mvvm, MSAGL, WpfMath, xUnit 2.9.3, System.IO.Compression (in the framework).

**Spec:** `docs/superpowers/specs/2026-11-24-aianatomy-stage-2-design.md`. Read sections 1–4, 5.6–5.10, 6, 7 and 9.
- Also binding: Part A's records in `docs/decisions.md` (section `## Stage 2 Part A`: screens rules, live text, check-box style, citation checks) and D14 and D20 (the model file, shipped models).
- Part A's plan, `docs/superpowers/plans/2026-11-24-aianatomy-stage-2-part-a.md`, shows the lab patterns.

## Global Constraints

- Branch: all work on local branch `stage2-part-b`, cut from `main` at 55918c4. The controller commits each task without asking (owner, 2026-11-24). Never merge or push: those wait for the owner.
- First commit on the branch is this plan: `Stage 2 Part B plan`.
- Commit messages start `Stage 2B: ` and end with the line `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- C#/WPF, .NET 10, MVVM with CommunityToolkit.Mvvm. **No new third-party runtime libraries.** All AI maths is our own code on our own Core engine. New engine operations get finite-difference gradient checks below 1e-6, like Stage 1's.
- Layering stays App → Presentation → Lessons → Models → Core.
  - New ops with their own backward live in Core, because the `Tensor` constructor is internal.
  - Models never references Presentation.
- **Download gate (spec 5.7):** the controller asks the owner before any dataset is downloaded, naming the file, its source and its size, and waits for a yes. If the answer is no, or no 28×28 set's licence can be confirmed, Part B carries on with the UCI 8×8 optical digits (same gate) without stopping, and the lessons are written for whichever set is used. Raw downloads stay outside the repo; only our subset is bundled.
- Bundled data must be public domain or openly licensed and listed in `THIRD-PARTY-NOTICES.txt` (repo root, existing block format).
- Every lab responds within about a second. Training runs in the background with progress and Stop, and the UI never freezes.
- **"Train it again" finishes in about 2 minutes or less on this PC.** If a network can't, in this order: shrink the network, then train on a smaller data subset or for fewer steps, and only then accept a longer run, never past about 3 minutes.
  - The shipped models may be larger than what "train it again" trains, and the lab says so.
  - Measured times and results go in `docs/decisions.md`, in D20 style.
- Shipped networks are trained by `tools/AIAnatomy.Pretrain` in **Release with seed 1**. Their measured results are stored in the model file's info (`ModelInfo.Results`).
- Seeded randomness everywhere (`AIAnatomy.Core.SeededRandom`), with the seed shown and editable in every lab that uses randomness.
- Stage 1's shipped and user-saved model files must still load (spec 6). A test proves it.
- 13 pt default text, 10–24 pt range, Dark and Light, full keyboard use, colour never the only carrier of meaning. **No sound.**
  - Result text uses the shared live-text behaviour (Task 1), never an `AutomationProperties.Name` that hides bound text.
  - Drawing needs a mouse or pen, so every lab that takes a drawing also offers ready-made samples for keyboard users.
- Visual checks are by `--render-png` and `--selftest` only, with few exe launches. **Never take screenshots with PowerShell `Add-Type`/`CopyFromScreen`.**
- Lesson text rules:
  - Explanations of 150–350 words, in plain words.
  - Standard textbook material or a confirmed source, and every cited source named in the lesson's own text.
  - No over-claiming either way. Claude may appear only as one example among many.
  - Built lessons have `idea:` (one sentence, under 25 words, ending with a full stop), `part:`, `## Try this` (2–4 steps), `## Check yourself` (2–3 questions, with the correct answer's place varied and its length no giveaway), `## Formula` where the method has one, and connections in `content/connections.json`.
  - A question on lab defaults or shipped results has `checked-by-code:`.
- Each new source is confirmed from the publisher or a library catalogue before it is cited. The check is logged under "Citation checks (Stage 2 Part B)" in `docs/decisions.md`.
- Decisions go under a new heading `## Stage 2 Part B` in `docs/decisions.md`. Issues found and not fixed go in `private/AIAnatomy-issues.xlsx` (next ID after AA-034). Model, research and observation mismatches go in `docs/research/scientific-inconsistencies.md`.
- Tests have no wall-clock assertions.
  - Slow training tests use `[Collection(TrainingCollection.Name)]` and the Release-only `Slow` pattern in `InspectionTests.cs`.
  - Full suite: `dotnet test AIAnatomy.slnx > <log> 2>&1`, run in the foreground, then read the log. Build the App explicitly before any render.

## Review Focus

1. **Drawings that are empty, a dot, very small, at the pad's edge, or a scribble over the whole pad.** Expected:
   - The digit labs refuse an empty pad with a message. A dot becomes a small centred blob, and the rest are scaled and centred as the training digits were.
   - The diffusion lab refuses a line that is too short or too small, says why, and keeps the previous shape.
   - Tests in Tasks 8 and 10.
2. **Train it again, then leave the lesson, press Reset, press Stop mid-run, change the seed, or let the run diverge (NaN).** Expected: no stale result lands, Reset brings back the shipped network, and a diverged run stops with a message. Tests in Task 5, plus each lab task.
3. **A model file of the wrong kind or corrupted.** Examples: a family network file given to the language-model loader, a truncated shipped file, a version 3 file. Expected: a plain `InvalidDataException` message. The language-model picker never lists family files, and the app still starts. Tests in Task 4.
4. **Memory-lab text with characters the network never saw, or an empty prompt.** Expected: unknown characters map to the unknown token as the Fables model does, and an empty prompt gives the lab's message, not an exception. Tests in Tasks 9 and 15.
5. **Large text and narrow panes with many small maps.** Cases: 16 feature maps, a 28×28 heat map, and a point cloud at 24 pt. Expected: the content scrolls rather than clips, and numbers appear on hover or keyboard focus. Tests in Tasks 13, 14 and 17, and the Task 18 renders.

---

### Task 1: Shared live text for result lines (AA-034)

**Files:**
- Create: `src/AIAnatomy.App/Controls/LiveText.cs`
- Modify: every lab view that raises its own LiveRegionChanged (`SearchView`, `ExpertSystemView`, `ClassicMlView`, `RewardView` and their code-behind), and the Stage 1 views named in AA-034: `SamplingView.xaml` lines ~43 and ~110, `RulesVsLearningView.xaml` ~50, `TrainView.xaml` ~25.
- Test: `tests/AIAnatomy.App.Tests/LiveTextXamlTests.cs`

**Interfaces:**
- Produces: the attached property `c:LiveText.IsLive` (bool) for a `TextBlock`. Setting it True sets `AutomationProperties.LiveSetting="Polite"` and raises `LiveRegionChanged` whenever the bound `Text` changes. It never raises when the value hasn't changed, and never on layout. Every later lab task uses it for its result lines.

- [ ] **Step 1: Write the failing test** `LiveTextXamlTests.NoTextBlockHidesItsBoundText`. It reads every `src/AIAnatomy.App/Views/*.xaml` as XML and fails for any `TextBlock` that has both `Text="{Binding …}"` and `AutomationProperties.Name`, naming the file and line. A second test, `ResultLinesUseLiveText`, fails if any view's code-behind still contains its own `OnLiveTextUpdated`.
- [ ] **Step 2: Run** `dotnet test tests/AIAnatomy.App.Tests --filter "FullyQualifiedName~LiveText"`. Expect it to FAIL, listing the four Stage 1 sites and the Part A handlers.
- [ ] **Step 3: Implement `LiveText`.** Replace each view's hand-written handler with `c:LiveText.IsLive="True"`, and remove the hiding names, keeping a separate visible or `HelpText` label where a name described the block.
- [ ] **Step 4: Run** the filter, then the full suite. PASS. Build the App and run `--selftest all-labs` once, expecting exit code 0.
- [ ] **Step 5: Log** under `## Stage 2 Part B` in `docs/decisions.md`, and mark AA-034 Fixed in the issues sheet with the commit. **Commit** `Stage 2B: shared live text for result lines (AA-034)`.

### Task 2: Part keys, colours and icons for vision, memory and diffusion

**Files:** `src/AIAnatomy.Lessons/Parts.cs`, both theme files, `src/AIAnatomy.App/Controls/PartResource.cs`, `tests/AIAnatomy.App.Tests/ThemeContrastTests.cs`.

**Interfaces:**
- Produces: `Parts.Family` gains `vision`, `memory` and `diffusion`, in that order, so `Parts.All` has 19 keys. It also gains the brushes `PartVisionBrush`, `PartMemoryBrush` and `PartDiffusionBrush` in both themes, and three new glyphs.

- [ ] **Step 1: Update the tests.**
  - `PartsIsKnown_AcceptsOnlyListedKeys` expects 19 keys.
  - The family distance group is the eight family keys plus `later`, with the same thresholds: 15 for normal vision, 8 for protanopia and 8 for deuteranopia.
  - `EveryPartKeyHasItsOwnGlyph` covers all 19.
- [ ] **Step 2: Run** `dotnet test tests/AIAnatomy.App.Tests`. FAIL.
- [ ] **Step 3: Implement.** Each new colour is one hue family in both themes, lighter in Dark and darker in Light (Part A's rule). Don't change any existing colour; if no set passes, report rather than move an existing one. Glyphs come from Segoe Fluent Icons, named in a comment.
- [ ] **Step 4: Run.** PASS. **Log** the colours and glyphs, and **commit** `Stage 2B: part keys for vision, memory and diffusion`.

### Task 3: Core operations — tanh, reshape, convolution, pooling

**Files:**
- Create: `src/AIAnatomy.Core/Ops.Image.cs`
- Modify: `src/AIAnatomy.Core/Ops.Neural.cs` (Tanh), `src/AIAnatomy.Core/Ops.Matrix.cs` (Reshape)
- Test: `tests/AIAnatomy.Core.Tests/ImageOpsTests.cs`, `tests/AIAnatomy.Core.Tests/NeuralOpsTests.cs`

**Interfaces:**
- Produces, in `public static partial class Ops`:
  ```csharp
  public static Tensor Tanh(Tensor a);                         // any shape
  public static Tensor Reshape(Tensor a, params int[] shape);  // same Size; gradient passes straight through
  public static Tensor Conv2d(Tensor input, Tensor weights, Tensor bias, int kernel, int padding);
      // input [N,C,H,W]; weights [F, C*kernel*kernel] (row f is filter f, laid out c, then ky, then kx);
      // bias [F]; stride 1, zero padding; output [N, F, H+2p-kernel+1, W+2p-kernel+1]
  public static Tensor MaxPool2d(Tensor input, int size);      // [N,C,H,W] -> [N,C,H/size,W/size]; non-overlapping windows
  ```
- The op names are `"tanh"`, `"reshape"`, `"conv2d"` and `"maxpool2d"`.
- Errors are `ArgumentException`s with messages starting with the op name, for example `"conv2d: weights must be [F, C*k*k] = [?, 9] but got [8, 4]."`.
- `MaxPool2d` throws when H or W is not divisible by `size`. When a window has tied maxima, the first in row-major order takes the gradient.
- Conv2d is built by unfolding each image into columns and calling the same deterministic matrix product the engine already uses. Results must be identical run to run, including in parallel.

- [ ] **Step 1: Write the failing tests:**
  - **Gradient checks**, `GradCheck.MaxRelativeError(...) < 1e-6` through the `Weighted` wrapper:
    - Tanh on a [3,4] input.
    - Reshape [2,6] to [3,4].
    - Conv2d with input [2,2,5,5], weights [3,18], bias [3], kernel 3, padding 1; and again with padding 0.
    - MaxPool2d [1,2,4,4] with size 2, on distinct inputs.
  - **Hand-worked:**
    - One 3×3 image `[[1,2,3],[4,5,6],[7,8,9]]`, an all-ones 3×3 filter, bias 0 and padding 1 give the output `[[12,21,16],[27,45,33],[24,39,28]]`.
    - The same image with padding 0 gives `[[45]]`.
    - Bias 1 adds 1 everywhere.
    - MaxPool of `[[1,3,2,0],[4,2,1,5],[0,1,7,6],[2,3,8,1]]` with size 2 gives `[[4,5],[3,8]]`.
  - **Errors:** a wrong weight shape, a mismatched bias length, an odd size for MaxPool, and a Reshape that changes Size, each with its message.
  - **Determinism:** Conv2d on a [16,8,14,14] input gives bit-identical results in two runs.
- [ ] **Step 2: Run** `dotnet test tests/AIAnatomy.Core.Tests`. FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run.** PASS. **Log** in `docs/decisions.md` that convolution is done by unfolding, how the weights are laid out, and the tie rule. **Commit** `Stage 2B: tanh, reshape, convolution and pooling ops`.

### Task 4: Model file version 2 for the family networks

**Files:**
- Create: `src/AIAnatomy.Models/Files/ModelContainer.cs` (internal: the shared header and float64 parameter blocks, taken out of `ModelFile`), `src/AIAnatomy.Models/Files/FamilyModelFile.cs`
- Modify: `src/AIAnatomy.Models/Language/ModelFile.cs` (uses the container; behaviour for version 1 unchanged), `src/AIAnatomy.Models/Language/ModelInfo.cs`
- Test: `tests/AIAnatomy.Models.Tests/FamilyModelFileTests.cs`, `tests/AIAnatomy.Models.Tests/ModelFileTests.cs`

**Interfaces:**
- Produces:
  ```csharp
  public sealed record ModelInfo(string Name, string TrainingTextName, int StepsTrained, double? ValidationLoss, int Seed,
      bool IsShipped, IReadOnlyDictionary<string, double>? Results = null);   // Results: measured numbers, e.g. "testAccuracy"
  public static class FamilyModelFile
  {
      public const int Version = 2;
      public static void Save(Stream s, string kind, object config, string? vocabulary, ModelInfo info, IReadOnlyList<Parameter> parameters);
      public static FamilyModelData Load(Stream s);   // InvalidDataException with a plain message on any problem
  }
  public sealed record FamilyModelData(string Kind, JsonElement Config, string? Vocabulary, ModelInfo Info,
      IReadOnlyList<(string Name, int[] Shape, double[] Values)> Parameters);
  public static class FamilyKinds { public const string Digits = "digit-network", Memory = "memory-network", Diffusion = "diffusion-network"; }
  ```
- Version 2 header: one JSON line `{ format: "aianatomy-model", version: 2, kind, config, vocabulary, info, parameters: [{name, shape}] }`, followed by little-endian float64 values, exactly as in D14.
- `ModelFile` (the transformer) still writes version 1 and reads version 1 only.
  - Given a version 2 file, it throws `"This file holds a {kind}, not a language model."`.
  - `FamilyModelFile` given a version 1 file throws `"This is a language model file, not a family network."`.
  - Any other version throws D14's existing message.
- Family files live in `content/models/family/` (a subfolder). `ModelLibrary.List()` reads only the top-level folder, so the language-model picker never sees them. A test pins this.
- Each network type (Tasks 7, 9 and 10) adds its own `Save(Stream)` and `static Load(Stream)` on top of `FamilyModelFile`. Each checks that the kind, config and parameter names and shapes match.

- [ ] **Step 1: Write the failing tests:**
  - **Round trip:** save a kind, a config, a vocabulary and two parameters, then load them; everything is equal, including `Results`.
  - **Wrong-kind messages:** both directions.
  - **Corrupted files:** truncated, trailing bytes, a NaN value, a shape that doesn't match its data, and version 3. Each gives an `InvalidDataException` with a plain message.
  - **Stage 1 files still load:**
    - `ShippedStage1Files_StillLoad`: `fables-small.aimodel` and `induction-demo.aimodel` load through `ModelFile.Load` with their existing names and losses.
    - A user model saved by `ModelLibrary.Save` round-trips, and an info without `Results` reads back as null.
  - **Library:** a family file placed in a `family/` subfolder of the shipped folder is not listed by `ModelLibrary.List()`.
- [ ] **Step 2: Run** `dotnet test tests/AIAnatomy.Models.Tests --filter "FullyQualifiedName~ModelFile"`. FAIL.
- [ ] **Step 3: Implement.** The refactor must leave the version 1 bytes unchanged: a test saves the Fables model and compares the bytes with the shipped file's parameter section.
- [ ] **Step 4: Run** the filter, then the full suite. PASS. **Log** "D14, version 2 for family networks" (kinds, folder, messages). **Commit** `Stage 2B: model file version 2 for family networks`.

### Task 5: Shared stepped training and the retrain runner

**Files:**
- Create: `src/AIAnatomy.Models/Training/SteppedTraining.cs`, `src/AIAnatomy.Presentation/Labs/RetrainRunner.cs`
- Test: `tests/AIAnatomy.Models.Tests/SteppedTrainingTests.cs`, `tests/AIAnatomy.Presentation.Tests/Labs/RetrainRunnerTests.cs`

**Interfaces:**
- Produces (Models, namespace `AIAnatomy.Models.Training`):
  ```csharp
  public interface ISteppedTrainer { int TotalSteps { get; } int StepsTaken { get; } double Step(); }   // one optimisation step; returns its training loss
  public sealed record TrainingProgress(int Step, int TotalSteps, double Loss);
  public static class SteppedTraining
  {
      public const string DivergedMessage = "The training went wrong: the loss stopped being a number. Try again with another seed.";
      public static Task<string?> RunAsync(ISteppedTrainer trainer, IProgress<TrainingProgress> progress, CancellationToken token, int reportEvery = 10);
      // runs on a worker thread until TotalSteps, cancellation (returns null), or a non-finite or > 1e4 loss (returns DivergedMessage)
  }
  ```
- Produces (Presentation, namespace `AIAnatomy.Presentation.Labs`):
  ```csharp
  public sealed partial class RetrainRunner : ObservableObject
  {
      public RetrainRunner(Func<int, ISteppedTrainer> createTrainer);   // seed -> a fresh trainer on a fresh network
      public bool IsRunning { get; }                                   // observable
      public string StatusText { get; }                                // "Step 120 of 600 · loss 0.412 · 38 s"; "Stopped after 120 steps."; DivergedMessage
      public IReadOnlyList<LineSeries> LossSeries { get; }             // one series, "training loss"
      public ISteppedTrainer? Finished { get; }                        // set when a run completes all its steps
      public event EventHandler? Completed;                            // raised on the UI thread after Finished is set
      public Task StartAsync(int seed);                                // ignored while running
      public void Stop();                                              // keeps what was trained so far: Finished stays null, StatusText says stopped
      public void Abandon();                                           // Reset/Deactivate/seed change: cancels and drops every late report
      public TimeSpan Elapsed { get; }
  }
  ```
  UI updates happen at most 20 times a second. A lab wraps `StartAsync` in its own `BeginRun` scope.

- [ ] **Step 1: Write the failing tests.** Use a fake trainer whose losses are scripted, with a gate so no timing is involved:
  - It runs to TotalSteps and reports every `reportEvery` steps plus the last.
  - Cancellation returns null and takes no further step.
  - A NaN loss, and a loss of 2e4, each return DivergedMessage.
  - `Abandon` during a run means no later report changes `StatusText` or `LossSeries`.
  - `Stop` leaves `Finished` null.
  - A completed run sets `Finished` and raises `Completed` once.
  - `StartAsync` while running does nothing.
- [ ] **Step 2: Run** the two test classes. FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run.** PASS. **Commit** `Stage 2B: stepped training and the retrain runner`.

### Task 6: The digits dataset — GATED on the owner's OK

**Controller gate, before dispatching:**
- **Find the file:** find the 28×28 handwritten digits (MNIST, LeCun, Cortes and Burges) from an authoritative source or mirror. Confirm its licence from the source page; MNIST is generally given as CC BY-SA 3.0, but the builder must read it.
- **Ask the owner, in one message:** each file's name, source URL and size, and the licence. Also give the fallback, the UCI 8×8 optical digits (its files, source, size and licence).
- **Wait for a yes.** On a no, or when no 28×28 licence can be confirmed, use the 8×8 set (asking the same way if it too needs a download) and tell the implementer which set to use.

**Files:**
- Create: `src/AIAnatomy.Models/Vision/DigitSet.cs`, `content/data/digits/digits-train.bin.gz`, `content/data/digits/digits-test.bin.gz`
- Modify: `tools/AIAnatomy.Pretrain/Program.cs` (subcommand `digits-subset`), `THIRD-PARTY-NOTICES.txt`
- Test: `tests/AIAnatomy.Models.Tests/Vision/DigitSetTests.cs`

**Interfaces:**
- Produces (namespace `AIAnatomy.Models.Vision`):
  ```csharp
  public sealed class DigitSet
  {
      public static DigitSet Load(string path);              // InvalidDataException naming the file on any problem
      public static void Save(string path, int imageSize, IReadOnlyList<(byte Label, byte[] Pixels)> images);
      public int Count { get; } public int ImageSize { get; }
      public int Label(int index); public double[] Pixels(int index);   // 0..1, row by row
      public Tensor Batch(IReadOnlyList<int> indices);       // [N, 1, ImageSize, ImageSize]
      public IReadOnlyList<int> Labels { get; }
  }
  ```
- File format, gzip-compressed:
  - ASCII `AIDIGITS` (8 bytes), then int32 version 1, int32 count, and int32 imageSize, little-endian.
  - Per image: one label byte (0–9), then imageSize² pixel bytes (0–255, 0 is background).
- `digits-subset --images <idx[.gz]> --labels <idx[.gz]> --per-class N --out <file>` reads the IDX format (magic 2051 and 2049, big-endian). It takes the first N images of each digit in file order, so the subset is deterministic.
  - Train: 800 per digit from the training files (8,000).
  - Test: 200 per digit from the test files (2,000).
  - The 8×8 fallback uses UCI's own split, the same per-class rule, its 0–16 values scaled to 0–255, and the same file format with imageSize 8.

- [ ] **Step 1: Write the failing tests:**
  - **Round trip:** Save then Load gives the same data.
  - **Rejected files:** wrong magic, a label outside 0–9, a short file and trailing bytes each give an `InvalidDataException` naming the file.
  - **`Batch` shape and values.**
  - **The shipped subsets** (once built):
    - Counts are 8,000 and 2,000 (or the 8×8 numbers), with every digit exactly balanced.
    - `ImageSize` is 28 (or 8).
    - No test image is byte-identical to a training image.
- [ ] **Step 2: Run** `dotnet test tests/AIAnatomy.Models.Tests --filter "FullyQualifiedName~DigitSet"`. FAIL.
- [ ] **Step 3: Implement** `DigitSet` and `digits-subset`. Run the tool on the downloaded files, which stay in the scratchpad and never in the repo. Write the two `.bin.gz` files into `content/data/digits/`.
- [ ] **Step 4: THIRD-PARTY-NOTICES.txt.** Add a block giving the dataset's name, authors, source, licence, the subset taken (how many per digit, and how they were chosen), and the conversion.
- [ ] **Step 5: Run.** PASS. **Log** the source, the licence confirmation, file sizes and which set was used. **Commit** `Stage 2B: handwritten digits subset`.

### Task 7: The image network (DigitNet), its trainer and the speed check

**Files:**
- Create: `src/AIAnatomy.Models/Vision/DigitNet.cs`, `src/AIAnatomy.Models/Vision/DigitTrainer.cs`
- Modify: `tools/AIAnatomy.Pretrain/Program.cs` (subcommand `speed`)
- Test: `tests/AIAnatomy.Models.Tests/Vision/DigitNetTests.cs`

**Interfaces:**
- Consumes: `Ops.Conv2d`, `MaxPool2d`, `Reshape` and `Relu` (Task 3); `ISteppedTrainer` (Task 5); `DigitSet` (Task 6); `FamilyModelFile` (Task 4).
- Produces:
  ```csharp
  public sealed record DigitNetConfig(int ImageSize = 28, int Filters1 = 8, int Filters2 = 16, int Hidden = 64)
  { public long CountParameters(); }      // kernel 3, padding 1, 2x2 max pooling after each convolution; ImageSize divisible by 4
  public sealed class DigitNet : Module
  {
      public DigitNet(DigitNetConfig config, SeededRandom rng);   // He-normal weights (std sqrt(2/fan-in)), zero biases
      public DigitNetConfig Config { get; }
      public DigitTrace Forward(Tensor images);                   // images [N,1,S,S], values 0..1
      public override IEnumerable<Parameter> Parameters();        // conv1.weight, conv1.bias, conv2.weight, conv2.bias, fc1.weight, fc1.bias, fc2.weight, fc2.bias
      public void Save(Stream s, ModelInfo info); public static (DigitNet Net, ModelInfo Info) Load(Stream s);
  }
  public sealed record DigitTrace(Tensor Conv1, Tensor Pool1, Tensor Conv2, Tensor Pool2, Tensor Hidden, Tensor Logits)
  { public double[] Probabilities(int image); }               // Conv1 and Conv2 are after ReLU
  public sealed class DigitTrainer : ISteppedTrainer
  {
      public DigitTrainer(DigitNet net, DigitSet train, int totalSteps, int seed, int batchSize = 32, double learningRate = 1e-3);   // Adam, gradient norm clipped to 1.0
      public static double Accuracy(DigitNet net, DigitSet test);   // fraction right, in batches
  }
  ```
- **The speed check** (spec 6; this comes before any lab work):
  - `speed --kind digits --config <f1,f2,hidden> --images 256` times forward plus backward per image in Release.
  - The builder runs it on the default config. If one pass over the 8,000 training images at the measured rate would take over 90 s, use the spec's fallback order (shrink, then fewer images or steps) until it fits.
  - The chosen settings go into `public static class DigitTraining { public static DigitNetConfig Retrain { get; } public static DigitNetConfig Shipped { get; } public const int RetrainSteps = …, ShippedSteps = …; }`, and into decisions with the measured ms per image. The shipped config may be larger. Tasks 9 and 10 add `MemoryTraining` (`RnnConfig Retrain(int vocabularySize)`, `RnnConfig Shipped(int vocabularySize)`, `RetrainSteps`, `ShippedSteps`) and `DiffusionTraining` (`DiffusionConfig Retrain`, `DiffusionConfig Shipped`, `RetrainSteps`, `ShippedSteps`) the same way. Labs and Pretrain read only these classes.

- [ ] **Step 1: Write the failing tests:**
  - **Shapes:** for [2,1,28,28], Conv1 is [2,8,28,28], Pool1 [2,8,14,14], Conv2 [2,16,14,14], Pool2 [2,16,7,7], Hidden [2,64] and Logits [2,10]. The same holds for ImageSize 8.
  - **Parameter count:** 52,138 for the default config.
  - **Probabilities sum to 1.**
  - **Learning** (in the Training collection): on 200 images of the shipped training subset, 150 steps lower the mean training loss below 80% of its first value.
  - **Save and Load:** round trip, with the wrong-kind message.
  - **Determinism:** the same seed gives the same weights after 20 steps.
- [ ] **Step 2: Run** `dotnet test tests/AIAnatomy.Models.Tests --filter "FullyQualifiedName~DigitNet"`. FAIL.
- [ ] **Step 3: Implement**, then run the speed check in Release and set the constants.
- [ ] **Step 4: Run** the filter. PASS. **Log** the speed check (times, the choice, and the fallback taken if any). **Commit** `Stage 2B: image network, trainer and speed check`.

### Task 8: Drawing a digit — the drawing pad and the preprocessing

**Files:**
- Create: `src/AIAnatomy.Models/Vision/DigitImage.cs`, `src/AIAnatomy.App/Controls/DrawingPad.cs`
- Test: `tests/AIAnatomy.Models.Tests/Vision/DigitImageTests.cs`

**Interfaces:**
- Produces:
  ```csharp
  public static class DigitImage
  {
      public static double[]? FromStrokes(IReadOnlyList<IReadOnlyList<(double X, double Y)>> strokes, int imageSize = 28);
      // null when there is no ink. The MNIST method: the ink's bounding box is scaled, keeping its proportions, so its longer side
      // is 20/28 of imageSize; the strokes are drawn anti-aliased with a pen imageSize/14 px wide; then the image is shifted
      // so its centre of mass is at the centre. For the 8x8 fallback: draw at 32x32 and sum 4x4 blocks, as UCI did.
  }
  ```
  `DrawingPad` is an App control (FrameworkElement) on the pattern of `ClassPlot`:
  - A dependency property `Strokes` (`IReadOnlyList<IReadOnlyList<Point>>`) and an event `StrokesChanged`.
  - It draws in pad coordinates, with a mouse or pen and a visible stroke, and has a public `Clear()`.
  - It is focusable but not drawable by keyboard. Each lab using it offers ready-made samples.
  - An empty pad shows a muted "Draw here" in words.

- [ ] **Step 1: Write the failing tests:**
  - An empty list, or strokes with no length, give null.
  - A single vertical line gives an image whose ink is centred (centre of mass within 0.5 px of the centre). The ink's height is 20 ± 1 px and its width about the pen width.
  - The same digit drawn small in a corner and large in the middle gives images within a small mean difference (under 0.05).
  - A dot gives a small centred blob, never a blown-up square.
  - All values are in 0..1.
- [ ] **Step 2: Run** `dotnet test tests/AIAnatomy.Models.Tests --filter "FullyQualifiedName~DigitImage"`. FAIL.
- [ ] **Step 3: Implement** `DigitImage` and `DrawingPad`.
- [ ] **Step 4: Run.** PASS. **Log** the preprocessing (cite the MNIST page's description if it is confirmed). **Commit** `Stage 2B: drawing pad and digit preprocessing`.

### Task 9: The memory network (Elman RNN)

**Files:**
- Create: `src/AIAnatomy.Models/Recurrent/ElmanRnn.cs`, `src/AIAnatomy.Models/Recurrent/RnnTrainer.cs`, `src/AIAnatomy.Models/Recurrent/ContextLoss.cs`, `src/AIAnatomy.Models/Recurrent/RecordedResults.cs`
- Test: `tests/AIAnatomy.Models.Tests/Recurrent/ElmanRnnTests.cs`

**Interfaces:**
- Consumes: `Ops.Tanh` (Task 3), `ISteppedTrainer` (Task 5), `CharTokenizer`, `CorpusBatchSource`, `Losses.CrossEntropy`, `FamilyModelFile`.
- Produces:
  ```csharp
  public sealed record RnnConfig(int VocabularySize, int Hidden = 128);
  public sealed class ElmanRnn : Module
  {
      public ElmanRnn(RnnConfig config, SeededRandom rng);
      // parameters: input.weight [V,H], recurrent.weight [H,H], hidden.bias [H], output.weight [H,V], output.bias [V]
      // h_t = tanh(x_t W_in + h_{t-1} W_rec + b), h_0 = 0; logits_t = h_t W_out + b_out (Elman 1990)
      public RnnForward Forward(IReadOnlyList<IReadOnlyList<int>> sequences);   // equal lengths
      public void Save(Stream s, CharTokenizer tokenizer, ModelInfo info);
      public static (ElmanRnn Rnn, CharTokenizer Tokenizer, ModelInfo Info) Load(Stream s);
  }
  public sealed record RnnForward(IReadOnlyList<Tensor> Hidden, Tensor Logits);   // Hidden[t] is [B,H]; Logits rows in position-major order (t*B + b)
  public sealed class RnnTrainer : ISteppedTrainer { public RnnTrainer(ElmanRnn rnn, IBatchSource source, int totalSteps, int seed, int batchSize = 8, double learningRate = 3e-3); }   // cross-entropy, clip 1.0, Adam
  public static class ContextLoss
  {
      public static double[] LossByPosition(Transformer model, IReadOnlyList<IReadOnlyList<int>> windows);
      public static double[] LossByPosition(ElmanRnn rnn, IReadOnlyList<IReadOnlyList<int>> windows);
      // mean cross-entropy at each position 0..n-1 of the windows (n+1 tokens each: inputs then targets), so the curves compare
  }
  public static class RnnSampling { public static string Generate(ElmanRnn rnn, CharTokenizer tokenizer, string start, int count, double temperature, SeededRandom rng); }
  public static class RecordedResults
  {   // the shipped Fables transformer, as recorded in D20 (decisions.md); it is not retrained here
      public const double FablesValidationLoss = 1.711; public const int FablesSteps = 3000; public const double FablesSeconds = 517;
      public const int FablesBatchSize = 8; public const int FablesContextLength = 64;
      public static double FablesCharactersPerSecond => FablesSteps * FablesBatchSize * FablesContextLength / FablesSeconds;
  }
  ```
- The RNN uses **the shipped Fables model's tokenizer**. Its training and validation windows come from `CorpusBatchSource` built exactly as the Fables model's were (normalised fables, context 64, validation fraction 0.1, seed 1), so the two models' losses are measured on the same validation windows.
- **Speed check:** `speed --kind memory --hidden H` times one training step in Release. Choose the retrain hidden size and steps so "train it again" takes about 2 minutes or less (spec fallback order), and fill in `MemoryTraining` (see Task 7). Log the times.

- [ ] **Step 1: Write the failing tests:**
  - **Shapes**, and the logits' row order.
  - **Gradient check:** on a tiny RNN (V 5, H 4, 3 steps), the training loss's gradient is checked with `GradCheck`, error under 1e-6.
  - **Learning:** 100 steps on the fables lower the validation loss below the untrained value (ln V) by at least 0.5 (Training collection).
  - **LossByPosition:** both curves have length 64. On the shipped Fables model, the mean of positions 32–63 is below position 0.
  - **Same windows:** the RNN's validation windows equal the Fables model's.
  - **Unknown characters:** `RnnSampling` with a start containing a character outside the vocabulary maps it to the unknown token and doesn't throw; an empty start throws `ArgumentException`.
  - **Recorded results:** `RecordedResults.FablesValidationLoss` equals the shipped file's `ValidationLoss`, rounded to 3 decimals.
  - **Save and Load:** round trip.
- [ ] **Step 2: Run** `dotnet test tests/AIAnatomy.Models.Tests --filter "FullyQualifiedName~ElmanRnn"`. FAIL.
- [ ] **Step 3: Implement**, then run the speed check.
- [ ] **Step 4: Run.** PASS. **Log** the RNN, the same-windows rule, the recorded results and the speed check. **Commit** `Stage 2B: memory network (Elman RNN)`.

### Task 10: Diffusion on 2-D points (DDPM)

**Files:**
- Create: `src/AIAnatomy.Models/Diffusion/NoiseSchedule.cs`, `DiffusionNet.cs`, `DiffusionTrainer.cs`, `DiffusionSampler.cs`, `Shapes.cs`
- Test: `tests/AIAnatomy.Models.Tests/Diffusion/DiffusionTests.cs`

**Interfaces:**
- Consumes: Core ops, `ISteppedTrainer`, `Losses.MeanSquaredError`, `FamilyModelFile`.
- Produces:
  ```csharp
  public sealed class NoiseSchedule
  {
      public NoiseSchedule(int steps = 100, double betaStart = 1e-4, double betaEnd = 0.1);   // linear betas, t = 1..Steps (Ho et al. 2020)
      public int Steps { get; } public double Beta(int i); public double AlphaBar(int i);
      public IReadOnlyList<int> Timesteps { get; }        // the original t of each step; 1..Steps for a full schedule
      public NoiseSchedule Respaced(int steps);            // evenly spaced subsequence; beta'_i = 1 - AlphaBar(t_i)/AlphaBar(t_{i-1}) (Nichol & Dhariwal 2021)
  }
  public sealed record DiffusionConfig(int Hidden = 128, int TimeFeatures = 16);   // time features: sin and cos of t/Steps at 8 frequencies
  public sealed class DiffusionNet : Module
  {
      public DiffusionNet(DiffusionConfig config, SeededRandom rng);   // [x, y, time features] -> Hidden -> Hidden -> 2, ReLU; predicts the noise
      public Tensor Forward(Tensor points, IReadOnlyList<int> t, int scheduleSteps);   // points [N,2] -> [N,2]
      public void Save(Stream s, NoiseSchedule schedule, string shapeName, ModelInfo info);
      public static (DiffusionNet Net, NoiseSchedule Schedule, string ShapeName, ModelInfo Info) Load(Stream s);
  }
  public static class DiffusionSampler
  {
      public static (double[,] Noisy, double[,] Noise) AddNoise(IReadOnlyList<(double X, double Y)> shape, int t, NoiseSchedule s, SeededRandom rng);
      public static IReadOnlyList<double[,]> Sample(DiffusionNet net, NoiseSchedule s, int count, SeededRandom rng);
      // index 0 = pure noise x_T, last = the result; DDPM ancestral sampling with sigma_t^2 = beta_t, no noise at the last step;
      // a respaced schedule passes its original t values to the net
  }
  public sealed class DiffusionTrainer : ISteppedTrainer
  { public DiffusionTrainer(DiffusionNet net, NoiseSchedule s, IReadOnlyList<(double X, double Y)> shape, int totalSteps, int seed, int batchSize = 128, double learningRate = 1e-3); }   // epsilon-prediction, MSE (Ho et al. 2020)
  public static class Shapes
  {
      public const int PointCount = 1000;
      public static IReadOnlyList<(string Name, IReadOnlyList<(double X, double Y)> Points)> Presets { get; }   // "Spiral", "Circle", "Two moons", "Letter S"; inside [-1,1]
      public static ShapeResult FromStroke(IReadOnlyList<(double X, double Y)> stroke, double padSize);
      public static double MeanDistanceToShape(IReadOnlyList<(double X, double Y)> samples, IReadOnlyList<(double X, double Y)> shape);   // the sample-quality measure (lower is better)
  }
  public sealed record ShapeResult(IReadOnlyList<(double X, double Y)>? Points, string? Error);
  ```
- `FromStroke` resamples the stroke to `PointCount` points evenly spaced along its length, mapped into [-1,1] with the pad's centre at 0 (y up).
  - Fewer than 2 points, or a total length under 25% of the pad size, gives `Error = "Draw a longer line: this one is too short to learn a shape from."`.
  - A bounding box under 15% of the pad on both axes gives `Error = "Draw bigger: this shape is too small to learn from."`.
- **Speed check:** `speed --kind diffusion` times one step. Fill in `DiffusionTraining` (see Task 7) so "train it again" takes about 2 minutes or less; the shipped steps may be more. Log them.

- [ ] **Step 1: Write the failing tests:**
  - **Schedule:** AlphaBar(100) < 0.01, `Timesteps` is 1..100, and `Respaced(100)` equals the full schedule (each beta within 1e-12).
  - **Respaced(10):** has 10 steps with t values 10, 20, …, 100, and its AlphaBar at each step equals the full schedule's at that t.
  - **AddNoise:** at t = 1 the points stay near the shape; at t = 100 the mean squared radius is near 2, the variance of two unit normals.
  - **Learning** (Training collection): 400 steps on "Circle" lower the training loss below 70% of the first 20 steps' mean. Sampling 500 points then gives `MeanDistanceToShape` below that of pure noise, by at least half.
  - **Sampling:** the result has Steps + 1 frames, and the same seed gives the same frames.
  - **Presets:** each has 1,000 points in [-1,1].
  - **FromStroke:** the two error messages; a good stroke gives 1,000 points with equal spacing (within 1%).
  - **MeanDistanceToShape:** a hand-worked case, points on the shape give 0.
  - **Save and Load:** round trip.
- [ ] **Step 2: Run** `dotnet test tests/AIAnatomy.Models.Tests --filter "FullyQualifiedName~Diffusion"`. FAIL.
- [ ] **Step 3: Implement**, then run the speed check.
- [ ] **Step 4: Run.** PASS. **Log** the schedule, the network, the respacing and its source, the quality measure and the speed check. **Commit** `Stage 2B: diffusion on 2-D points`.

### Task 11: Train and ship the family networks

**Files:** `tools/AIAnatomy.Pretrain/Program.cs` (subcommands `digits`, `memory`, `diffusion`), `content/models/family/*.aimodel`, `THIRD-PARTY-NOTICES.txt`, `tests/AIAnatomy.Models.Tests/ShippedFamilyModelTests.cs`.

**Interfaces:**
- Consumes: Tasks 6, 7, 9 and 10.
- Produces these shipped files in `content/models/family/`:
  - `digits.aimodel`: Results `testAccuracy` and `trainSeconds`.
  - `memory.aimodel`: Results `validationLoss`, `trainSeconds` and `charactersPerSecond`.
  - `diffusion-spiral.aimodel`, `diffusion-circle.aimodel`, `diffusion-two-moons.aimodel` and `diffusion-letter-s.aimodel`: Results `meanDistance` and `trainSeconds`.
  - Every file has `IsShipped` true and seed 1, and is trained in Release with the `Shipped` configs from Tasks 7, 9 and 10.
- Run, for example: `dotnet run -c Release --project tools/AIAnatomy.Pretrain -- digits --train content/data/digits/digits-train.bin.gz --test content/data/digits/digits-test.bin.gz --out content/models/family/digits.aimodel --seed 1`. The memory and diffusion runs are similar. Long runs go in the background.

- [ ] **Step 1: Write the failing tests** (`ShippedFamilyModelTests`). Each file loads through its own type's `Load`, with the right kind and with `IsShipped` set.
  - The digits network's accuracy on the bundled test set equals `Results["testAccuracy"]` within 1e-9 and is at least 0.95 (0.90 for the 8×8 set).
  - The memory network's validation loss on the shared validation windows equals `Results["validationLoss"]` within 1e-6 and is below 2.3. Its sample from "The " is at least 30% known words, using the existing generation test's word list.
  - Each diffusion model's `MeanDistanceToShape` for 500 samples with seed 1 equals its recorded value within 1e-9 and is under 0.05.
  - Each file is under 1 MB.
- [ ] **Step 2: Implement** the three subcommands, run them in Release, and run the tests. PASS.
  - If a shipped network misses its threshold, take the spec's order: a larger shipped config or more steps first. Record what was done.
- [ ] **Step 3: Records.**
  - In decisions, a D20-style entry for each file: config, parameter count, steps, time and results.
  - `THIRD-PARTY-NOTICES.txt`: the models block names the new files, trained by this project's code on the bundled data, the fables, or the generated shapes.
  - **Commit** `Stage 2B: shipped family networks`.

### Task 12: Shared lab pieces — family model service and image views

**Files:**
- Create: `src/AIAnatomy.Presentation/Services/FamilyModels.cs`, `src/AIAnatomy.App/Controls/FeatureMapGrid.cs`, `src/AIAnatomy.App/Controls/PointCloudPlot.cs`
- Modify: `src/AIAnatomy.Presentation/LabFactory.cs`
- Test: `tests/AIAnatomy.Presentation.Tests/FamilyModelsTests.cs`

**Interfaces:**
- Produces:
  ```csharp
  public sealed class FamilyModels   // loads the shipped family networks and data once per window, lazily, off the UI thread
  {
      public FamilyModels(string contentFolder);
      public Task<(DigitNet Net, ModelInfo Info)> DigitsAsync();
      public Task<DigitSet> DigitTestSetAsync(); public Task<DigitSet> DigitTrainSetAsync();
      public Task<(ElmanRnn Rnn, CharTokenizer Tokenizer, ModelInfo Info)> MemoryAsync();
      public Task<(DiffusionNet Net, NoiseSchedule Schedule, string ShapeName, ModelInfo Info)> DiffusionAsync(string presetName);
  }
  ```
  - Every call returns a fresh copy of the network (the same loaded file, cloned), so a lab's "train it again" never touches another lab's network.
  - A missing or bad file gives an `InvalidDataException` that the lab shows as a message.
  - `LabFactory` builds one `FamilyModels` and passes it to the Part B labs.
- `FeatureMapGrid` (App): shows a list of 2-D maps (`IReadOnlyList<double[,]>`) as small heat maps in a wrapping grid, each with a caption ("map 3").
  - A symmetric diverging colour ramp, with its legend and numbers.
  - Hovering, or keyboard focus with the arrow keys, shows the map and cell value in text under the grid.
  - It scrolls rather than clips at 24 pt.
- `PointCloudPlot` (App, derived from `ChartBase`): shows the target shape faintly, the current points, and optional arrows (`IReadOnlyList<(Point From, Point To)>`) for noise vectors.
  - Distinguished by marker shape and line style, not colour alone.
  - Axes from −3 to 3, or from the data.
  - Keyboard: focus shows the point count and the spread in text.

- [ ] **Step 1: Write the failing tests:**
  - Each `FamilyModels` call returns a working network.
  - Two calls return independent copies: changing one's weights leaves the other unchanged.
  - A missing file gives the message.
  - Loading happens once.
- [ ] **Step 2: Run** the filter. FAIL.
- [ ] **Step 3: Implement.** Render each new control once in a throwaway gallery case if the App already has a control gallery (`ControlGallery.xaml`, through `--render-png gallery`), and look at the picture.
- [ ] **Step 4: Run.** PASS. **Commit** `Stage 2B: family model service, feature-map grid and point-cloud plot`.

### Task 13: Lesson 4.6 — Image networks: filters

**Files:**
- Create: `src/AIAnatomy.Presentation/Labs/FiltersLabViewModel.cs`, `src/AIAnatomy.App/Views/FiltersView.xaml(.cs)`
- Modify: `LabFactory.cs`, `MainWindow.xaml`, `DiagnosticOptions.cs` (and `IsLabRenderView`), `PngRenderer.cs`, `content/lessons/family/06-image-filters.md`, `content/idea-map.json` (description), `content/connections.json`, `content/sources.json`
- Test: `tests/AIAnatomy.Presentation.Tests/Labs/FiltersLabTests.cs`, `CheckedQuestionTests.cs`, `CommandLineTests.cs`, `LessonContentTests.cs` (remove `image-filters` from `StillComingLater`)

**Interfaces:**
- Consumes: `FamilyModels` (the test set and the shipped DigitNet's conv1), `DigitImage`, `DrawingPad`, `Ops.Conv2d` (the feature map is computed by the same op the network uses), `FeatureMapGrid` or `HeatMap`, and `LiveText`.
- Produces `FiltersLabViewModel` (Id `"FiltersLab"`, part `vision`):
  - **Input:** a sample from the test set, chosen with Previous and Next or a digit picker 0–9 (the keyboard path), or the drawing (`UseDrawing(strokes)`).
  - **The filter:** `Filter` holds 9 editable numbers, each clamped to −10..10.
  - **Presets:**
    - Vertical edge `[-1,0,1; -1,0,1; -1,0,1]`
    - Horizontal edge (its transpose)
    - Blur (all 1/9)
    - Sharpen `[0,-1,0; -1,5,-1; 0,-1,0]`
    - Learned 1 to 8, the shipped network's first-layer filters, shown as 3×3 grids beside the presets.
  - **The feature map:** a 28×28 map, padding 1, bias 0, on a diverging ramp with numbers on hover.
  - **The count line:** "The same 9 numbers were used at all 784 positions."
  - **Screens:** `image` and `feature-map`.
  - **Render view:** `filters-learned` (learned filter 1 on sample 0).

- [ ] **Step 1: Write the failing tests:**
  - **Hand-worked:** the vertical-edge preset on a 28×28 image that is 0 on the left half and 1 on the right half gives 3 along the border columns and 0 far from it.
  - Selecting a learned filter gives exactly the shipped conv1 weights.
  - Edits outside −10..10 are clamped.
  - **The drawing:** an empty drawing gives the message; a drawn image replaces the sample.
  - **Screens:** baselines, and each lights once per change.
  - **Reset** restores sample 0 and the vertical-edge preset.
  - **CommandLine** test for the render view.
- [ ] **Step 2: Run** `dotnet test tests/AIAnatomy.Presentation.Tests --filter "FullyQualifiedName~FiltersLab|FullyQualifiedName~CommandLine"`. FAIL.
- [ ] **Step 3: Implement** the view-model and the view. Write the lesson:
  - Part `vision`, sources `lecun1998` plus `textbook`.
  - The explanation: convolution as a small window of the same weights slid over the image; what's new is the local window (a transformer also reuses its weights at every position); learned versus hand-made filters.
  - A `## Formula` for the 2-D convolution sum.
  - Try this, and 2–3 questions, one `checked-by-code: Filters_OneFilterIsUsedAt784Positions`.
  - Connections: `image-filters` → `feed-forward`, and → `image-network`.
  - Update its idea-map description.
- [ ] **Step 4: Run** the filters, Lessons.Tests and App.Tests. Build, then make 2 render launches: `image-filters,filters-learned` at Light 13 and at Dark 24, with `--full-lab`. Look at them. Run `--selftest all-labs` once. Then the full suite. PASS. **Commit** `Stage 2B: lesson 4.6, image filters`.

### Task 14: Lesson 4.7 — Image networks: the whole network

**Files:**
- Create: `src/AIAnatomy.Presentation/Labs/DigitNetworkLabViewModel.cs`, `src/AIAnatomy.App/Views/DigitNetworkView.xaml(.cs)`
- Modify: the factory, window, diagnostics and renderer as in Task 13; `content/lessons/family/07-image-network.md`; the idea map, connections and sources; and the tests, as in Task 13 (remove `image-network`).

**Interfaces:**
- Consumes: `FamilyModels`, `DigitNet`, `DigitTrace`, `DigitImage`, `DrawingPad`, `RetrainRunner` with `DigitTrainer` (the `Retrain` config), `FeatureMapGrid`, `ProbabilityBars` and `LineChart`.
- Produces `DigitNetworkLabViewModel` (Id `"DigitNetworkLab"`, part `vision`):
  - **Input:** the drawing or a test sample (the keyboard path).
  - **Outputs:**
    - `Probabilities` for 0–9.
    - `PredictionText`: "It reads this as a 7 (91%)."
    - `Layer1Maps` (8) and `Layer2Maps` (16), after ReLU.
  - **Train it again:**
    - The `Retrain` config, for `Retrain` steps, with the seed, a loss chart, and Stop.
    - On completion: test accuracy on the 2,000 test images, shown as "Right on 1,912 of 2,000 test digits (95.6%)". The trained network replaces the shipped one in the lab.
    - The lab says how the retrained network is smaller or trained for less time than the shipped one, if it is.
    - Reset restores the shipped network.
  - **The shipped network's recorded accuracy** is shown, labelled as recorded when it was trained.
  - **Screens:** `maps`, `probabilities`, `training`.
  - **Render view:** `digit-network-sample` (test sample 0).

- [ ] **Step 1: Write the failing tests:**
  - On open, the shipped network classifies sample 0 the same way `DigitNet` does directly.
  - The maps have the counts and sizes above.
  - An empty drawing gives "Draw a digit first, or pick a sample."
  - **Train it again** (with a small step count injected through a test hook):
    - It replaces the network and shows the accuracy line.
    - Stop keeps the shipped network.
    - Reset or Deactivate mid-run: no stale result.
    - A seed change mid-run abandons the run.
    - The `training` screen lights once, at the end.
  - **CommandLine** test.
- [ ] **Step 2: Run** the filter. FAIL.
- [ ] **Step 3: Implement**, and write the lesson:
  - Sources `lecun1998` plus `textbook`; the dataset is named in the text if it is cited.
  - The text covers layers of filters, pooling, a fully connected layer, and probabilities. It says the network sees only what it was trained on, which is why a drawing is scaled and centred like the training digits.
  - It is honest about mistakes on odd drawings.
  - A `## Formula` for max pooling and softmax.
  - A checked question on the shipped network's recorded test accuracy, `checked-by-code: DigitNetwork_ShippedAccuracyOnTheTestDigits`.
  - Connections: `image-network` → `learning`, and → `output-sampling` (softmax).
- [ ] **Step 4: Run** as in Task 13: renders `image-network,digit-network-sample` at Light 13 and Dark 24, the self-test (its all-labs entry does no training), and the full suite. PASS. **Commit** `Stage 2B: lesson 4.7, the whole image network`.

### Task 15: Lesson 4.8 — Memory networks

**Files:**
- Create: `src/AIAnatomy.Presentation/Labs/MemoryNetworkLabViewModel.cs`, `src/AIAnatomy.App/Views/MemoryNetworkView.xaml(.cs)`
- Modify: as in Task 13; `content/lessons/family/08-memory-networks.md`; tests (remove `memory-networks`).

**Interfaces:**
- Consumes: `FamilyModels.MemoryAsync()`, `IActiveModelService` (the shipped Fables model, loaded by name and not the active model, so the comparison is always against the shipped one), `ContextLoss`, `RnnSampling`, `Sampling.Generate` (the transformer), `RecordedResults`, `RetrainRunner` with `RnnTrainer`, `HeatMap` and `LineChart`.
- Produces `MemoryNetworkLabViewModel` (Id `"MemoryNetworkLab"`, part `memory`):
  - **The comparison table**, each value labelled with its kind:
    - Validation loss: the transformer's recorded value, 1.711; the RNN's recorded value; and the RNN's live value after "train it again".
    - Training speed in characters per second: the transformer's recorded value, the RNN's recorded value, and the RNN's live value.
  - **How far back it remembers:** a `LossByPosition` chart with two lines, measured live in the background when the lab opens. `WorkCompleted` covers it.
  - **The hidden state:** for a text box (default "The Fox"), a heat map of the RNN's hidden vector after each character (rows are characters, columns are units), with numbers on hover.
  - **Samples:** 200 characters from each model, from the same start text, at temperature 0.8, with the seed.
  - **Train it again** (the RNN only, with the `Retrain` config).
  - **Messages:** an empty text gives "Type some text first."; characters the model never saw appear as � (as in Stage 1).
  - **Screens:** `compare`, `memory`, `hidden`, `samples`, `training`.
  - **Render view:** `memory-network-measured` (after the live measurement).

- [ ] **Step 1: Write the failing tests:**
  - The recorded transformer numbers come from `RecordedResults`.
  - The live loss-by-position lines equal `ContextLoss` on the shared validation windows.
  - The hidden-state rows equal `ElmanRnn.Forward`'s hidden vectors.
  - The samples are deterministic for a seed.
  - **Train it again** (with the test hook): live RNN values appear, recorded values stay, and Stop, Reset and Deactivate behave as in Task 14.
  - The messages above.
  - **CommandLine** test.
- [ ] **Step 2: Run** the filter. FAIL.
- [ ] **Step 3: Implement**, and write the lesson:
  - Sources: `elman1990`, `hochreiter1997` (LSTM, mentioned only), `bengio1994` (why gradients fade over long spans, if confirmed), `vaswani2017` and `textbook`.
  - The text explains why attention largely replaced recurrence for language: long-range memory, and training on all positions at once on parallel hardware. It notes that this app's engine runs on the CPU, so its speed numbers don't show that parallel advantage, and says so.
  - It doesn't overstate: recurrent networks are still used in places.
  - A `## Formula` for the Elman update.
  - A checked question from the live memory measurement, `checked-by-code: Memory_TheTransformerGainsMoreFromLongContext`. The implementer pins the exact claim to what the measurement shows and writes it so that it stays true.
  - Connections: `memory-networks` → `attention`, and → `residual-stream`, only if accurate.
- [ ] **Step 4: Run** as in Task 13: renders `memory-networks,memory-network-measured`, the self-test and the full suite. PASS. **Commit** `Stage 2B: lesson 4.8, memory networks`.

### Task 16: Lesson 4.9 — Diffusion: from noise to a shape

**Files:**
- Create: `src/AIAnatomy.Presentation/Labs/DiffusionShapesLabViewModel.cs`, `src/AIAnatomy.App/Views/DiffusionShapesView.xaml(.cs)`
- Modify: as in Task 13; `content/lessons/family/09-diffusion-shapes.md`; tests (remove `diffusion-shapes`).

**Interfaces:**
- Consumes: `FamilyModels.DiffusionAsync`, `DiffusionSampler`, `Shapes`, `DrawingPad`, `PointCloudPlot`, and `RetrainRunner` with `DiffusionTrainer`.
- Produces `DiffusionShapesLabViewModel` (Id `"DiffusionShapesLab"`, part `diffusion`):
  - **Shapes:** a preset picker (the four shipped presets, each opening trained), and "Draw your own".
    - A drawing that `Shapes.FromStroke` refuses shows its message and keeps the previous shape.
    - An accepted drawing needs "Train it again" before it can be sampled. The lab says so and shows the target shape only.
  - **The cloud:** `Frames` (`Sample` with 500 points, the seed) and `Step` (a slider from 0 to Steps). Play advances one step about every 50 ms until the end, and Pause stops it.
  - **The quality line:** "Average distance from the shape: 0.031 (lower is better)."
  - **Train it again** on the current shape (with the `Retrain` steps).
  - **Screens:** `cloud`, `training`.
  - **Render view:** `diffusion-shapes-done` (the spiral at its last step).
  - With Windows animations off, Play still steps, because it is the learner's own action. Nothing animates by itself.

- [ ] **Step 1: Write the failing tests:**
  - Each preset loads and samples.
  - Each frame equals `DiffusionSampler.Sample` for the seed.
  - Both drawing errors keep the previous shape.
  - A drawn shape gives no frames until it is trained.
  - **Train it again** (with the hook) gives frames.
  - Stop, Reset, Deactivate and a seed change behave as in Task 14.
  - Play reaches the last step and stops, driven by a test clock with no wall-clock wait.
  - **CommandLine** test.
- [ ] **Step 2: Run** the filter. FAIL.
- [ ] **Step 3: Implement**, and write the lesson:
  - Sources `sohldickstein2015`, `ho2020`, `rombach2022` and `textbook`.
  - The text covers learning to undo noise, and that image generators use the same method on pixels or on compressed versions of them. It doesn't over-claim about any product.
  - A checked question on a shipped preset's recorded distance, `checked-by-code: DiffusionShapes_ShippedSpiralIsCloseToItsShape`.
  - Connections: `diffusion-shapes` → `diffusion-steps`, and → `learning`.
- [ ] **Step 4: Run** as in Task 13: renders `diffusion-shapes,diffusion-shapes-done`, the self-test and the full suite. PASS. **Commit** `Stage 2B: lesson 4.9, diffusion from noise to a shape`.

### Task 17: Lesson 4.10 — Diffusion: the steps

**Files:**
- Create: `src/AIAnatomy.Presentation/Labs/DiffusionStepsLabViewModel.cs`, `src/AIAnatomy.App/Views/DiffusionStepsView.xaml(.cs)`
- Modify: as in Task 13; `content/lessons/family/10-diffusion-steps.md`; tests (remove `diffusion-steps`, which leaves `beyond-coming` as the only placeholder).

**Interfaces:**
- Consumes: the shipped spiral model, `NoiseSchedule`, `DiffusionSampler`, `PointCloudPlot` and `LineChart`.
- Produces `DiffusionStepsLabViewModel` (Id `"DiffusionStepsLab"`, part `diffusion`):
  - **Forward:** a step slider t from 0 to 100 shows `AddNoise` at t (seeded). A schedule chart shows β_t and ᾱ_t, with the current t marked.
  - **Prediction:** at the chosen t, 20 highlighted points show arrows for the true noise and the predicted noise, in two line styles. The text gives the mean squared error between them.
  - **Reverse:** a steps picker (100, 50, 20, 10, 5) runs respaced sampling and shows the final cloud with its average distance, so the quality cost of fewer steps is visible. A frame slider steps through that run.
  - **Screens:** `forward`, `prediction`, `reverse`.
  - **Render view:** `diffusion-steps-five` (5 steps).

- [ ] **Step 1: Write the failing tests:**
  - The forward points equal `AddNoise`.
  - The arrows equal the true noise and the network's prediction.
  - The 100-step result equals the shipped sampler.
  - The 5-step average distance is larger than the 100-step one, for the shipped spiral and seed 1. If not, report it rather than change the claim, and look into it.
  - The screens rules.
  - **CommandLine** test.
- [ ] **Step 2: Run** the filter. FAIL.
- [ ] **Step 3: Implement**, and write the lesson:
  - Sources `ho2020`, `nichol2021` (respacing, if confirmed) and `textbook`.
  - A `## Formula` for the forward noising, x_t = √ᾱ_t·x_0 + √(1−ᾱ_t)·ε, and the reverse step.
  - A checked question on the fewer-steps result, `checked-by-code: DiffusionSteps_FewerStepsLandFurtherFromTheShape`.
  - Connections: `diffusion-steps` → `learning`, and → `output-sampling`, only if accurate.
- [ ] **Step 4: Run** as in Task 13: renders `diffusion-steps,diffusion-steps-five`, the self-test and the full suite. PASS. **Commit** `Stage 2B: lesson 4.10, diffusion steps`.

### Task 18: Final rendered check, records and the family tree

**Files:**
- Modify: `docs/decisions.md`, `README.md`, `docs/final-check.md`, `docs/research/scientific-inconsistencies.md` (if anything was found), `content/lessons/beyond/01-beyond-coming.md` (only if it still says section 4 is partly coming), and the issues sheet.

- [ ] **Step 1: Self-test.** Run `--selftest all-labs`, expecting exit code 0 with all ten section-4 lessons built.
- [ ] **Step 2: Renders, one launch per theme and size:**
  - The five Part B lessons and their render views, at Dark and Light, 10, 13 and 24 pt, with `--full-lab`.
  - Plus `ideamap-radial,ideamap-loop` at Dark 13 and Light 24.
  - That makes 8 launches, plus 1 re-check launch.
  - Look at every picture for clipping, overlap, unreadable text, contrast, colour as the only signal, and the Review Focus cases. Make small fixes; anything bigger goes in the issues sheet.
  - Check the family tree: no "later" left in section 4.
  - The Radial map's centre test: if it fails, use the Task 11 rule from Part A (measure, move only the failing bound, log it).
- [ ] **Step 3: Records:**
  - The decisions under `## Stage 2 Part B`: screens, anchors and render views of each lab, the rulings, and the citation checks.
  - README: section 4 complete.
  - `docs/final-check.md`: this check.
- [ ] **Step 4: Run** the full suite. PASS. **Commit** `Stage 2B: final rendered check and records`.
