# AIAnatomy UI Rework Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give AIAnatomy a visible order (numbered contents tree, lesson pages of one shape, Next/Back), an idea map
(Radial and Loop, laid out by MSAGL), Check yourself questions, output "screens", and the instrument-panel look at
13 pt, without changing what any lab computes.

**Architecture:** Content (lesson front matter, Try this, questions, `connections.json`, `idea-map.json`) is parsed and
validated in `AIAnatomy.Lessons`. View-models for the contents tree, lesson page, idea map, progress and Check
yourself live in `AIAnatomy.Presentation` (net10.0, no WPF), which also wraps MSAGL behind `IGraphLayout`.
`AIAnatomy.App` draws everything with theme resources and checks itself with `--render-png`.

**Tech Stack:** C# / .NET 10, WPF, CommunityToolkit.Mvvm 8.4.0, WpfMath 2.1.0, xUnit 2.9.3, new: AutomaticGraphLayout
1.1.12 (MIT).

**Spec:** `docs/superpowers/specs/2026-11-23-aianatomy-ui-rework-design.md`, which amends
`docs/superpowers/specs/2026-11-22-aianatomy-design.md`. Read both.

## Global Constraints

- No change to any model, the engine, or what a lab computes. Labs are restyled, not rewritten.
- Nothing may depend on sound. Every message is on screen.
- Text: default body **13 pt**; slider **10 to 24 pt**; titles **2 × body**; section headings **1.15 × body**; small
  text **0.85 × body, never under 9 pt**. One slider; the heading-size setting goes.
- Settings migration (once): a saved body size of **16 or more** with no scale version becomes **13**; anything else is
  kept. Default theme for a new install: **Dark**. A saved theme is kept.
- Only Dark and Light themes. No Paper theme, no font choice.
- Contrast: body text **≥ 4.5:1** against its background; part colours and lines **≥ 3:1**; both themes.
- Animations: light-up under **1 s**, never more than **3 per second**, off when Windows "Show animations" is off
  (`SystemParameters.ClientAreaAnimation == false`).
- A screen lights up after a direct user action that changes its value, and once when a run the user started ends;
  not on live updates during a run; not when the value is unchanged. Lab view-models say which screen changed.
- New dependency allowed: `AutomaticGraphLayout` 1.1.12, in `AIAnatomy.Presentation` only, for positions and line
  routes. No MSAGL WPF control. Licence added to `THIRD-PARTY-NOTICES.txt`.
- Check yourself questions may depend only on general ideas, the character tokenizer's rules, or the **shipped**
  Fables model and BPE tokenizer at default settings, named as such. Never on the active model, a user-trained model or
  a slider position. Each such question names a unit test that computes the real answer.
- Rules vs learning history: at most **8** entries, oldest dropped first; frozen when a run ends; start-text or seed
  change clears it and starts a new history from the model as it is now.
- Idea map: Radial is the default and the last choice is remembered; it scrolls rather than shrinks; at 13 pt its
  layout fits **1040 × 660** px (a 1280 × 800 window beside the tree).
- Visual checks only through `--render-png`; never screen capture. Keep launches of the built exe few.
- Every factual sentence (connections, questions, big ideas, Try this) is standard textbook material or backed by a
  source id from `content/sources.json`; nothing invented about Claude.
- Git: local only. Each task commits on the feature branch with a message ending
  `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`. Merging into `main` needs the owner's go-ahead.
- Decisions made during the build go in `docs/decisions.md`.

## Review Focus

1. **Placeholder lessons (Family, Beyond):** they have no idea, Try this or questions. Their page must hide empty
   blocks, Next/Back must skip them, and the tree shows them greyed. Test in Task 8.
2. **An old settings file** (body 20, heading 28, theme "Light", no scale version) must come out as 13 pt and Light,
   not Dark. A body of 14 with no version is kept. Test in Task 1.
3. **A corrupt or missing `progress.json`** must give empty progress, not a crash, and a failed save must not lose
   progress for the session. Test in Task 5.
4. **Reset, leaving the lab, or a start-text change while Learn is running** must never add or rewrite a history entry
   afterwards. Test in Task 12.
5. **A training run in the background while the idea map is showing** must keep the background status line and return
   to the Train lab intact. Test in Task 8.

## Before Task 1

The two display fixes (expander header colour in `Themes/Shared.xaml`, Explanation focus ring in `Shared.xaml` and
`MainWindow.xaml`) are uncommitted on `main`. With the owner's OK they are committed to `main` first, then the
feature branch is cut from that commit.

## Rulings made while planning

- **Content completeness is a content test, not a load-time check** (spec 6.1 lists it under "validation when
  loading"). The loader still rejects malformed files; a lesson that is merely missing its big idea or questions fails
  `LessonContentTests` instead of stopping the app. Why: a half-written lesson should never stop the app from opening.
  Cost if wrong: move one check into `LessonCatalog.Load`.
- **The section enum keeps its names** (`Section.Map` etc.); only the shown title changes, via `SectionInfo.Title`.
- **Next/Back skip the placeholder lessons**, so 3.3 Scale has no Next.
- **The explanation goes under the lab from 18 pt**, replacing the old 26 pt threshold on the new 10 to 24 pt scale.

---

## File structure

**Lessons** (`src/AIAnatomy.Lessons/`)
- `Lesson.cs`: gains init properties `Idea`, `Part`, `Branches`, `TryThis`, `Questions`.
- `LessonBranch.cs`, `Question.cs`, `QuestionParser.cs`: new.
- `Parts.cs`: the part keys. `SectionInfo.cs`: section numbers and titles, lesson numbers.
- `LessonCatalog.cs`: parses the new front matter and the `## Try this` and `## Check yourself` sections.
- `Connections.cs`: `Connection`, `ConnectionList` (loads and validates `content/connections.json`).
- `IdeaMapData.cs`: loads and validates `content/idea-map.json`.

**Presentation** (`src/AIAnatomy.Presentation/`)
- `Services/AppSettings.cs`, `Services/SettingsService.cs`: scale version, migration, default theme, map layout.
- `Services/IProgressService.cs`, `Services/ProgressService.cs`: new.
- `Contents/ContentsTreeViewModel.cs`, `Contents/ContentsItems.cs`: new.
- `Lesson/LessonPageViewModel.cs`, `Lesson/ConnectionChip.cs`, `Lesson/CheckYourselfViewModel.cs`: new.
- `IdeaMap/IGraphLayout.cs`, `IdeaMap/GraphTypes.cs`, `IdeaMap/MsaglGraphLayout.cs`, `IdeaMap/IdeaMapViewModel.cs`:
  new.
- `LabViewModel.cs`: screen reporting. `Labs/RulesVsLearningLabViewModel.cs`: history. Other lab view-models: screen
  reports only.
- `MainViewModel.cs`, `SettingsViewModel.cs`, `DiagnosticOptions.cs`: as described per task.

**App** (`src/AIAnatomy.App/`)
- `Themes/ThemeManager.cs`, `Themes/Dark.xaml`, `Themes/Light.xaml`, `Themes/Shared.xaml`.
- `Controls/Screen.cs`, `Controls/LessonAnchor.cs`: new.
- `Views/ContentsTreeView.xaml`, `Views/LessonPageView.xaml`, `Views/IdeaMapView.xaml`,
  `Views/CheckYourselfView.xaml`: new.
- `MainWindow.xaml(.cs)`, every `Views/*View.xaml` lab view, `Diagnostics/PngRenderer.cs`, `Diagnostics/SelfTests.cs`.

**Content:** `content/lessons/**/*.md`, `content/connections.json`, `content/idea-map.json`.

**Tests:** existing projects; new files named per task.

---

### Task 1: Text scale and settings migration

**Files:**
- Modify: `src/AIAnatomy.Presentation/Services/AppSettings.cs`, `src/AIAnatomy.Presentation/Services/SettingsService.cs`,
  `src/AIAnatomy.Presentation/Services/ISettingsService.cs`, `src/AIAnatomy.Presentation/SettingsViewModel.cs`,
  `src/AIAnatomy.Presentation/DiagnosticOptions.cs`, `src/AIAnatomy.Presentation/MainViewModel.cs`,
  `src/AIAnatomy.App/Themes/ThemeManager.cs`, every caller of `ThemeManager.Apply` and of `HeadingFontSize` in settings
- Test: `tests/AIAnatomy.Presentation.Tests/SettingsServiceTests.cs`, `SettingsViewModelTests.cs`, `CommandLineTests.cs`

**Interfaces:**
- Produces: `record AppSettings(double BodyFontSize = 13, string Theme = "Dark", string? ActiveModelName = null,
  string IdeaMapLayout = "Radial", int ScaleVersion = 0)`; `SettingsService.MinBodyFontSize = 10`,
  `MaxBodyFontSize = 24`, `CurrentScaleVersion = 2`; `ThemeManager.Apply(string theme, double bodyPt)`;
  `MainViewModel.StackedLayoutFromSize = 18`.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] public void Settings_MissingFile_GivesDefaults()
{ var s = _env.NewSettings().Current; Assert.Equal(13, s.BodyFontSize); Assert.Equal("Dark", s.Theme); Assert.Equal("Radial", s.IdeaMapLayout); }

[Theory]
[InlineData(20, 13)] [InlineData(16, 13)] [InlineData(32, 13)] [InlineData(14, 14)]
public void Settings_OldFile_MovesToNewScaleOnce(double savedBody, double expected)
{
    File.WriteAllText(_env.SettingsFile, $$"""{ "BodyFontSize": {{savedBody}}, "HeadingFontSize": 28, "Theme": "Light" }""");
    var s = _env.NewSettings().Current;
    Assert.Equal(expected, s.BodyFontSize); Assert.Equal("Light", s.Theme); Assert.Equal(2, s.ScaleVersion);
}

[Fact] public void Settings_NewScaleFile_KeepsSixteen()
{ File.WriteAllText(_env.SettingsFile, """{ "BodyFontSize": 16, "ScaleVersion": 2 }"""); Assert.Equal(16, _env.NewSettings().Current.BodyFontSize); }

[Theory] [InlineData(4, 10)] [InlineData(30, 24)] [InlineData(13, 13)]
public void Settings_ClampsFontSize(double asked, double body) { /* Save then read Current.BodyFontSize */ }

[Fact] public void Settings_UnknownTheme_BecomesDark() { }
[Fact] public void Settings_UnknownMapLayout_BecomesRadial() { }
```

Settings view-model: minimum 10, maximum 24. `DiagnosticOptions.Parse`: `--text-size 9` and `25` are errors; with no
`--text-size` the render uses 13.

- [ ] **Step 2: Run and see them fail**

Run: `dotnet test tests/AIAnatomy.Presentation.Tests --filter "FullyQualifiedName~Settings|FullyQualifiedName~CommandLine"`
Expected: FAIL (defaults are 20/Light; no migration).

- [ ] **Step 3: Implement**

- `Normalise`: if `ScaleVersion < 2` and body ≥ 16, body = 13; always write `ScaleVersion = 2`; clamp 10 to 24
  (non-finite → 13); unknown theme → "Dark"; `IdeaMapLayout` not "Loop" → "Radial". The JSON deserializer must see
  a missing `ScaleVersion` as 0 (it is, given the constructor default 0).
- `HeadingFontSize` is removed from `AppSettings`; an old file's value is ignored.
- `ThemeManager.Apply(theme, bodyPt)` sets `BodyFontSize`, `TitleFontSize` (2 ×), `HeadingFontSize` and
  `SubheadingFontSize` (both 1.15 ×), `SmallFontSize` (max(9, 0.85 ×)), `FormulaScale` (1.1 ×); keeps the existing
  size-derived resources; `ScrollBarSize = max(14, 24 × bodyPt / 20)`.
- `MainViewModel.StackedLayoutFromSize = 18`. `DiagnosticOptions`: text size 10 to 24, default 13 (update its doc
  comment).

- [ ] **Step 4: Run the whole suite**

Run: `dotnet test AIAnatomy.slnx`
Expected: PASS (fix existing tests that assumed 20/28/16–32/Light; each such fix is a changed expectation, not a
deleted test).

- [ ] **Step 5: Commit** — `git commit -m "Text scale: 13 pt default, 10-24 pt slider, one-time move of old sizes"`

---

### Task 2: Part palette and theme brushes

**Files:**
- Create: `src/AIAnatomy.Lessons/Parts.cs`
- Modify: `src/AIAnatomy.App/Themes/Dark.xaml`, `Light.xaml`, `Shared.xaml`
- Test: `tests/AIAnatomy.App.Tests/ThemeContrastTests.cs` (link `src/AIAnatomy.App/Themes/*.xaml` into the test
  output as content; parse them as XML, do not load WPF resources)

**Interfaces:**
- Produces: `Parts.All` = `intro, tokens, embeddings, position, attention, feedforward, residual, output, learning,
  whole, later` (`IReadOnlyList<string>`), `Parts.IsKnown(string)`; brush keys `Part.<key>` → `PartIntroBrush`,
  `PartTokensBrush`, `PartEmbeddingsBrush`, `PartPositionBrush`, `PartAttentionBrush`, `PartFeedforwardBrush`,
  `PartResidualBrush`, `PartOutputBrush`, `PartLearningBrush`, `PartWholeBrush`, `PartLaterBrush`; `ScreenBrush`
  (inset background), `ScreenTextBrush`, `ConnectionBrush` (the gold), `ScreenGlowOpacity` (`double`, 0 in Light);
  style keys `ScreenFrame`, `HeaderBand`, `TryThisBox`, `Chip`.

- [ ] **Step 1: Write the failing tests**

```csharp
[Theory] [InlineData("Dark")] [InlineData("Light")]
public void TextBrushes_MeetFourPointFive(string theme)
{   // TextBrush and MutedTextBrush against WindowBrush, SurfaceBrush and ScreenBrush; ScreenTextBrush against ScreenBrush
    foreach (var (fg, bg) in TextPairs) Assert.True(Contrast(theme, fg, bg) >= 4.5, $"{theme} {fg} on {bg}");
}

[Theory] [InlineData("Dark")] [InlineData("Light")]
public void PartAndConnectionBrushes_MeetThree(string theme)
{   // every Part*Brush and ConnectionBrush against SurfaceBrush and ScreenBrush
}

[Fact] public void EveryPartKeyHasABrushInBothThemes() { /* Parts.All → "Part" + PascalCase(key) + "Brush" */ }
```

`Contrast` is the WCAG 2.x ratio from relative luminance; the colours are read from the `Color` attributes of the
`SolidColorBrush` elements with those `x:Key`s.

- [ ] **Step 2: Run and see them fail.** Run: `dotnet test tests/AIAnatomy.App.Tests`. Expected: FAIL (keys missing).

- [ ] **Step 3: Add the palette.** Colour-blind-safe (Okabe-Ito based), tuned per theme to pass the tests;
  `PartLaterBrush` is a grey. Dark: `ScreenBrush` darker than `SurfaceBrush`; Light: a slightly tinted inset.
  `Parts.cs` holds the keys. Add the four styles to `Shared.xaml` (they read the brushes by `DynamicResource`).

- [ ] **Step 4: Run.** Expected: PASS.

- [ ] **Step 5: Commit** — `git commit -m "Part colours, screen brushes and contrast tests for both themes"`

---

### Task 3: Lesson content model

**Files:**
- Create: `src/AIAnatomy.Lessons/LessonBranch.cs`, `Question.cs`, `QuestionParser.cs`, `SectionInfo.cs`
- Modify: `src/AIAnatomy.Lessons/Lesson.cs`, `LessonCatalog.cs`
- Test: `tests/AIAnatomy.Lessons.Tests/LessonCatalogTests.cs`, `QuestionParserTests.cs`, `SectionInfoTests.cs`

**Interfaces:**
- Consumes: `Parts.IsKnown` (Task 2).
- Produces:
  - `record LessonBranch(string Label, string Anchor)`
  - `record Question(string Prompt, IReadOnlyList<string> Answers, int CorrectIndex, string Why, string See, string? CheckedBy)`
  - `Lesson` init properties: `string? Idea`, `string? Part`, `IReadOnlyList<LessonBranch> Branches` (empty default),
    `IReadOnlyList<string> TryThis` (empty), `IReadOnlyList<Question> Questions` (empty); `bool IsPlaceholder` (true
    when `LabId == "ComingLaterLab"`).
  - `SectionInfo.Number(Section) -> int` (Map 1 … Beyond 5), `SectionInfo.Title(Section)`: "The big picture",
    "The parts", "The whole model", "The family of AI", "Beyond the model"; `LessonCatalog.NumberOf(Lesson) -> string`
    ("2.4" = section number, position within section starting at 1); `LessonCatalog.InContentsOrder` (all lessons,
    section then order).
  - Fixed anchors usable by `see:`: `explanation`, `formula`, `brains`, `lab`, plus the lesson's branch anchors.

**Formats** (decided here, the spec left them to the builder):

Front matter:
```
idea: Each token gathers what it needs from the tokens before it.
part: attention
branches: By hand=toy; In the model=heads; Induction heads=induction
```

Body sections (each optional, any order after the explanation; they never count as explanation words):
```
## Try this
1. Drag Q towards key 2.
2. Watch weight 2 grow.

## Check yourself
? Which token can position 3 attend to?
- Only position 4
* Positions 1, 2 and 3
- Every position
why: The causal mask blocks every later position, so a token sees itself and what came before.
see: heads

? …
```
`*` marks the one correct answer; `checked-by-code: <TestMethodName>` is an optional line after `see:`.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] public void Parse_ReadsIdeaPartBranches() { /* branches "By hand=toy; In the model=heads" → two LessonBranch */ }
[Fact] public void Parse_TryThisAndCheckYourself_AreNotPartOfExplanation() { }
[Fact] public void Parse_UnknownPart_FailsNamingTheFile() { /* InvalidDataException message contains file name and "part" */ }
[Fact] public void Questions_TwoCorrectAnswers_Fail() { }
[Fact] public void Questions_NoCorrectAnswer_Fail() { }
[Fact] public void Questions_MissingWhyOrSee_Fail() { }
[Fact] public void Questions_SeeMustBeAKnownAnchor() { /* "see: nowhere" fails; "see: explanation" and a branch anchor pass */ }
[Fact] public void Questions_CheckedByIsOptional() { }
[Fact] public void Numbering_FollowsSectionAndOrder() { /* real catalog: tokens → "2.1", attention → "2.4", scale → "3.3", rules-vs-learning → "1.1" */ }
[Fact] public void Placeholders_AreMarked() { /* family-coming, beyond-coming */ }
```

- [ ] **Step 2: Run and see them fail.** Run: `dotnet test tests/AIAnatomy.Lessons.Tests`.

- [ ] **Step 3: Implement** the parser additions in `LessonCatalog.SplitBody` (new headings `## Try this`,
  `## Check yourself`) and `QuestionParser.Parse(IReadOnlyList<string> lines, IReadOnlySet<string> anchors)`.
  Errors are `InvalidDataException` with the question number; the catalog adds the file name as now.

- [ ] **Step 4: Run all Lessons and App tests.** Existing lesson files have none of the new fields and must still
  load. Expected: PASS.

- [ ] **Step 5: Commit** — `git commit -m "Lesson model: big idea, part, branches, Try this, Check yourself, numbering"`

---

### Task 4: Connections and idea-map data

**Files:**
- Create: `src/AIAnatomy.Lessons/Connections.cs`, `src/AIAnatomy.Lessons/IdeaMapData.cs`,
  `content/connections.json`, `content/idea-map.json`
- Test: `tests/AIAnatomy.Lessons.Tests/ConnectionsTests.cs`, `IdeaMapDataTests.cs`;
  `tests/AIAnatomy.Presentation.Tests/IdeaMapContentTests.cs`

**Interfaces:**
- Produces:
  - `record Connection(string From, string To, string Sentence, IReadOnlyList<string> SourceIds)`;
    `const string ConnectionList.WholeLoop = "LOOP"`; `ConnectionList.Load(string path)`;
    `ConnectionList.All`; `ConnectionList.Of(string nodeId) -> IReadOnlyList<Connection>` (either end);
    `ConnectionList.Validate(LessonCatalog, SourceList, IdeaMapData) -> IReadOnlyList<string>` (problems, empty = fine).
  - `record IdeaNode(string Id, string Label, string Description, string Kind)` with `Kind` one of `centre`, `flow`,
    `group`; `record FlowEdge(string From, string To, string Label)`; `IdeaMapData.Load(path)`, `.Nodes`,
    `.FlowEdges`, `.GroupMembers` (`IReadOnlyList<string>` of lesson ids inside the Block group),
    `.Validate(LessonCatalog) -> IReadOnlyList<string>`.

**Content:** node ids are lesson ids (`rules-vs-learning`, `architecture-map`, `tokens`, `embeddings`, `position`,
`attention`, `multi-head`, `feed-forward`, `residual-stream`, `output-sampling`, `learning`, `train`, `step-through`,
`scale`, `family-coming`, `beyond-coming`) or idea-map ids (`centre` "How a transformer works", `your-text`,
`next-character`, `block` group). Connections: the 20 from the approved mock-up (`.superpowers/brainstorm/7988-1795466249/content/loop-mindmap.html`, `IDEAS`), with its short ids mapped to these
ids and every sentence re-checked against the code and the lesson text. Flow edges: your-text→tokens "split",
tokens→embeddings "look up", position→embeddings "added", embeddings→residual-stream "start the stream",
residual-stream→output-sampling "after the last block", output-sampling→next-character "pick one",
next-character→your-text "added to the text". Group members: attention, multi-head, feed-forward.
Use `textbook` as the source id where no paper is needed.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] public void RealConnections_Validate() { Assert.Empty(connections.Validate(catalog, sources, map)); Assert.Empty(map.Validate(catalog)); }
[Fact] public void UnknownEnd_IsAProblem() { }
[Fact] public void UnknownSource_IsAProblem() { }
[Fact] public void EmptySentence_IsAProblem() { }
[Fact] public void LoopIsAllowedOnlyAsTo() { }
[Fact] public void DuplicatePair_IsAProblem() { /* same From/To twice, either direction */ }
// Presentation.Tests: the "your-text" description states the shipped Fables model's context length (read it from the shipped model file).
[Fact] public void YourText_Description_StatesShippedContextLength() { }
```

- [ ] **Step 2: Run and see them fail.** Run: `dotnet test tests/AIAnatomy.Lessons.Tests tests/AIAnatomy.Presentation.Tests`.
- [ ] **Step 3: Implement** the loaders (System.Text.Json, case-insensitive) and write the two JSON files.
- [ ] **Step 4: Run.** Expected: PASS.
- [ ] **Step 5: Commit** — `git commit -m "Connections and idea-map data with validation"`

---

### Task 5: Progress and the contents tree

**Files:**
- Create: `src/AIAnatomy.Presentation/Services/IProgressService.cs`, `Services/ProgressService.cs`,
  `src/AIAnatomy.Presentation/Contents/ContentsItems.cs`, `Contents/ContentsTreeViewModel.cs`
- Modify: `src/AIAnatomy.Presentation/Services/AppPaths.cs` (add `Progress` = `%LOCALAPPDATA%\AIAnatomy\progress.json`),
  `tests/AIAnatomy.Presentation.Tests/TestEnvironment.cs` (`ProgressFile`, `NewProgress()`)
- Test: `tests/AIAnatomy.Presentation.Tests/ProgressServiceTests.cs`, `ContentsTreeViewModelTests.cs`

**Interfaces:**
- Consumes: `LessonCatalog.NumberOf`, `SectionInfo`, `Lesson.IsPlaceholder`, `Lesson.Branches` (Task 3).
- Produces:
  - `enum LessonMark { NotOpened, Opened, Checked }`
  - `IProgressService { LessonMark MarkOf(string lessonId); void MarkOpened(string id); void MarkChecked(string id);
    void Reset(); event EventHandler? Changed; string? LastSaveProblem; }` — `MarkOpened` never lowers `Checked`.
  - `ProgressService(string filePath)`: JSON `{ "opened": [...], "checked": [...] }`, written via temp file like
    `SettingsService`.
  - `ContentsSection(Section Section, string Heading /* "2 The parts" */, bool IsLater, IReadOnlyList<ContentsLesson> Lessons)`
  - `ContentsLesson : ObservableObject` with `Lesson Lesson`, `string Number`, `string Label` ("2.4 Attention"),
    `IReadOnlyList<LessonBranch> Branches`, `LessonMark Mark`, `bool IsSelected`, `bool IsLater`.
  - `ContentsTreeViewModel(LessonCatalog, IProgressService)`: `Sections`, `Select(string lessonId)`, marks refresh
    on `IProgressService.Changed`.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] public void Progress_MissingFile_IsEmpty() { }
[Fact] public void Progress_CorruptFile_IsEmpty_AndNextSaveWorks() { }
[Fact] public void Progress_OpenedThenChecked_RoundTrips() { }
[Fact] public void Progress_OpenedAfterChecked_StaysChecked() { }
[Fact] public void Progress_SaveFails_KeepsMarksForTheSession() { /* make the folder a file; MarkOpened; MarkOf == Opened; LastSaveProblem != null */ }
[Fact] public void Progress_Reset_ClearsAll() { }
[Fact] public void Tree_HasFiveNumberedSections_FamilyAndBeyondLater() { /* "1 The big picture" … "5 Beyond the model" */ }
[Fact] public void Tree_LabelsUseNumbers() { /* "2.4 Attention" */ }
[Fact] public void Tree_MarkFollowsProgress() { }
```

- [ ] **Step 2–4:** run (fail), implement, run (pass): `dotnet test tests/AIAnatomy.Presentation.Tests`.
- [ ] **Step 5: Commit** — `git commit -m "Progress marks and the numbered contents tree"`

---

### Task 6: Graph layout and the idea-map view-model

**Files:**
- Modify: `src/AIAnatomy.Presentation/AIAnatomy.Presentation.csproj` (add `AutomaticGraphLayout` 1.1.12),
  `THIRD-PARTY-NOTICES.txt` (MSAGL, MIT, copyright line from the package)
- Create: `src/AIAnatomy.Presentation/IdeaMap/GraphTypes.cs`, `IGraphLayout.cs`, `MsaglGraphLayout.cs`,
  `IdeaMapViewModel.cs`
- Test: `tests/AIAnatomy.Presentation.Tests/IdeaMap/GraphLayoutTests.cs`, `IdeaMapViewModelTests.cs`

**Interfaces:**
- Consumes: `ConnectionList`, `IdeaMapData` (Task 4), `LessonCatalog`, `IProgressService`, `ISettingsService`.
- Produces:
  - `enum MapMode { Radial, Loop }`
  - `record LayoutNode(string Id, double Width, double Height)`, `record LayoutEdge(string From, string To)`,
    `record GraphInput(MapMode Mode, IReadOnlyList<LayoutNode> Nodes, IReadOnlyList<LayoutEdge> PlacementEdges,
    IReadOnlyList<LayoutEdge> RoutedOnlyEdges, string? CentreId, IReadOnlyList<string> GroupMembers)`
  - `record PlacedNode(string Id, double X, double Y, double Width, double Height)` (top-left, px),
    `record RoutedEdge(string From, string To, IReadOnlyList<(double X, double Y)> Points)`,
    `record GraphLayoutResult(IReadOnlyList<PlacedNode> Nodes, IReadOnlyList<RoutedEdge> Edges, double Width, double Height,
    (double X, double Y, double Width, double Height)? Group)`
  - `interface IGraphLayout { GraphLayoutResult Layout(GraphInput input); }`, `sealed class MsaglGraphLayout : IGraphLayout`
  - `IdeaMapViewModel(LessonCatalog, ConnectionList, IdeaMapData, IProgressService, ISettingsService, IActiveModelService, IGraphLayout)`:
    `string GroupLabel` ("Block × N", N = the active model's layer count, updated when the active model changes),
    `MapMode Mode` (from and saved to `AppSettings.IdeaMapLayout`), `IReadOnlyList<IdeaBubble> Bubbles`,
    `GraphLayoutResult? Layout`, `void Arrange(IReadOnlyDictionary<string, (double Width, double Height)> sizes)`,
    `string? ChosenId`, `void Choose(string id)`, `IReadOnlyList<Connection> ChosenConnections`,
    `IdeaCard? Card` (`record IdeaCard(string Title, string? Number, string? Description, IReadOnlyList<string> Sentences, string? LessonId)`),
    `event EventHandler<string>? OpenLessonRequested`, `IRelayCommand OpenChosenLessonCommand`.
  - `record IdeaBubble(string Id, string Label, string? Number, string? Part, bool IsLater, bool IsLesson, LessonMark Mark)`
    (bubbles are rebuilt when `IProgressService.Changed` fires; the layout is not).

**What goes into the layout:**
- Radial: nodes = centre, five section nodes (`section-1` … `section-5`, labels from `SectionInfo`), every lesson.
  Placement edges = centre→sections, section→its lessons. Routed-only edges = every connection whose both ends are
  on the map (`LOOP` → `centre`).
- Loop: nodes = flow nodes, every lesson. Placement edges = flow edges plus connections (so Learning sits near what it
  changes); the drawn structural lines are the flow edges; `LOOP` → the group's frame. Group = attention, multi-head,
  feed-forward.
- Algorithm: builder's choice, aiming for the mock-ups (start with MDS for Radial with the centre pinned, and Sugiyama
  top-to-bottom for Loop with the back edge routed round the side). Edges are sampled to polylines (≥ 16 points per
  curve segment). Idea edges are added to the router after placement so they avoid bubbles. Verify every MSAGL type
  and call against the 1.1.12 package.
- Layout runs on `Arrange` and on mode change, never on `Choose`.

- [ ] **Step 1: Write the failing tests** (bubble sizes in tests: width = 8.6 × chars × body/13 + 24, height = 36 × body/13)

```csharp
[Theory] [InlineData(MapMode.Radial, 10)] [InlineData(MapMode.Radial, 24)] [InlineData(MapMode.Loop, 10)] [InlineData(MapMode.Loop, 24)]
public void Layout_NoOverlaps_AllInsideBounds(MapMode mode, double bodyPt) { }

[Theory] [InlineData(MapMode.Radial)] [InlineData(MapMode.Loop)]
public void Layout_NoLineCrossesABubbleItDoesNotJoin(MapMode mode) { /* every RoutedEdge, every point pair vs every other PlacedNode rect deflated by 2 px */ }

[Theory] [InlineData(MapMode.Radial)] [InlineData(MapMode.Loop)]
public void Layout_At13pt_FitsTheMapArea(MapMode mode) { /* Width <= 1040 && Height <= 660 */ }

[Fact] public void Layout_IsDeterministic() { /* two runs give identical results */ }
[Fact] public void Map_DefaultsToRadial_AndRemembersLoop() { }
[Fact] public void Choose_ShowsOnlyThatBubblesConnections() { /* Choose("learning"): ChosenConnections equals ConnectionList.Of("learning"), and no other connection appears */ }
[Fact] public void Choose_DoesNotRelayout() { /* Layout reference unchanged */ }
[Fact] public void Card_ForNonLessonNode_HasDescriptionAndNoLesson() { }
[Fact] public void OpenChosenLesson_RaisesWithLessonId() { }
[Fact] public void LaterBubbles_AreMarkedLater() { }
```

- [ ] **Step 2–4:** run (fail), implement, run (pass).
- [ ] **Step 5: Commit** — `git commit -m "Idea map: MSAGL layout behind IGraphLayout, map view-model"`

---

### Task 7: Idea map view, renders, and the owner checkpoint

**Files:**
- Create: `src/AIAnatomy.App/Views/IdeaMapView.xaml(.cs)`
- Modify: `src/AIAnatomy.App/Diagnostics/PngRenderer.cs`, `src/AIAnatomy.Presentation/DiagnosticOptions.cs`
  (views `ideamap-radial`, `ideamap-loop`), `src/AIAnatomy.App/AppServices.cs` (gains `ConnectionList Connections`
  and `IdeaMapData IdeaMap`, loaded with the catalogue, and `IdeaMapViewModel NewIdeaMap(IProgressService)` using
  `MsaglGraphLayout`). Until Task 9 hosts the map in the window, the renderer shows it with
  `window.ShowDiagnosticPage(new IdeaMapView { DataContext = … })`, with the mode set from the view name.
- Test: `tests/AIAnatomy.Presentation.Tests/CommandLineTests.cs`

**Interfaces:**
- Consumes: `IdeaMapViewModel` (Task 6), brushes and styles (Task 2).

- [ ] **Step 1: Failing test** — `Parse(["--render-png", "ideamap-loop", "m.png"])` is valid; `"ideamap-sideways"` is not.
- [ ] **Step 2: Build the view.** A Radial | Loop switch (two toggle buttons); a `ScrollViewer` holding a `Canvas`
  sized to `Layout.Width/Height`; structural lines as muted `Path`s; bubbles as `Button`s (number, label, part
  colour edge, mark, greyed when later) in contents order for Tab; the chosen bubble's routed edges drawn in
  `ConnectionBrush` above the structural lines; the side card with sentences and **Open lesson**. The view measures
  every bubble at the current text size, calls `Arrange`, and calls it again when text size changes. Choosing a
  bubble by keyboard calls `BringIntoView` on it. In Loop mode the group frame is drawn with its label
  "Block × N" where N is the active model's layer count.
- [ ] **Step 3: Render** both modes, both themes, at 13 pt and 24 pt:
  `AIAnatomy.App.exe --render-png ideamap-radial,ideamap-loop <scratch>\map.png --theme Dark --text-size 13`
  (and Light, and 24). Look over every PNG: no overlaps, no clipped text, lines readable.
- [ ] **Step 4: Run** `dotnet test AIAnatomy.slnx`. Expected: PASS.
- [ ] **Step 5: Commit** — `git commit -m "Idea map view and its renders"`
- [ ] **Step 6: OWNER CHECKPOINT — stop here.** Show the owner the eight PNGs (Radial and Loop, Dark and Light,
  13 and 24 pt) beside the mock-ups. The owner decides: carry on, tune MSAGL further, or switch to hand-placed
  positions (a new `IGraphLayout` implementation only). Do not start Task 8 until they answer. Record the answer in
  `docs/decisions.md`.

---

### Task 8: Lesson page view-model and navigation

**Files:**
- Create: `src/AIAnatomy.Presentation/Lesson/LessonPageViewModel.cs`, `Lesson/ConnectionChip.cs`
- Modify: `src/AIAnatomy.Presentation/MainViewModel.cs`, `tests/AIAnatomy.Presentation.Tests/TestEnvironment.cs`
- Test: `tests/AIAnatomy.Presentation.Tests/LessonPageViewModelTests.cs`, `MainViewModelTests.cs`

**Interfaces:**
- Consumes: Tasks 3–6.
- Produces:
  - `record ConnectionChip(string OtherId, string Label, string? Part, string Sentence, bool IsWholeLoop)`
  - `LessonPageViewModel`: `Lesson Lesson`, `string Path` ("2 The parts › 2.4"), `string Number`, `string Title`,
    `string? Idea`, `string? Part`, `IReadOnlyList<string> TryThis`, `LabViewModel Lab`,
    `IReadOnlyList<ConnectionChip> Connections`, `Lesson? Previous`, `Lesson? Next` (contents order, placeholders
    skipped). (Task 10 adds `Check`.)
  - `SettingsViewModel(ISettingsService, IProgressService)` gains `IRelayCommand ResetProgressCommand` (the
    Settings page shows it as "Reset progress" in Task 9).
  - `MainViewModel` constructor gains `IProgressService progress, ConnectionList connections, IdeaMapData map,
    IGraphLayout layout` (`AppServices.NewMainViewModel` and `TestEnvironment.NewMain` pass them); adds `ContentsTreeViewModel Contents`, `IdeaMapViewModel IdeaMap`,
    `LessonPageViewModel? CurrentPage`, `bool IsShowingIdeaMap`, commands `ShowIdeaMap`, `ShowContents`,
    `OpenBranch(LessonBranch)`, `GoNext`, `GoBack`, `OpenConnection(ConnectionChip)`; `AnchorRequest? AnchorRequest`
    with `record AnchorRequest(string Anchor, int Serial)` (a new serial on every request, so the same anchor twice
    still scrolls). `IsShowingLesson` is false while the map, Sources or Settings show. Showing a lesson calls
    `MarkOpened`. A `LOOP` chip opens the idea map in Loop mode with nothing chosen. `IdeaMap.OpenLessonRequested`
    opens that lesson. `Sections` and the section-tab handling go; `ShowSection` (Ctrl+1 … Ctrl+5) opens the
    section's first lesson.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] public void Page_PathAndNumber() { /* attention: "2 The parts › 2.4", "2.4" */ }
[Fact] public void Next_FollowsContentsOrder_AcrossSections() { /* architecture-map.Next == tokens; learning.Next == train */ }
[Fact] public void Next_SkipsPlaceholders_LastBuiltLessonHasNoNext() { /* scale.Next == null; rules-vs-learning.Previous == null */ }
[Fact] public void PlaceholderPage_HasNoIdeaTryOrQuestions() { }
[Fact] public void Chips_ListEveryConnectionOfTheLesson_WithTheOtherEnd() { }
[Fact] public void ShowingALesson_MarksItOpened() { }
[Fact] public void ResetProgress_ClearsMarks() { }
[Fact] public void OpenBranch_OpensItsLessonAndRequestsTheAnchor() { /* twice in a row → two serials */ }
[Fact] public void IdeaMap_HidesTheLesson_AndOpenLessonComesBack() { }
[Fact] public void BackgroundTraining_StatusStaysWhileIdeaMapShows() { /* start Train lab run (TrainingCollection), ShowIdeaMap, BackgroundStatus != null; open train again, same lab instance */ }
```

- [ ] **Step 2–4:** run (fail), implement, run the whole Presentation suite (existing navigation tests updated where
  section tabs went).
- [ ] **Step 5: Commit** — `git commit -m "Lesson page view-model, Next/Back, branches, idea-map page"`

---

### Task 9: Shell and lesson page views

**Files:**
- Create: `src/AIAnatomy.App/Views/ContentsTreeView.xaml(.cs)`, `Views/LessonPageView.xaml(.cs)`,
  `src/AIAnatomy.App/Controls/LessonAnchor.cs`
- Modify: `src/AIAnatomy.App/MainWindow.xaml(.cs)`, `Themes/Shared.xaml`, `AppServices.cs`,
  `Diagnostics/PngRenderer.cs` (view `contents`: first lesson with every tree group expanded)

**Interfaces:**
- Consumes: Task 8.
- Produces: attached property `LessonAnchor.Name` (string) on any element in a lab view or the lesson page; the page
  answers `AnchorRequest` by finding the element with that name inside the page and calling `BringIntoView`. The
  fixed anchors `explanation`, `formula` (expands the formula expander), `brains`, `lab` are set on the page.

- [ ] **Step 1: Shell.** Slim top bar: app name, model picker, Reset, Sources, Settings. The Settings page gains a
  "Reset progress" button and its slider runs 10 to 24. Status and background lines
  as now. Left: the contents tree (`TreeView`, theme styles, mark glyphs ✓ ✔ ·, part colour bar, later groups
  greyed, branches as child items) with the **Contents | Idea map** switch above it. Right: the lesson page, the idea
  map, Sources or Settings.
- [ ] **Step 2: Lesson page,** one vertical `ScrollViewer`: path; header band (part colour, icon, title at
  `TitleFontSize`, big idea); Try this box (hidden when empty); lab and explanation side by side, explanation below
  the lab when `UseStackedLayout`; Check yourself placeholder slot (filled in Task 10); How this connects chips
  (tooltip and focus show the sentence; click opens); Back and Next buttons showing "← 2.3 Position" /
  "Next: 2.5 Multi-head →", hidden when null. Lab and explanation have no inner scrollers. Icons: builder's choice
  from Segoe Fluent Icons, one per part key.
- [ ] **Step 3: Keyboard.** Arrow keys in the tree; Enter opens; Tab moves from the tree to the page; Ctrl+1 … Ctrl+5
  still work.
- [ ] **Step 4: Render** `contents,tokens,attention,family-coming` in Dark and Light at 10, 13 and 24 pt; look over every
  PNG for clipping, overlap and unreadable text; fix and re-render.
- [ ] **Step 5: Run** `dotnet test AIAnatomy.slnx` and `AIAnatomy.App.exe --selftest all-labs`. Expected: PASS.
- [ ] **Step 6: Commit** — `git commit -m "New shell: contents tree, lesson page, idea-map switch"`

---

### Task 10: Check yourself

**Files:**
- Create: `src/AIAnatomy.Presentation/Lesson/CheckYourselfViewModel.cs`, `src/AIAnatomy.App/Views/CheckYourselfView.xaml(.cs)`
- Modify: `LessonPageViewModel.cs`, `LessonPageView.xaml`
- Test: `tests/AIAnatomy.Presentation.Tests/CheckYourselfViewModelTests.cs`

**Interfaces:**
- Produces: `LessonPageViewModel.Check` (`CheckYourselfViewModel?`, null when the lesson has no questions);
  `CheckYourselfViewModel(Lesson lesson, IProgressService progress, Action<string> requestAnchor)` with
  `IReadOnlyList<QuestionViewModel> Questions`, `bool IsSkipped`, `IRelayCommand SkipCommand`,
  `IRelayCommand ShowAgainCommand`; `QuestionViewModel`: `Prompt`, `Answers`, `int? ChosenIndex`, `bool? IsRight`,
  `string? Feedback` ("Right. " or "Not quite. " + Why), `IRelayCommand<int> ChooseCommand`, `IRelayCommand SeeCommand`.
  The lesson is marked `Checked` when every question has a chosen answer, or on Skip.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact] public void WrongAnswer_ShowsWhy_AndCanBeRetried() { }
[Fact] public void RightAnswer_ShowsWhy() { }
[Fact] public void AllAnswered_MarksChecked() { }
[Fact] public void OneAnswered_DoesNotMarkChecked() { }
[Fact] public void Skip_MarksChecked_AndHidesQuestions() { }
[Fact] public void See_RequestsTheAnchor() { }
[Fact] public void NoScoreIsKept() { /* reopening the lesson gives fresh, unanswered questions */ }
```

- [ ] **Step 2–4:** run (fail), implement the view-model and view (answers as buttons, feedback text under them,
  "See in the lesson" link, Skip and "Show the questions again"), run (pass).
- [ ] **Step 5: Commit** — `git commit -m "Check yourself questions with Skip"`

---

### Task 11: Screens and light-up

**Files:**
- Create: `src/AIAnatomy.App/Controls/Screen.cs`, `src/AIAnatomy.Presentation/ScreenChangedEventArgs.cs`
- Modify: `src/AIAnatomy.Presentation/LabViewModel.cs`, `Themes/Shared.xaml`
- Test: `tests/AIAnatomy.Presentation.Tests/ScreenReportingTests.cs`

**Interfaces:**
- Produces:
  - `record ScreenChangedEventArgs(string Screen)` (an `EventArgs`)
  - On `LabViewModel`: `event EventHandler<ScreenChangedEventArgs>? ScreenChanged`;
    `protected void ReportScreen(string screen, object? value)` — raises only if `value` differs (`Equals`) from the
    last value reported for that screen and no run is active; during a run it only remembers that the screen changed;
    `protected IDisposable BeginRun()` — on dispose raises once for each screen that changed during the run.
    Collections are reported as a string fingerprint.
  - `Screen : ContentControl` with `string ScreenName`; it finds the `LabViewModel` in its `DataContext` chain,
    listens for its own name, and brightens its edge (and glow in Dark) for under 1 s; pulses closer than 333 ms apart
    are dropped; nothing animates when `SystemParameters.ClientAreaAnimation` is false. It uses style `ScreenFrame`
    and the lesson's part brush (inherited attached property `Screen.PartBrush`, set by the lesson page).

- [ ] **Step 1: Write the failing tests** (a small test lab subclass)

```csharp
[Fact] public void SameValue_DoesNotRaise() { }
[Fact] public void ChangedValue_Raises() { }
[Fact] public void DuringRun_NothingRaises_ThenOncePerChangedScreenAtEnd() { }
[Fact] public void RunWithNoChange_RaisesNothing() { }
```

- [ ] **Step 2–4:** run (fail), implement, run (pass). Add a `Screen` to the control gallery and render
  `gallery` in both themes.
- [ ] **Step 5: Commit** — `git commit -m "Screens: output panels that light up when the user changes them"`

---

### Task 12: Rules versus learning, two screens with a history

**Files:**
- Modify: `src/AIAnatomy.Presentation/Labs/RulesVsLearningLabViewModel.cs`, `src/AIAnatomy.App/Views/RulesVsLearningView.xaml`
- Test: `tests/AIAnatomy.Presentation.Tests/Labs/RulesVsLearningLabTests.cs` (existing file; add to it)

**Interfaces:**
- Produces: `record LearnedEntry(int Steps, double Loss, string Text)`; `ObservableCollection<LearnedEntry> History`
  (newest first; `History[0]` is the newest); `const int MaxHistory = 8`; `string HistoryHeading`
  ("Written from “The ”"). Screens reported: `"rules"`, `"learned"`.

**Behaviour** (spec 5.4): entry zero is the untrained text; a Learn press adds a new top entry and updates it after
each chunk; at the end of the run it is frozen; a start-text or seed change clears the history and adds one entry
from the model as it is now; Reset gives only entry zero; oldest entries drop beyond 8.

- [ ] **Step 1: Write the failing tests** (use `ChunksPerRun = 2` for speed)

```csharp
[Fact] public void Start_HasOneUntrainedEntry() { /* History.Count == 1, Steps == 0 */ }
[Fact] public void Learn_AddsOneEntry_UpdatedToTheRunsEnd() { /* after one run: Count == 2, History[0].Steps == 40 */ }
[Fact] public void OlderEntries_AreNeverRewritten() { /* copy History[1] before a second run; equal after */ }
[Fact] public void History_KeepsAtMostEight() { /* nine runs → Count == 8, History[^1].Steps > 0 */ }
[Fact] public void StartTextChange_ClearsAndStartsFromCurrentModel() { /* Count == 1, Steps == StepsTrained, heading names the new text */ }
[Fact] public void ResetDuringRun_LeavesOnlyEntryZero() { }
[Fact] public void LeavingDuringRun_AddsNothingAfterwards() { /* Deactivate mid-run; await; Count unchanged */ }
[Fact] public void StartTextChangeDuringRun_NoOldTextEntryAppearsAfterwards() { }
[Fact] public void LearnedScreen_LightsOnceWhenTheRunEnds() { }
```

- [ ] **Step 2–4:** run (fail), implement, run (pass).
- [ ] **Step 5: View.** Two screens side by side: "Written rules" (output, rules listed below) and "Learned from
  examples" (history list, newest highlighted with the part colour and full brightness, older entries dimmed, each
  showing "after N steps · loss X"); the loss chart below, smaller. Render `rules-vs-learning` in Dark and Light at
  13 pt with `--full-lab` (it shows entry zero). A render can't press Learn, so `--selftest all-labs` also presses
  Learn once and checks `History.Count == 2`.
- [ ] **Step 6: Commit** — `git commit -m "Rules vs learning: written and learned side by side, with a history"`

---

### Tasks 13–15: Restyle the labs onto screens and anchors

Same steps for each task; three tasks so each review stays small.

- **Task 13:** Tokens, Embeddings, Position, Architecture map.
- **Task 14:** Attention (anchors `toy`, `heads`, `induction`), Multi-head, Feed-forward, Residual stream.
- **Task 15:** Output and sampling, Learning (anchors `neuron`, `surface`, `backprop`, `crossentropy`), Train,
  Step through, Scale.

**Files:** the lab's `Views/*View.xaml` and its view-model in `src/AIAnatomy.Presentation/Labs/`; tests in
`tests/AIAnatomy.Presentation.Tests/Labs/`; branches in the lesson's front matter.

- [ ] **Step 1: Failing tests,** one per lab: the main output screen raises `ScreenChanged` after the lab's direct
  input changes (typed text, slider, drag) and does not when the same value is set again. For Train and the
  induction experiment: nothing during the run, once at its end.
- [ ] **Step 2: Implement:** wrap every model output (generated text, probabilities, predictions, heat maps, loss,
  token chips, vectors) in a `Screen` with a name; controls stay outside screens; call `ReportScreen` at the points
  where those outputs change; wrap runs in `BeginRun()`. Set `LessonAnchor.Name` on each branch's panel and add the
  `branches:` line to the lesson file.
- [ ] **Step 3: Selftest:** `--selftest all-labs` also checks that every branch anchor of every lesson exists in its
  lab view.
- [ ] **Step 4: Render** each lab in Dark and Light at 13 pt with `--full-lab`, and at 24 pt; look over every PNG.
- [ ] **Step 5: Run** `dotnet test AIAnatomy.slnx` and the selftest. Expected: PASS.
- [ ] **Step 6: Commit** — `git commit -m "Restyle <labs>: outputs on screens, lesson anchors"`

---

### Tasks 16–17: Lesson content: big ideas, Try this, Check yourself

- **Task 16:** 1.1 Rules vs learning, 1.2 Architecture map, 2.1 Tokens, 2.2 Embeddings, 2.3 Position,
  2.4 Attention, 2.5 Multi-head.
- **Task 17:** 2.6 Feed-forward, 2.7 Residual stream, 2.8 Output and sampling, 2.9 Learning, 3.1 Train,
  3.2 Step through, 3.3 Scale.

**Files:** `content/lessons/**/*.md`; tests in `tests/AIAnatomy.Lessons.Tests/LessonContentTests.cs` and
`tests/AIAnatomy.Presentation.Tests/CheckedQuestionTests.cs` (create in Task 16).

- [ ] **Step 1: Failing content tests** (Task 16 adds them; they name the lessons still missing content, so Task 16
  makes them pass for its lessons by listing the Task 17 lessons in a temporary `NotYetWritten` set, which Task 17
  empties and deletes):

```csharp
[Fact] public void EveryBuiltLesson_HasIdeaPartTryThisAndTwoToThreeQuestions() { }
[Fact] public void BigIdea_IsOneSentence_UnderTwentyFiveWords() { }
[Fact] public void TryThis_HasTwoToFourSteps() { }
// Presentation.Tests:
[Fact] public void EveryCheckedByCode_NamesATestInThisClass() { /* reflection over CheckedQuestionTests */ }
[Fact] public void Tokens_<what it checks>() { /* computes the answer the way the lab does, through the lab's view-model where possible, with the shipped model and default settings; asserts the answer marked correct in the file */ }
```

- [ ] **Step 2: Write the content.** For each lesson: `idea:`, `part:`, two to four Try this steps that match the
  lab's real controls and labels, and two or three questions with `why:` and `see:`. Follow the Global Constraints
  rule on what a question may depend on; every question that depends on the app's models, tokenizers or text gets
  `checked-by-code:` and its test. Every claim is textbook material or backed by a source id.
- [ ] **Step 3: Run** `dotnet test AIAnatomy.slnx`. Expected: PASS.
- [ ] **Step 4: Render** each lesson page in Dark at 13 pt; read every Try this against its lab in the PNG.
- [ ] **Step 5: Commit** — `git commit -m "Lesson content: big ideas, Try this and Check yourself for <lessons>"`

---

### Task 18: Final rendered check and records

**Files:** `docs/decisions.md`, `README.md` (shell description and text sizes), `private/AIAnatomy-issues.xlsx`
(anything found and not fixed).

- [ ] **Step 1:** Render every lesson page, both idea maps, Settings and Sources in Dark and Light at 10, 13 and 24 pt
  (`--render-png <all ids>,ideamap-radial,ideamap-loop,settings,sources`). Look over every PNG for clipping, overlap,
  unreadable text and contrast; fix and re-render.
- [ ] **Step 2:** `dotnet test AIAnatomy.slnx` and `--selftest all-labs`. Expected: PASS, with the test count reported.
- [ ] **Step 3:** Update `docs/decisions.md` with the calls made in this plan and during the build (question format,
  anchors, part keys, stacked layout at 18 pt, Next skipping placeholders, the checkpoint outcome) and README.
- [ ] **Step 4: Commit** — `git commit -m "UI rework: final rendered check, decisions and README"`
