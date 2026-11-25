# AIAnatomy Stage 2, Part A (lessons 4.1–4.5) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Section 4, "The family of AI", gets its first five working lessons (the family tree, an expert system, minimax search, classic machine learning, Q-learning) and five short "coming later" pages for the Part B lessons.

**Architecture:** Each family member is a small, exact algorithm in a new `AIAnatomy.Models` namespace with hand-worked unit tests, wrapped by one `LabViewModel` per lesson in Presentation and one WPF view per lab in App, following the Stage 1 and UI-rework patterns (screens, `BeginRun`, DataTemplates in `MainWindow.xaml`, `--render-png`, `--selftest all-labs`). The family tree reuses the idea map's MSAGL machinery through a new `MapMode.Tree` layout. No neural networks and no shipped models in this part.

**Tech Stack:** C# / WPF on .NET 10, CommunityToolkit.Mvvm, MSAGL (already referenced by Presentation), WpfMath, xUnit 2.9.3.

**Spec:** `docs/superpowers/specs/2026-11-24-aianatomy-stage-2-design.md` (read sections 1–5.5, 6, 7, 8 and 9). Also binding: the app design `docs/superpowers/specs/2026-11-22-aianatomy-design.md` (section 7.2, lesson accuracy) and the rulings in `docs/decisions.md` (screens rules, Check yourself format, anchors, part keys).

## Global Constraints

- Branch: all work on local branch `stage2-part-a`, cut from `main` at 49a61d5. The controller commits each task on this branch without asking (owner, 2026-11-24). Never merge into main, never push: those wait for the owner.
- First commit on the branch (before Task 1): the Stage 2 design and this plan, message `Stage 2 design and Part A plan`.
- Commit messages start `Stage 2A: ` and end with the line `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- C#/WPF, .NET 10, MVVM with CommunityToolkit.Mvvm; WpfMath; MSAGL. **No new third-party runtime libraries.**
- Layering stays App → Presentation → Lessons → Models → Core. Models code never references Presentation (so no `NumberText` in Models).
- Every lab responds within about a second; anything longer runs in the background with Stop and a UI that never freezes.
- Seeded randomness everywhere, with the seed shown and editable (labs that use randomness: Reward).
- 13 pt default text, 10–24 pt range, Dark and Light, full keyboard use, **colour never the only carrier of meaning**.
- **No sound.** Visual checks only by `--render-png` and `--selftest`; few launches of the built exe. Never take screenshots with PowerShell `Add-Type`/`CopyFromScreen`.
- Lesson text: plain explanations of 150–350 words; standard textbook material or a confirmed source; every cited source named in the lesson's own text; no over-claiming either way. Claude may appear only as one example among many.
- Built lessons have: `idea:` (one sentence, under 25 words, ends with a full stop), `part:`, `## Try this` (2–4 numbered steps), `## Check yourself` (2–3 questions, correct answer's place varied by hand), connections in `content/connections.json`.
- A question that depends on a lab's defaults names them ("the default rules", "the lab's default maze") and has `checked-by-code:` naming a test in `tests/AIAnatomy.Presentation.Tests/CheckedQuestionTests.cs`.
- Each new source is confirmed from the publisher or a library catalogue before it is cited (authors, year, title, venue), as in Stage 1, and the check is logged in `docs/decisions.md` under "Citation checks (Stage 2 Part A)".
- Screens rules (`docs/decisions.md`): every screen is reported once as a baseline at construction; it lights on a direct user action that changes it and once at the end of a run (net change); Reset is a learner action.
- Every new view gets a `DataTemplate` in `MainWindow.xaml`; `--selftest all-labs` must pass with every lab's view screen names equal to its `ReportedScreens`.
- Decisions go in `docs/decisions.md` under a new heading `## Stage 2 Part A`; issues found and not fixed go in `private/AIAnatomy-issues.xlsx` (Severity and Priority columns); model/research mismatches in `docs/research/scientific-inconsistencies.md`.
- Full suite: `dotnet test AIAnatomy.slnx > <scratchpad>/test-<task>.log 2>&1`, then read the log (it takes 4–6 minutes; a failure's name must be in the saved log). Build the App project explicitly (`dotnet build src/AIAnatomy.App`) before any render.

## Review Focus

1. **Expert system rules a person types badly:** an empty line, a line with no `then`, 200 characters, a rule that concludes its own condition, two rules that conclude each other. Expected: a plain error under that rule, the rule left out, chaining always ends. Tests in Task 6.
2. **A maze the agent cannot solve:** the goal walled off, the start or goal placed on a wall, start and goal on one square. Expected: the edit is refused or a message says the goal can't be reached; Run stops at its episode cap and never hangs. Tests in Tasks 12 and 13.
3. **Odd point sets in classic machine learning:** no points, one point, every point one class, two points of different classes at the same place, k larger than the number of points, 200 points (the cap). Expected: no exception, sensible shading or a message. Tests in Tasks 10 and 11.
4. **Clicking during the computer's turn in tic-tac-toe:** a cell pressed while the computer is searching, after the game is over, or New game pressed mid-search. Expected: ignored, or the old search's result is thrown away. Tests in Task 9.
5. **Large text and narrow panes:** the family tree at 24 pt wider than the lab pane, and the Radial idea map with ten section-4 bubbles. Expected: the tree scrolls sideways with nothing clipped or overlapping; the map stays readable. Tests in Tasks 3 and 5; looked at in Task 14's renders.

---

### Task 1: Part keys, colours and icons for the family

**Files:**
- Modify: `src/AIAnatomy.Lessons/Parts.cs`
- Modify: `src/AIAnatomy.App/Themes/Dark.xaml`, `src/AIAnatomy.App/Themes/Light.xaml` (part palette block, lines ~31–45)
- Modify: `src/AIAnatomy.App/Controls/PartResource.cs` (the `Glyphs` table)
- Test: `tests/AIAnatomy.App.Tests/ThemeContrastTests.cs`

**Interfaces:**
- Produces: `Parts.All` (16 keys: the existing 11 in their order, then `family`, `rules`, `search`, `classic`, `reward`); `Parts.Transformer` (the existing 11); `Parts.Family` (`family`, `rules`, `search`, `classic`, `reward`; Part B appends `vision`, `memory`, `diffusion`). Brushes `PartFamilyBrush`, `PartRulesBrush`, `PartSearchBrush`, `PartClassicBrush`, `PartRewardBrush` in both themes.

- [ ] **Step 1: Write the failing tests.** In `ThemeContrastTests`:
  - `PartsIsKnown_AcceptsOnlyListedKeys`: `Assert.Equal(16, Parts.All.Count)`; `Parts.All` equals `Parts.Transformer.Concat(Parts.Family)` in order.
  - Replace the all-pairs loop of `PartBrushes_AreDistinguishable_ForColourBlindViewers` with one run per group: `Parts.Transformer`, and `Parts.Family` plus `"later"` (placeholders in section 4 wear `later`). Same thresholds (15 normal, 8 protanopia, 8 deuteranopia). Across groups there is no distance rule (spec 5: parts may share a hue family across sections; icon and label tell them apart).
  - `HeaderBandTint_…`, `PartAndConnectionBrushes_MeetThree` and `EveryPartKeyHasABrushInBothThemes` already loop over `Parts.All` and need no change.
  - New `EveryPartKeyHasItsOwnGlyph` (in App.Tests, reading `PartResource.Glyph`): every key in `Parts.All` gets a glyph that is not the neutral `""`, and the 16 glyphs are distinct.
- [ ] **Step 2: Run** `dotnet test tests/AIAnatomy.App.Tests` — expect failures on the count, missing brushes and glyphs.
- [ ] **Step 3: Implement.** Add the keys and the two lists to `Parts`. Add the five brushes to both theme files inside the palette block (keep the comment accurate: say the distance rule is per group). Choose the colours (builder's call) so the tests pass. Add five glyphs from Segoe Fluent Icons not already used, with a comment naming each icon as the existing entries do.
- [ ] **Step 4: Run** `dotnet test tests/AIAnatomy.App.Tests` — PASS.
- [ ] **Step 5: Log** in `docs/decisions.md` (new heading `## Stage 2 Part A`): the five keys, their colours in both themes, the icon names and code points, and the per-group distance rule with its reason.
- [ ] **Step 6: Commit** `Stage 2A: part keys, colours and icons for the family`.

### Task 2: Section 4 skeleton — ten lessons, map boxes, placeholders

**Files:**
- Delete: `content/lessons/family/01-family-coming.md`
- Create: `content/lessons/family/01-family-tree.md` … `10-diffusion-steps.md` (table below)
- Modify: `content/idea-map.json`, `content/lessons/beyond/01-beyond-coming.md`, `README.md` (line 7 and any other mention of section 4 being a placeholder)
- Modify: `src/AIAnatomy.Presentation/IdeaMap/IdeaMapViewModel.cs` (`BuildBubbles`, Loop branch)
- Test: `tests/AIAnatomy.Lessons.Tests/LessonContentTests.cs`, `LessonCatalogTests.cs`; `tests/AIAnatomy.Presentation.Tests/IdeaMap/IdeaMapViewModelTests.cs`, `CheckYourselfViewModelTests.cs`, `LessonPageViewModelTests.cs` (replace every use of `family-coming` as "a placeholder" with `beyond-coming`, which stays a placeholder through Stage 2)

**Interfaces:**
- Produces: lesson ids, files and titles used by every later task. In this task every one of them has `lab: ComingLaterLab` and no `part:`; Tasks 5, 7, 9, 11, 13 each turn one into a built lesson.

| File | id | order | title | idea-map label |
|---|---|---|---|---|
| 01-family-tree.md | `family-tree` | 1 | The family tree | Family tree |
| 02-expert-system.md | `expert-system` | 2 | Rules: an expert system | Expert system |
| 03-search.md | `search` | 3 | Search: looking ahead | Search |
| 04-classic-ml.md | `classic-ml` | 4 | Classic machine learning | Classic ML |
| 05-learning-by-reward.md | `learning-by-reward` | 5 | Learning by reward | Learning by reward |
| 06-image-filters.md | `image-filters` | 6 | Image networks: filters | Image filters |
| 07-image-network.md | `image-network` | 7 | Image networks: the whole network | Image network |
| 08-memory-networks.md | `memory-networks` | 8 | Memory networks | Memory networks |
| 09-diffusion-shapes.md | `diffusion-shapes` | 9 | Diffusion: from noise to a shape | Diffusion: shapes |
| 10-diffusion-steps.md | `diffusion-steps` | 10 | Diffusion: the steps | Diffusion: steps |

- [ ] **Step 1: Write the failing tests.**
  - `LessonContentTests`: a static set `StillComingLater = ["beyond-coming", "family-tree", "expert-system", "search", "classic-ml", "learning-by-reward", "image-filters", "image-network", "memory-networks", "diffusion-shapes", "diffusion-steps"]`; `EveryBuiltLesson_…` asserts the placeholders are exactly this set (each lab task later removes its id). Word limits: built lessons 150–350 as now; placeholders 40–350 (`MinPlaceholderWords = 40`). `ComingLaterLessons_HaveNoFormulaOrNote` loops over all placeholders; only `beyond-coming` must keep its bullet list.
  - `IdeaMapViewModelTests.Loop_ShowsOnlyTheFamilyTreeFromSection4`: in Loop mode the bubbles include `family-tree` and no other section-4 lesson.
  - `IdeaMapViewModelTests.Radial_Section4HasItsTenLessonsInOrder`: the bubbles after `section-4` are the ten ids above in order, each `IsLater == true` (all are placeholders at this point).
  - `IdeaMapViewModelTests.SavedTreeMode_IsReadAsRadial`: settings `IdeaMapLayout = "Tree"` gives `MapMode.Radial` (guards Task 3's new enum value; today any enum name is accepted).
- [ ] **Step 2: Run** `dotnet test tests/AIAnatomy.Lessons.Tests` and `dotnet test tests/AIAnatomy.Presentation.Tests --filter "FullyQualifiedName~IdeaMap"` — expect failures.
- [ ] **Step 3: Write the ten files.** Front matter: `id`, `section: family`, `order`, `title`, `lab: ComingLaterLab`, `sources`. Body: one paragraph of 40–150 words saying what the lesson will show (spec 5.1–5.10), naming each cited source in the text. Sources: `image-filters` and `image-network` cite `lecun1998`; `diffusion-shapes` cites `ho2020, rombach2022`; `diffusion-steps` cites `ho2020`; `learning-by-reward` cites `suttonbarto2018`; the rest `textbook`. (Every source `family-coming` cited stays cited.)
- [ ] **Step 4: Map and code.** In `idea-map.json` remove the `family-coming` node and add one `flow` node per new id with the label above and a one-sentence description. In `IdeaMapViewModel` Loop branch, leave out section-4 lessons other than the section's first (`family-tree`); read the saved mode only when it is `"Radial"` or `"Loop"`. Add to `beyond-coming` one sentence: the stage will also end with a map of how the family of AI fits together in a modern assistant (spec 8). Update the README's description of sections 4 and 5.
- [ ] **Step 5: Run** the two commands from Step 2, then the full suite (log to file) — PASS. Fix any other test that named `family-coming`.
- [ ] **Step 6: Commit** `Stage 2A: section 4 skeleton (ten lessons, map boxes, placeholders)`.

### Task 3: Tree layout in the MSAGL graph layout

**Files:**
- Modify: `src/AIAnatomy.Presentation/IdeaMap/GraphTypes.cs` (enum), `src/AIAnatomy.Presentation/IdeaMap/MsaglGraphLayout.cs`
- Test: `tests/AIAnatomy.Presentation.Tests/IdeaMap/GraphLayoutTests.cs`

**Interfaces:**
- Produces: `MapMode.Tree`. For a `GraphInput` with `Mode = MapMode.Tree`, `PlacementEdges` are parent → child, `Nodes` are in file order (siblings left to right in that order), `CentreId` is the root, `RoutedOnlyEdges` and `GroupMembers` are empty. The result is a top-down tree in the usual `GraphLayoutResult` (y downwards, one `RoutedEdge` per placement edge).

- [ ] **Step 1: Write the failing tests** on a fixed 13-node input shaped like the spec 5.1 tree (root; three children; 2, 2 and 1 grandchildren; four great-grandchildren under the second grandchild of the middle branch), boxes 120×40, plus one input with boxes 300×90 (24 pt):
  - `Tree_ParentsSitAboveTheirChildren`: for every edge, parent's bottom ≤ child's top − 10.
  - `Tree_SiblingsKeepTheirOrderLeftToRight`: children of each parent have strictly increasing centre X in `Nodes` order.
  - `Tree_NoTwoBoxesOverlap` (both box sizes).
  - `Tree_EveryEdgeIsRoutedFromParentToChild`: one routed edge per placement edge; first point within 2 px of the parent box, last within 2 px of the child box.
  - `Tree_SameInputGivesTheSameLayout`.
- [ ] **Step 2: Run** `dotnet test tests/AIAnatomy.Presentation.Tests --filter "FullyQualifiedName~GraphLayoutTests"` — FAIL.
- [ ] **Step 3: Implement `PlaceTree(GraphInput)`** in `MsaglGraphLayout`, called from `Layout` for `MapMode.Tree`; lines go through the existing `Route`. Use MSAGL's `LayeredLayout` (top to bottom, `NodeSeparation` 20, `LayerSeparation` 40) with each parent's children passed to `SugiyamaLayoutSettings.AddSameLayerNeighbors` in `Nodes` order, as `PlaceLoop` does for its rows. If MSAGL cannot keep sibling order, place the boxes with a plain layered tree placement of our own (each leaf the next slot left to right, each parent centred over its children, layers 40 px apart) and still route with `Route`; log which was used and why in decisions.
- [ ] **Step 4: Run** the filter from Step 2 — PASS.
- [ ] **Step 5: Commit** `Stage 2A: tree layout for the family tree`.

### Task 4: The family tree's content file

**Files:**
- Create: `src/AIAnatomy.Lessons/FamilyTreeData.cs`, `content/family-tree.json`
- Test: `tests/AIAnatomy.Lessons.Tests/FamilyTreeDataTests.cs`

**Interfaces:**
- Produces:
  ```csharp
  public sealed record FamilyNode(string Id, string Label, string? Parent, string? LessonId,
      string WhatItIs, string? GoodAt, string? BadAt, string? Example);
  public sealed class FamilyTreeData
  {
      public IReadOnlyList<FamilyNode> Nodes { get; }          // file order; Nodes[0] is the root
      public FamilyNode Root { get; }
      public IReadOnlyList<FamilyNode> ChildrenOf(string id);  // file order
      public static FamilyTreeData Create(IEnumerable<FamilyNode> nodes);
      public static FamilyTreeData Load(string path);          // InvalidDataException naming the file
      public IReadOnlyList<string> Validate(LessonCatalog catalog); // empty means fine
  }
  ```
  JSON: `{ "nodes": [ { "id", "label", "parent", "lesson", "whatItIs", "goodAt", "badAt", "example" } ] }` (`lesson` maps to `LessonId`; `parent` null only for the root).

- [ ] **Step 1: Write the failing tests.**
  - `ShippedTree_IsValid`: `Load(content/family-tree.json).Validate(catalog)` is empty.
  - `ShippedTree_HasTheThreeTopBranches`: root's children labels are `Written by a person`, `Learned from examples`, `Learned from reward`, in that order.
  - `ShippedTree_LinksTheEightMembers`: the set of `LessonId`s is exactly `expert-system, search, classic-ml, learning-by-reward, image-filters, memory-networks, architecture-map, diffusion-shapes`; the `architecture-map` node is labelled `Transformer` and sits under `Neural networks` under `Learned from examples`.
  - One validation test per rule, each asserting a problem containing the quoted text: repeated id (`"repeated id"`), second root (`"exactly one root"`), root not first (`"the root must come first"`), unknown parent (`"unknown parent"`), a cycle (`"cycle"`), empty label (`"empty label"`), empty `whatItIs` (`"what it is"`), a node with a lesson missing good at / bad at / example (`"good at, bad at and an example"`), unknown lesson (`"not a lesson"`), a lesson used twice (`"used twice"`), root with other than three children (`"three branches"`).
  - `Load_BadJson_NamesTheFile`.
- [ ] **Step 2: Run** `dotnet test tests/AIAnatomy.Lessons.Tests --filter "FullyQualifiedName~FamilyTree"` — FAIL.
- [ ] **Step 3: Implement** `FamilyTreeData` on the pattern of `IdeaMapData` (System.Text.Json, case-insensitive). Write `content/family-tree.json` with the spec 5.1 tree: root `Programs that do tasks we call intelligent` (builder may improve labels, keeping the three branches); members Rules, Search, Classic machine learning, Neural networks (no lesson) with Image networks, Memory networks, Transformer, Diffusion beneath it, and Q-learning under reward. Card text held to spec 7: a real-world example is well known or from a source the lesson cites.
- [ ] **Step 4: Run** the filter — PASS. Make sure the content file is copied to the test output as the other content files are.
- [ ] **Step 5: Commit** `Stage 2A: family tree content file`.

### Task 5: Lesson 4.1 — the family tree lab

**Files:**
- Create: `src/AIAnatomy.Presentation/Labs/FamilyTreeLabViewModel.cs`, `src/AIAnatomy.App/Views/FamilyTreeView.xaml(.cs)`
- Modify: `src/AIAnatomy.Presentation/LabFactory.cs`, `src/AIAnatomy.Presentation/MainViewModel.cs` (passes its catalog to the factory), `src/AIAnatomy.App/MainWindow.xaml` (DataTemplate), `src/AIAnatomy.Presentation/DiagnosticOptions.cs`, `src/AIAnatomy.App/Diagnostics/PngRenderer.cs`, `content/lessons/family/01-family-tree.md`, `content/connections.json`
- Test: `tests/AIAnatomy.Presentation.Tests/Labs/FamilyTreeLabTests.cs`, `CheckedQuestionTests.cs`, `CommandLineTests.cs`, `LessonContentTests.cs` (remove `family-tree` from `StillComingLater`)

**Interfaces:**
- Consumes: `FamilyTreeData` (Task 4), `MapMode.Tree` and `IGraphLayout` (Task 3).
- Produces:
  ```csharp
  public sealed record FamilyBubble(string Id, string Label, string? Number, bool IsLater, bool HasLesson)
  { public string AutomationName { get; } }   // "4.2 Expert system, family member", "Neural networks, group", "4.6 Image networks, not built yet"
  public sealed record FamilyCard(string Title, string WhatItIs, string? GoodAt, string? BadAt, string? Example,
      string? LessonId, string? LessonTitle, bool LessonIsLater);
  public sealed partial class FamilyTreeLabViewModel : LabViewModel
  {
      public const string Id = "FamilyTreeLab";
      public FamilyTreeLabViewModel(FamilyTreeData tree, LessonCatalog catalog, IGraphLayout layout);
      public IReadOnlyList<FamilyBubble> Bubbles { get; }      // file order
      public GraphLayoutResult? Layout { get; }                // observable
      public string? ChosenId { get; }                         // observable
      public FamilyCard? Card { get; }                         // observable
      public void Arrange(IReadOnlyDictionary<string, (double Width, double Height)> sizes);
      public void Choose(string id);                           // ArgumentException for an unknown id
      public string? ChooseOrOpen(string id);                  // already chosen and has a lesson: returns its id to open
      public void ClearChoice();
  }
  ```
  `LabFactory(IActiveModelService models, string contentFolder, Action? libraryChanged = null, LessonCatalog? lessons = null)`: the family tree lab loads `family-tree.json` from `contentFolder` lazily and uses `lessons ?? LessonCatalog.Load(contentFolder)`. Render view constant `DiagnosticOptions.FamilyTreeChosen = "family-tree-chosen"` (Rules chosen; works with `--full-lab`).

- [ ] **Step 1: Write the failing tests** (`FamilyTreeLabTests`, with a counting `IGraphLayout` fake as in `IdeaMapViewModelTests`):
  - `Bubbles_FollowTheFileOrder_WithLessonNumbers`: `expert-system` bubble has Number `"4.2"`, the Transformer `"1.2"`, Neural networks null.
  - `Bubbles_ForPlaceholderLessons_AreLater`: the Image networks bubble `IsLater` (its lesson is a placeholder in Part A).
  - `Arrange_LaysOutOnce_InTreeMode_WithTheRootAsCentre`: the fake sees one `GraphInput` with `MapMode.Tree`, `CentreId` = root id, one placement edge per non-root node.
  - `Choose_ShowsTheCard`: choosing `rules` gives a card whose `LessonId` is `expert-system` and whose four texts equal the file's.
  - `Choose_UnknownId_Throws`.
  - `ChooseOrOpen_SecondPressOpensTheLesson`: first call returns null and chooses; second returns `expert-system`; for `neural` (no lesson) the second also returns null.
  - `Reset_ClearsTheChoice`; `CardScreen_LightsWhenABubbleIsChosen` (subscribe to `ScreenChanged`, choose: one raise for `card`).
  - `Arrange_At24pt_NoBoxesOverlap` with the real `MsaglGraphLayout` and 300×90 boxes.
  - `CommandLineTests.FamilyTreeChosen_IsARenderViewThatTakesFullLab`.
- [ ] **Step 2: Run** `dotnet test tests/AIAnatomy.Presentation.Tests --filter "FullyQualifiedName~FamilyTree|FullyQualifiedName~CommandLine"` — FAIL.
- [ ] **Step 3: Implement the view-model and factory registration.** Screen: `card` (the chosen card's title and texts, or empty), reported as a baseline in the constructor.
- [ ] **Step 4: Implement `FamilyTreeView`** on the pattern of `IdeaMapView`: one `Button` per bubble in file order (Tab follows it), measured at the current text size and passed to `Arrange` (again on a text-size change, never on a choice); lines from `Layout.Edges` on a canvas inside a horizontal `ScrollViewer`; a bubble whose lesson is a placeholder shows the word "later" (not colour alone); the chosen bubble gets a thicker outline and the card shows its four texts with an `Open lesson 4.2 Rules: an expert system` button that raises `LessonNavigation.NavigateRequestedEvent`; Enter on the chosen bubble opens (`ChooseOrOpen`), Space and the mouse only choose. The card sits inside `<c:Screen ScreenName="card">`. Add the DataTemplate and the render view.
- [ ] **Step 5: Write lesson 4.1** (`lab: FamilyTreeLab`, `part: family`, `sources: textbook` plus any source a card's example needs): explanation 150–350 words on the three ways a program gets its behaviour (spec 1); Try this; 2–3 questions, one `checked-by-code: FamilyTree_TransformerIsLearnedFromExamples`. Remove `family-tree` from `StillComingLater`. Add connection `family-tree` → `rules-vs-learning` (the first split is the same idea).
- [ ] **Step 6: Write the checked-question test** `FamilyTree_TransformerIsLearnedFromExamples`: walks up from the `architecture-map` node to the root's child and asserts its label equals `Correct(...)`.
- [ ] **Step 7: Run** the filter, then `dotnet build src/AIAnatomy.App` and render `family-tree,family-tree-chosen` in one launch (Dark, 13 pt, `--full-lab`); look at both PNGs.
- [ ] **Step 8: Run** the full suite (log to file) — PASS. **Commit** `Stage 2A: lesson 4.1, the family tree`.

### Task 6: Rules — the expert system model

**Files:**
- Create: `src/AIAnatomy.Models/Rules/Rule.cs`, `RuleText.cs`, `RuleSet.cs`, `ForwardChainer.cs`, `AnimalRules.cs`
- Test: `tests/AIAnatomy.Models.Tests/Rules/RuleTextTests.cs`, `ForwardChainerTests.cs`

**Interfaces:**
- Produces (namespace `AIAnatomy.Models.Rules`):
  ```csharp
  public sealed record Condition(string Fact, bool Negated);
  public sealed record Rule(IReadOnlyList<Condition> Conditions, string Conclusion) { public string ToText(); }
  public sealed record RuleParse(Rule? Rule, string? Error);
  public static class RuleText
  {
      public const int MaxFactLength = 40, MaxConditions = 6;
      public static RuleParse Parse(string? text);   // never throws
      public static string Normalize(string fact);   // trim, lower-case (invariant), inner whitespace collapsed to one space
  }
  public sealed record RuleProblem(int RuleNumber, string Message);   // 1-based
  public sealed class RuleSet
  {
      public RuleSet(IEnumerable<Rule> rules);
      public IReadOnlyList<Rule> Rules { get; }
      public IReadOnlyList<string> AskableFacts { get; }      // in conditions, never concluded; first-use order
      public IReadOnlyList<string> FinalConclusions { get; }  // concluded, never a condition; first-use order
      public IReadOnlyList<RuleProblem> Problems { get; }
  }
  public sealed record FiredRule(int RuleNumber, Rule Rule, int Pass);   // Pass is 1-based
  public sealed record ChainResult(IReadOnlyList<FiredRule> Fired, IReadOnlySet<string> Known, IReadOnlyList<string> Answers);
  public static class ForwardChainer { public static ChainResult Run(RuleSet rules, IReadOnlySet<string> yesFacts); }
  public static class AnimalRules
  {
      public static IReadOnlyList<string> DefaultRuleTexts { get; }   // the 15 lines below, in order
      public static IReadOnlyList<(string Animal, IReadOnlySet<string> Yes)> Descriptions { get; }   // the 8 below, in order
  }
  ```
- Grammar: `if <condition> [and <condition>]… then <fact>`; keywords `if`, `and`, `not`, `then` in any case; a condition is `[not ]<fact>`; a fact is 1–40 characters of letters, digits, spaces, hyphens and apostrophes, normalized. Error messages, exactly: `Start the rule with "if".` · `Put "then" before the conclusion.` · `A condition is empty.` · `The conclusion is empty.` · `"<fact>" is too long (40 characters at most).` · `Use only letters, numbers, spaces, hyphens and apostrophes.` · `A rule can have at most 6 conditions.`
- Rule-set problems, exactly: `"not" only works with a fact you answer, and a rule concludes "<fact>".` (a negated condition on a concluded fact) and `This rule concludes one of its own conditions.` Rules with a problem are left out of chaining.
- Chaining: `Known` starts as `yesFacts`; each pass goes through the rules in order and fires a rule (adding its conclusion to `Known` at once) when every condition holds — a plain condition when its fact is in `Known`, a negated one when its fact is not in `yesFacts` — and its conclusion is not yet known; passes repeat until one fires nothing. `Answers` = the `FinalConclusions` that are known, in `FinalConclusions` order.
- Default rules, in order (the spec's "about 15 rules", in the textbook animal-identification style):
  1. `if has hair then mammal` · 2. `if gives milk then mammal` · 3. `if has feathers then bird` · 4. `if flies and lays eggs then bird` · 5. `if mammal and eats meat then carnivore` · 6. `if mammal and has pointed teeth and has claws and has forward-facing eyes then carnivore` · 7. `if mammal and has hooves then ungulate` · 8. `if mammal and chews cud then ungulate` · 9. `if carnivore and tawny and dark spots then cheetah` · 10. `if carnivore and tawny and black stripes then tiger` · 11. `if ungulate and long neck and long legs and dark spots then giraffe` · 12. `if ungulate and black stripes then zebra` · 13. `if bird and not flies and long neck and long legs and black-and-white then ostrich` · 14. `if bird and not flies and swims and black-and-white then penguin` · 15. `if bird and good flier then albatross`
- Descriptions (yes facts), in order: cheetah {has hair, eats meat, tawny, dark spots}; tiger {has hair, eats meat, tawny, black stripes}; giraffe {has hair, has hooves, long neck, long legs, dark spots}; zebra {has hair, has hooves, black stripes}; ostrich {has feathers, long neck, long legs, black-and-white}; penguin {has feathers, swims, black-and-white}; albatross {has feathers, flies, lays eggs, good flier}; bat {has hair, gives milk, flies}. (The bat is the animal the rules never considered: spec 5.2's brittleness case.)

- [ ] **Step 1: Write the failing tests.**
  - `RuleTextTests`: parse of rule 13 gives 5 conditions with `flies` negated; `"IF  Has   Hair THEN Mammal"` parses to fact `has hair`, conclusion `mammal`; `ToText()` round-trips every default rule; one test per error message above (empty string and null give `Start the rule with "if".`; `if has hair mammal` gives the `then` message; `if has hair and then x` the empty-condition one; a 41-character fact; `if a! then b`; seven conditions).
  - `ForwardChainerTests` (hand-worked on the defaults):
    - `AskableFacts_AreTheNineteenInFirstUseOrder`: `has hair, gives milk, has feathers, flies, lays eggs, eats meat, has pointed teeth, has claws, has forward-facing eyes, has hooves, chews cud, tawny, dark spots, black stripes, long neck, long legs, black-and-white, swims, good flier`.
    - `FinalConclusions_AreTheSevenAnimals`: `cheetah, tiger, giraffe, zebra, ostrich, penguin, albatross`.
    - `EachDescription_FiresTheseRules` (theory): cheetah [1,5,9]; tiger [1,5,10]; giraffe [1,7,11]; zebra [1,7,12]; ostrich [3,13]; penguin [3,14]; albatross [3,15]; bat [1] — all in pass 1; answers are the animal itself, and none for the bat (`Known` contains `mammal`).
    - `RulesOutOfOrder_NeedASecondPass`: rules [`if mammal and eats meat then carnivore`, `if has hair then mammal`], yes {has hair, eats meat}: fired (2, pass 1), (1, pass 2).
    - `PenguinThatFlies_GetsNoAnswer` (penguin + flies).
    - `TigerWithSpots_GetsTwoAnswers`: tiger + dark spots → answers [cheetah, tiger].
    - `NotOnAConcludedFact_IsAProblem_AndTheRuleIsLeftOut`: defaults + `if not mammal then fish` → problem on rule 16 with the exact message; chaining never fires it.
    - `RulesThatConcludeEachOther_StillEnd` (review focus 1): [`if a then b`, `if b then a`] with yes {a} ends after 2 passes, known {a, b}; `if c and d then c` is a problem (`This rule concludes one of its own conditions.`).
- [ ] **Step 2: Run** `dotnet test tests/AIAnatomy.Models.Tests --filter "FullyQualifiedName~Rules"` — FAIL.
- [ ] **Step 3: Implement** the five files.
- [ ] **Step 4: Run** the filter — PASS.
- [ ] **Step 5: Commit** `Stage 2A: expert system model (rules, forward chaining)`.

### Task 7: Lesson 4.2 — the expert system lab

**Files:**
- Create: `src/AIAnatomy.Presentation/Labs/ExpertSystemLabViewModel.cs`, `src/AIAnatomy.App/Views/ExpertSystemView.xaml(.cs)`
- Modify: `LabFactory.cs`, `MainWindow.xaml`, `content/lessons/family/02-expert-system.md`, `content/connections.json`, `content/sources.json`
- Test: `tests/AIAnatomy.Presentation.Tests/Labs/ExpertSystemLabTests.cs`, `CheckedQuestionTests.cs`, `LessonContentTests.cs` (remove `expert-system`)

**Interfaces:**
- Consumes: everything in Task 6.
- Produces:
  ```csharp
  public sealed partial class RuleLine : ObservableObject   // Number (1-based), Text (editable), Error (string?), IsUsed (no parse error, no problem)
  public sealed partial class QuestionAnswer : ObservableObject   // Fact, Prompt ("Has hair?": first letter upper-case, "?" added), IsYes
  public sealed record TraceLine(int Pass, int RuleNumber, string Text);
      // "Rule 5: mammal and eats meat, so carnivore."  (conditions joined by " and ", "not " kept)
  public sealed partial class ExpertSystemLabViewModel : LabViewModel
  {
      public const string Id = "ExpertSystemLab";
      public const int MaxRules = 40;
      public ObservableCollection<RuleLine> Rules { get; }
      public ObservableCollection<QuestionAnswer> Questions { get; }   // one per askable fact, in AskableFacts order
      public IReadOnlyList<string> Animals { get; }                    // AnimalRules.Descriptions names
      public IReadOnlyList<TraceLine> Trace { get; }                   // observable
      public string AnswerText { get; }                                // observable
      public void Describe(string animal);                             // also DescribeCommand
      public void CommitRule(RuleLine line, string text);              // applies an edit; also re-runs
      public void AddRule();  public void DeleteRule(RuleLine line);   // commands; AddRule disabled at MaxRules
  }
  ```
  `AnswerText`, exactly: `Answer: tiger.` · `No animal fits these answers.` · `The rules give more than one answer: cheetah and tiger.` (three or more: `a, b and c`).

- [ ] **Step 1: Write the failing tests:** opens with the default 15 rules and the tiger described (`AnswerText == "Answer: tiger."`, trace rules 1, 5, 10); `Describe("bat")` gives `No animal fits these answers.`; ticking `dark spots` on the tiger gives the two-answer text; `CommitRule` with `if has hair mammal` sets that line's `Error` to the `then` message and leaves it out; adding `if mammal and flies then bat` with the bat described gives `Answer: bat.` and a new question appears for any new askable fact (`AddRule` then `CommitRule(…, "if mammal and barks then dog")` adds `Barks?`) while existing answers are kept; deleting a rule renumbers the rest; `AddRule` is disabled at 40 rules; `Reset` restores the 15 rules and the tiger; screens `trace` and `answer` are reported as baselines and light once on `Describe`.
- [ ] **Step 2: Run** `dotnet test tests/AIAnatomy.Presentation.Tests --filter "FullyQualifiedName~ExpertSystem"` — FAIL.
- [ ] **Step 3: Implement the view-model** (everything recomputes synchronously; it is tiny).
- [ ] **Step 4: Implement `ExpertSystemView`:** the questions as yes/no check boxes with their prompts; a row of `Describe:` buttons, one per animal; the trace inside `<c:Screen ScreenName="trace">` (each fired rule as a line, grouped by pass when there is more than one) and the answer inside `<c:Screen ScreenName="answer">`; the editable rule list (a numbered `TextBox` per rule, applied on Enter or leaving the box, with its error in text under it and an unused rule marked in words, "not used"), Add rule, a Delete button per rule. Screens stack under about 520 px of lab width, as in `RulesVsLearningView`.
- [ ] **Step 5: Lesson 4.2.** Confirm and add `shortliffe1976` (Shortliffe, E. H. (1976), *Computer-Based Medical Consultations: MYCIN*, Elsevier) to `sources.json`. Lesson: `lab: ExpertSystemLab`, `part: rules`, `sources: shortliffe1976, textbook`; explanation (forward chaining, facts and rules, strength: you can read why; weakness: brittleness, with the bat), Try this (describe the bat; add a rule for it; see another case break), 2–3 questions, one `checked-by-code: ExpertSystem_BatGetsNoAnswerFromTheDefaultRules`. Connection `expert-system` → `rules-vs-learning`. Remove `expert-system` from `StillComingLater`. Write the checked-question test through the view-model: after `Describe("bat")`, `AnswerText` is `No animal fits these answers.` and `Correct(...)` equals that text without its full stop (so the correct answer reads `No animal fits these answers`).
- [ ] **Step 6: Run** the filter, build the App, render `expert-system` (Light 13 pt and Dark 24 pt, `--full-lab`) and look.
- [ ] **Step 7: Run** the full suite (log to file) — PASS. **Commit** `Stage 2A: lesson 4.2, the expert system`.

### Task 8: Search — tic-tac-toe and minimax

**Files:**
- Create: `src/AIAnatomy.Models/Search/Board.cs`, `Minimax.cs`
- Test: `tests/AIAnatomy.Models.Tests/Search/MinimaxTests.cs`

**Interfaces:**
- Produces (namespace `AIAnatomy.Models.Search`):
  ```csharp
  public enum Mark { None, X, O }
  public sealed class Board   // immutable; cells 0–8 row by row from the top left; X always moves first
  {
      public static Board Empty { get; }
      public static Board Parse(string nine);       // "XX.OO....": '.', 'X', 'O'; whose turn follows from the counts; ArgumentException if impossible
      public Mark this[int cell] { get; }
      public Mark ToMove { get; }
      public Mark Winner { get; }                    // None if no line
      public IReadOnlyList<int>? WinningLine { get; }
      public bool IsOver { get; }                    // a winner or a full board
      public IReadOnlyList<int> EmptyCells { get; }
      public Board Play(int cell);                   // InvalidOperationException if taken or over
  }
  public enum Outcome { Loss = -1, Draw = 0, Win = 1 }   // from one player's point of view
  public sealed record MoveScore(int Cell, Outcome Outcome, int Plies, long PositionsLookedAt);
  public sealed record SearchResult(int BestCell, IReadOnlyList<MoveScore> Moves, long PositionsLookedAt);
  public static class Minimax
  {
      public static SearchResult Search(Board board, CancellationToken token = default);   // for board.ToMove; InvalidOperationException if over
  }
  ```
- Plain minimax: no pruning, no caching, to the end of the game. `PositionsLookedAt` counts every position visited, the root included; each move's count is its subtree (the position after the move included), so the total = 1 + the sum. `Plies` = moves to the end of the game with best play by both. Best move: Win before Draw before Loss; among wins fewer plies, among losses more plies; then the lowest cell. (Plain minimax would also let the computer dawdle before a sure win; the tie-break makes it take the win, and the lesson says so.)

- [ ] **Step 1: Write the failing tests:**
  - `TheFullGameTree_Has549946Positions_And255168FinishedGames`: an independent recursive count over `Board` (not through `Minimax`) gives 549,946 positions including the empty board and 255,168 finished games (the well-known counts; if they disagree, stop and check the counting with the rules above before touching either number, and log the outcome).
  - `Search_FromTheEmptyBoard_LooksAtEveryPosition_AndIsADraw`: `PositionsLookedAt == 549946`; every move's outcome is `Draw`.
  - `Search_TakesAnImmediateWin`: `Parse("XX.OO....")` (X to move) → `BestCell == 2`, `Win`, `Plies == 1`.
  - `Search_BlocksAThreat`: `Parse("XX..O....")` (O to move) → `BestCell == 2`; every other move is `Loss`.
  - `Search_NeverLoses_AsXOrAsO`: play the search against every possible sequence of opponent moves, with the search as X and as O; no game is lost.
  - `Search_IsCancellable`: a cancelled token throws `OperationCanceledException`.
  - `Board_RejectsImpossiblePositions_AndTakenCells`.
- [ ] **Step 2: Run** `dotnet test tests/AIAnatomy.Models.Tests --filter "FullyQualifiedName~Minimax"` — FAIL.
- [ ] **Step 3: Implement** both files. Check the token every 4,096 positions.
- [ ] **Step 4: Run** the filter — PASS; note the time of the empty-board search in the report (it must be well under a second).
- [ ] **Step 5: Commit** `Stage 2A: tic-tac-toe and plain minimax`.

### Task 9: Lesson 4.3 — the search lab

**Files:**
- Create: `src/AIAnatomy.Presentation/Labs/SearchLabViewModel.cs`, `src/AIAnatomy.App/Views/SearchView.xaml(.cs)`
- Modify: `LabFactory.cs`, `MainWindow.xaml`, `DiagnosticOptions.cs`, `PngRenderer.cs`, `content/lessons/family/03-search.md`, `content/connections.json`, `content/sources.json`
- Test: `tests/AIAnatomy.Presentation.Tests/Labs/SearchLabTests.cs`, `CheckedQuestionTests.cs`, `CommandLineTests.cs`, `LessonContentTests.cs` (remove `search`)

**Interfaces:**
- Consumes: Task 8.
- Produces:
  ```csharp
  public sealed partial class LookAheadNode : ObservableObject
  {
      public int Cell { get; }  public Outcome Outcome { get; }   // from the computer's point of view
      public long PositionsLookedAt { get; }  public bool IsChosen { get; }  public int Depth { get; }   // 1 = the computer's options
      public string Label { get; }   // "Centre: draw (7,331 positions)", "Top left: computer loses"
      public bool IsExpanded { get; set; }   // expanding fills Children (the replies, searched then); never past depth 4
      public ObservableCollection<LookAheadNode> Children { get; }
  }
  public sealed partial class SearchLabViewModel : LabViewModel
  {
      public const string Id = "SearchLab";
      public const int MaxDepth = 4;
      public Board Board { get; }  public Mark Human { get; }  public bool IsThinking { get; }   // observable
      public string StatusText { get; }    // "Your move (you are X).", "The computer is thinking…", "X wins: top row.", "A draw."
      public string CountText { get; }     // "The computer looked at 549,946 positions to choose its move."  or ""
      public ObservableCollection<LookAheadNode> LookAhead { get; }   // the options weighed for the computer's latest move
      public Task NewGameAsync(Mark human);   // also NewGameAsXCommand / NewGameAsOCommand; O means the computer opens
      public Task PlayAsync(int cell);        // also PlayCommand; ignored while thinking, when the cell is taken or the game over
  }
  ```
  Cell names: top left, top middle, top right, middle left, centre, middle right, bottom left, bottom middle, bottom right. Outcome words: computer wins / draw / computer loses. Render view `DiagnosticOptions.SearchMidgame = "search-midgame"` (human X in the centre, the computer's reply made, the first look-ahead node expanded; takes `--full-lab`).

- [ ] **Step 1: Write the failing tests:** opens as a new game with the human as X, an empty board and no look-ahead; `NewGameAsync(Mark.O)` makes the computer open, with `CountText` naming 549,946 and nine options all `Draw`, the chosen one marked; after the human plays the centre the computer replies and the look-ahead lists its 8 options with the chosen one marked; expanding a node fills its children with the replies, and depth-4 nodes cannot expand; review focus 4: `PlayAsync` while `IsThinking`, on a taken cell or after the game ends changes nothing, and `NewGameAsync` during a search throws the old result away (a generation counter, as in `RulesVsLearningLabViewModel`); a finished game's status names the winner and the line; `Deactivate` cancels a search; screens `board` and `lookahead` are baselines and light once per move; `CommandLineTests.SearchMidgame_IsARenderViewThatTakesFullLab`.
- [ ] **Step 2: Run** `dotnet test tests/AIAnatomy.Presentation.Tests --filter "FullyQualifiedName~SearchLab|FullyQualifiedName~CommandLine"` — FAIL.
- [ ] **Step 3: Implement the view-model.** Searches run on `Task.Run` with the lab's cancellation token; `WorkCompleted` covers the opening search when the computer starts.
- [ ] **Step 4: Implement `SearchView`:** the board as nine buttons with large X and O text and screen-reader names ("centre, empty"), the winning line drawn and named in the status; two buttons `New game: you play X (you go first)` and `New game: you play O (the computer goes first)`; the look-ahead as a `TreeView` of `LookAheadNode` (keyboard-expandable) with the count above it, inside `<c:Screen ScreenName="lookahead">`; the board inside `<c:Screen ScreenName="board">`.
- [ ] **Step 5: Lesson 4.3.** Confirm and add `shannon1950` (Shannon, C. E. (1950), "Programming a Computer for Playing Chess", *Philosophical Magazine*, Ser. 7, Vol. 41, No. 314). Lesson: `lab: SearchLab`, `part: search`, `sources: shannon1950, textbook`; explanation (look ahead, score the ends, assume the other side plays its best, back the scores up; the count grows fast; real programs prune (alpha-beta, as textbook material) and stop early with an estimate); a `## Formula` with the minimax recursion and its symbols in words; Try this; 2–3 questions, one `checked-by-code: Search_TheComputerLooksAt549946PositionsForItsFirstMove`. Connection `search` → `learning-by-reward` (both weigh what comes later). Write the checked test through the view-model (`NewGameAsync(Mark.O)`, then the count in `CountText` equals the number in `Correct(...)`).
- [ ] **Step 6: Run** the filter, build the App, render `search,search-midgame` (Dark 13 pt, `--full-lab`) and look.
- [ ] **Step 7: Run** the full suite (log to file) — PASS. **Commit** `Stage 2A: lesson 4.3, search`.

### Task 10: Classic machine learning — decision tree and nearest neighbours

**Files:**
- Create: `src/AIAnatomy.Models/ClassicMl/LabeledPoint.cs`, `DecisionTree.cs`, `NearestNeighbours.cs`, `StarterSets.cs`
- Test: `tests/AIAnatomy.Models.Tests/ClassicMl/DecisionTreeTests.cs`, `NearestNeighboursTests.cs`

**Interfaces:**
- Produces (namespace `AIAnatomy.Models.ClassicMl`):
  ```csharp
  public sealed record LabeledPoint(double X, double Y, int Label);   // Label 0, 1 or 2
  public enum Axis { X, Y }
  public abstract record TreeNode(int Count, double Gini);
  public sealed record Leaf(int Label, int Count, double Gini) : TreeNode(Count, Gini);
  public sealed record SplitNode(Axis Axis, double Threshold, TreeNode Below, TreeNode AtOrAbove, int Count, double Gini) : TreeNode(Count, Gini);
  public sealed record Region(double XMin, double XMax, double YMin, double YMax, int Label);
  public static class DecisionTree
  {
      public static double Gini(IEnumerable<int> labels);                          // 0 for no labels
      public static TreeNode Grow(IReadOnlyList<LabeledPoint> points, int maxDepth); // ArgumentException for no points or maxDepth < 0
      public static int Predict(TreeNode tree, double x, double y);
      public static IReadOnlyList<Region> Regions(TreeNode tree, double xMin, double xMax, double yMin, double yMax);   // tile the box
      public static int Depth(TreeNode tree);
  }
  public static class NearestNeighbours { public static int Classify(IReadOnlyList<LabeledPoint> points, double x, double y, int k); }
  public static class StarterSets { public static IReadOnlyList<(string Name, IReadOnlyList<LabeledPoint> Points)> All { get; } }
  ```
- Tree growing (CART with Gini): a node splits when its depth is below `maxDepth`, it is impure, and some axis has two distinct values. Candidate thresholds are midpoints between consecutive distinct values; `Below` takes values `< Threshold`. Choose the lowest weighted child Gini, even when it is no better than the parent (as scikit-learn does with its defaults; this lets corner (XOR) data split); ties go to axis X, then the lower threshold. A leaf's label is the majority, ties to the lower label.
- Nearest neighbours: Euclidean distance; `k` clamped to 1…n; equal distances in point order; a tied vote goes to the tied label whose point is nearest. `Classify` with no points throws `ArgumentException`.
- Starter sets (plot 0–10 on both axes, 20–40 points each, fixed coordinates in code): `Two groups`, `Two groups with odd points` (the default: two groups plus three points sitting in the other group's area), `Corners (XOR)`, `Diagonal`, `Three groups`.

- [ ] **Step 1: Write the failing tests:** `Gini([0,0,1,1]) == 0.5`, `Gini([0,0,0]) == 0`, `Gini([0,1,2])` = 2/3 within 1e-12; four points (1,1,0),(2,1,0),(4,1,1),(5,1,1) split once on X at 3 into two pure leaves; corners (1,1,0),(1,9,1),(9,1,1),(9,9,0): depth 1 gets 2 of 4 right, depth 2 splits X at 5 then Y at 5 and gets all 4; `maxDepth` 0 gives one leaf with the majority; regions tile the box with no gaps or overlaps and each region's label equals `Predict` at its centre; review focus 3: one point, all points one label, two points of different labels at the same place (no split possible: a leaf, label 0), 200 points (a test of speed: growing to depth 10 under 100 ms); k-NN: points (0,0,0),(1,0,0),(5,0,1) at (2,0): k 1 → 0, k 3 → 0; (0,0,0),(4,0,1) at (1,0), k 2 → 0 (tie to nearest); k = 50 with 3 points acts as k = 3; every starter set has 20–40 points inside 0–10 and labels from 0 up with none skipped.
- [ ] **Step 2: Run** `dotnet test tests/AIAnatomy.Models.Tests --filter "FullyQualifiedName~ClassicMl"` — FAIL.
- [ ] **Step 3: Implement** the four files.
- [ ] **Step 4: Run** the filter — PASS.
- [ ] **Step 5: Commit** `Stage 2A: decision tree and nearest neighbours`.

### Task 11: Lesson 4.4 — the classic machine learning lab

**Files:**
- Create: `src/AIAnatomy.Presentation/Labs/ClassicMlLabViewModel.cs`, `src/AIAnatomy.App/Controls/ClassPlot.cs`, `src/AIAnatomy.App/Views/ClassicMlView.xaml(.cs)`
- Modify: `LabFactory.cs`, `MainWindow.xaml`, both theme files (`ClassABrush`, `ClassBBrush`, `ClassCBrush`), `DiagnosticOptions.cs`, `PngRenderer.cs`, `content/lessons/family/04-classic-ml.md`, `content/connections.json`, `content/sources.json`
- Test: `tests/AIAnatomy.Presentation.Tests/Labs/ClassicMlLabTests.cs`, `CheckedQuestionTests.cs`, `CommandLineTests.cs`, `tests/AIAnatomy.App.Tests/ThemeContrastTests.cs`, `LessonContentTests.cs` (remove `classic-ml`)

**Interfaces:**
- Consumes: Task 10.
- Produces:
  ```csharp
  public enum ClassicMethod { DecisionTree, NearestNeighbours }
  public sealed partial class ClassicMlLabViewModel : LabViewModel
  {
      public const string Id = "ClassicMlLab";
      public const double PlotMin = 0, PlotMax = 10;
      public const int MaxPoints = 200, GridCells = 60, DefaultDepth = 3, MaxDepth = 10, DefaultK = 3, MaxK = 15;
      public ObservableCollection<LabeledPoint> Points { get; }
      public int NewPointLabel { get; set; }         // 0–2, the class the next added point gets
      public ClassicMethod Method { get; set; }
      public int Depth { get; set; }  public int K { get; set; }
      public IReadOnlyList<string> StarterNames { get; }
      public void LoadStarter(string name);           // also a command
      public bool AddPoint(double x, double y);       // clamped to the plot; false (and Message) at MaxPoints
      public bool RemoveNearest(double x, double y);  // within 0.3 plot units; false if none
      public void Clear();
      public IReadOnlyList<Region> Regions { get; }   // tree: the leaf rectangles; neighbours: GridCells × GridCells squares
      public IReadOnlyList<(Axis Axis, double At, double From, double To)> SplitLines { get; }   // tree only
      public IReadOnlyList<string> Questions { get; } // tree: "Is x < 3.2?" lines, indented two spaces per level, with "→ class A" leaves
      public string AccuracyText { get; }            // "Right on 18 of 20 training points (90%)."
      public string? Message { get; }                // "Add points of at least one class to see regions." when empty
  }
  ```
  Classes are shown as A, B, C; numbers in questions use `NumberText.Short`. Render view `DiagnosticOptions.ClassicMlNeighbours = "classic-ml-neighbours"` (nearest neighbours, k = 1, default set; takes `--full-lab`).

- [ ] **Step 1: Write the failing tests:** opens on `Two groups with odd points`, decision tree, depth 3; depth 10 on the default set gives `Right on N of N` (overfitting shown); k = 1 also gets every training point right, k = 15 does not; `Questions` for the four-point set read `["Is x < 3?", "  yes → class A", "  no → class B"]`; switching method, moving a slider, adding or removing a point recomputes; review focus 3: an empty plot gives the message and no regions, one point shades everything its class, the 201st point is refused with `Message` `"The plot holds at most 200 points."`; `Reset` restores the defaults; screens `regions` and `questions` are baselines and light once on a slider change; `CommandLineTests.ClassicMlNeighbours_IsARenderViewThatTakesFullLab`. In `ThemeContrastTests`: the three class brushes meet 3:1 on Surface and Screen and pass the colour-blind distance check among themselves.
- [ ] **Step 2: Run** `dotnet test tests/AIAnatomy.Presentation.Tests --filter "FullyQualifiedName~ClassicMl|FullyQualifiedName~CommandLine"` and `dotnet test tests/AIAnatomy.App.Tests` — FAIL.
- [ ] **Step 3: Implement the view-model** (recompute synchronously; a 60×60 grid against 200 points is about 0.7 M distances).
- [ ] **Step 4: Implement `ClassPlot`** (derive from `ChartBase` for axes): shaded regions at low opacity of the class brush; points drawn as a circle (A), a square (B) and a triangle (C) so colour is not the only carrier; split lines; a click adds a point of `NewPointLabel`, a right-click removes the nearest; keyboard: focusable, arrow keys move a visible cross by 0.5, Enter adds, Delete removes the nearest; a point's tooltip "class A at (2.5, 3)". The view: method switch, depth and k sliders with their values, class chooser, starter buttons, Clear, the plot inside `<c:Screen ScreenName="regions">`, the questions inside `<c:Screen ScreenName="questions">` (in neighbours mode it says nearest neighbours asks no questions, it compares distances), the accuracy line.
- [ ] **Step 5: Lesson 4.4.** Confirm and add `breiman1984` (Breiman, Friedman, Olshen and Stone (1984), *Classification and Regression Trees*, Wadsworth) and `coverhart1967` (Cover and Hart (1967), "Nearest neighbor pattern classification", *IEEE Transactions on Information Theory* 13(1)). Lesson: `lab: ClassicMlLab`, `part: classic`, `sources: breiman1984, coverhart1967, textbook`; explanation (learned from examples without gradients; the tree's questions are found by search for the purest split; nearest neighbours keeps every example; overfitting); `## Formula` with Gini impurity and the distance; Try this; 2–3 questions, one `checked-by-code: ClassicMl_ADepth10TreeGetsEveryDefaultPointRight`. Connection `classic-ml` → `learning`.
- [ ] **Step 6: Run** the filters, build the App, render `classic-ml,classic-ml-neighbours` (Light 13 pt and Dark 24 pt, `--full-lab`) and look.
- [ ] **Step 7: Run** the full suite (log to file) — PASS. **Commit** `Stage 2A: lesson 4.4, classic machine learning`.

### Task 12: Learning by reward — grid world and Q-learning

**Files:**
- Create: `src/AIAnatomy.Models/Reinforcement/GridWorld.cs`, `QLearningAgent.cs`
- Test: `tests/AIAnatomy.Models.Tests/Reinforcement/QLearningTests.cs`

**Interfaces:**
- Produces (namespace `AIAnatomy.Models.Reinforcement`):
  ```csharp
  public enum Move { Up, Right, Down, Left }
  public readonly record struct GridCell(int Column, int Row);   // (0, 0) is the top left
  public sealed class GridWorld
  {
      public GridWorld(int columns, int rows, IEnumerable<GridCell> walls, GridCell start, GridCell goal);
          // ArgumentException: size outside 2–12, a cell off the grid, start == goal, start or goal on a wall
      public int Columns { get; }  public int Rows { get; }  public GridCell Start { get; }  public GridCell Goal { get; }
      public IReadOnlySet<GridCell> Walls { get; }
      public bool IsWall(GridCell cell);
      public GridCell Next(GridCell from, Move move);   // off the grid or into a wall: stays put
      public bool GoalReachable { get; }                // breadth-first search from Start
  }
  public sealed record QSettings(double GoalReward = 10, double StepCost = 1, double Discount = 0.9,
      double ExplorationRate = 0.2, double LearningRate = 0.5, int MaxStepsPerEpisode = 200);
  public sealed record StepResult(GridCell From, Move Move, GridCell To, double Reward, bool EpisodeEnded);
  public sealed class QLearningAgent
  {
      public QLearningAgent(GridWorld world, QSettings settings, int seed);
      public GridWorld World { get; }  public QSettings Settings { get; }
      public double ExplorationRate { get; set; }       // may change while learning
      public GridCell Position { get; }  public int Episodes { get; }  public int StepsThisEpisode { get; }
      public double Q(GridCell cell, Move move);
      public double Value(GridCell cell);               // max over moves; 0 at the goal
      public Move? Preferred(GridCell cell);            // highest Q, ties in enum order; null at the goal, on a wall, or when all four are equal
      public StepResult Step();                          // one move
      public int RunEpisode();                           // steps until the goal or MaxStepsPerEpisode; returns the steps
  }
  ```
- One step: choose a move ε-greedily (with probability `ExplorationRate` a uniformly random move, otherwise the highest Q with ties broken at random, all from the seeded `SeededRandom`); reward is `GoalReward` when the move enters the goal, otherwise `−StepCost` (bumping a wall costs a step too); update `Q(s,a) += LearningRate × (r + Discount × V(s′) − Q(s,a))` with `V(goal) = 0`; an episode ends on reaching the goal or after `MaxStepsPerEpisode` steps, and the agent goes back to `Start`.

- [ ] **Step 1: Write the failing tests** (hand-worked values with reward 10, step cost 1, discount 0.9: one step from the goal 10, two 8, three 6.2, four 4.58):
  - `OpenTwoByThree_ConvergesToTheHandValues`: 3 columns × 2 rows, no walls, start (0,0), goal (2,0); `ExplorationRate` 1, learning rate 0.5, seed 1, 5,000 episodes: values (1,0) 10, (0,0) 8, (2,1) 10, (1,1) 8, (0,1) 6.2, each within 1e-3; `Preferred((1,0)) == Right`.
  - `WallInTheMiddle_ConvergesAroundIt`: same grid with a wall at (1,0): (2,1) 10, (1,1) 8, (0,1) 6.2, (0,0) 4.58.
  - `Next_StaysPutAtEdgesAndWalls`; `BumpingAWall_CostsAStep`.
  - `Preferred_IsNullBeforeLearning`.
  - `SameSeed_SameTable`: two agents, same seed, 100 episodes each: equal Q everywhere.
  - Review focus 2: `GoalWalledOff_IsNotReachable_AndEpisodesStopAtTheCap` (`RunEpisode` returns `MaxStepsPerEpisode`); constructor rejects start on a wall, goal on a wall, start == goal.
- [ ] **Step 2: Run** `dotnet test tests/AIAnatomy.Models.Tests --filter "FullyQualifiedName~QLearning"` — FAIL.
- [ ] **Step 3: Implement** both files.
- [ ] **Step 4: Run** the filter — PASS.
- [ ] **Step 5: Commit** `Stage 2A: grid world and Q-learning`.

### Task 13: Lesson 4.5 — the learning-by-reward lab

**Files:**
- Create: `src/AIAnatomy.Presentation/Labs/RewardLabViewModel.cs`, `src/AIAnatomy.App/Controls/GridWorldView.cs`, `src/AIAnatomy.App/Views/RewardView.xaml(.cs)`
- Modify: `LabFactory.cs`, `MainWindow.xaml`, `DiagnosticOptions.cs`, `PngRenderer.cs`, `SelfTests.cs` (a short Run, as for Rules vs learning), `content/lessons/family/05-learning-by-reward.md`, `content/connections.json`, `content/sources.json`
- Test: `tests/AIAnatomy.Presentation.Tests/Labs/RewardLabTests.cs`, `CheckedQuestionTests.cs`, `CommandLineTests.cs`, `LessonContentTests.cs` (remove `learning-by-reward`; the placeholders are now `beyond-coming` and the five Part B ids)

**Interfaces:**
- Consumes: Task 12.
- Produces:
  ```csharp
  public enum MazeTool { Wall, Start, Goal }
  public sealed record MazeSquare(GridCell Cell, bool IsWall, bool IsStart, bool IsGoal, bool HasAgent,
      double Value, string ValueText, Move? Arrow, string AutomationName);   // "Square 3, 2: value 4.58, prefers right"
  public sealed partial class RewardLabViewModel : LabViewModel
  {
      public const string Id = "RewardLab";
      public const int Columns = 6, Rows = 6, MaxEpisodesPerRun = 300;
      public MazeTool Tool { get; set; }
      public double GoalReward { get; set; }      // 1–20, default 10
      public double StepCost { get; set; }        // 0–2, default 1
      public double ExplorationRate { get; set; } // 0–1, default 0.2
      public double Discount { get; set; }        // 0.5–0.99, default 0.9
      public IReadOnlyList<MazeSquare> Squares { get; }      // row by row
      public string EpisodesText { get; }    // "Episodes: 42. The last one took 17 steps."
      public IReadOnlyList<LineSeries> StepsSeries { get; }  // steps per episode
      public string? Message { get; }
      public void Apply(GridCell cell);       // the current Tool on that square
      public void StepOnce();                 // StepCommand
      // RunCommand (async, with RunCancelCommand as Stop): episodes until Stop or MaxEpisodesPerRun
  }
  ```
- Defaults (fixed, restored by Reset): the 6×6 maze with start (0,0), goal (5,5) and walls (1,1), (2,1), (3,1), (3,2), (3,3), (1,3), (1,4), (4,4); seed 1. Changing walls, start, goal, the goal reward, the step cost, the discount or the seed (the lab's `Seed`, shown and editable) clears what the agent has learned, and `Message` says so (`"The maze or the rewards changed, so the agent starts learning again."`); the exploration rate does not. A wall on the start or goal, or start onto the goal, is refused (`Message`, nothing changes). An unreachable goal is allowed and `Message` says `"The goal can't be reached from the start."`. During Run the squares and chart update at most 20 times a second (after whole episodes); the whole Run is one `BeginRun`. Render view `DiagnosticOptions.RewardTrained = "reward-trained"` (300 episodes run before the picture; takes `--full-lab`).

- [ ] **Step 1: Write the failing tests:** defaults as above with nothing learned (no arrows, every value 0); `StepOnce` moves the agent one square; Run for 300 episodes then the square next to the goal on the shortest path is worth more than the start and its arrow points along a shortest path; Stop ends a Run within one episode; maze edits and reward changes clear learning with the message, the exploration rate does not; review focus 2: a wall on the start is refused, walling off the goal gives the unreachable message and Run still ends at the cap; `Reset` restores everything; `Deactivate` cancels a Run; screens `values` and `episodes` are baselines, a Run lights each once at its end; `CommandLineTests.RewardTrained_IsARenderViewThatTakesFullLab`.
- [ ] **Step 2: Run** `dotnet test tests/AIAnatomy.Presentation.Tests --filter "FullyQualifiedName~RewardLab|FullyQualifiedName~CommandLine"` — FAIL.
- [ ] **Step 3: Implement the view-model.**
- [ ] **Step 4: Implement `GridWorldView`:** squares shaded by value (one hue, darker for higher values, scaled between the lowest and highest value on the grid); walls hatched, not just coloured; start and goal lettered S and G; the agent a ring; each square's preferred move an arrow; hover or keyboard focus shows "Square 3, 2: value 4.58, prefers right" in text under the grid; arrow keys move the focus square, Space applies the tool; the grid inside `<c:Screen ScreenName="values">`. The view adds the tool chooser, the four sliders with their values, Step, Run, Stop, the seed box, and the episodes text and steps chart inside `<c:Screen ScreenName="episodes">`. `SelfTests.AllLabsAsync` runs 5 episodes for this lab and checks `EpisodesText` names them.
- [ ] **Step 5: Lesson 4.5.** Confirm and add `watkins1992` (Watkins and Dayan (1992), "Q-learning", *Machine Learning* 8, 279–292; the method first appeared in Watkins's 1989 PhD thesis, which the lesson may mention). Lesson: `lab: RewardLab`, `part: reward`, `sources: watkins1992, suttonbarto2018, textbook`; explanation (no right answers given, only rewards; values; exploring versus using what it knows; discount); `## Formula` with the Q-learning update and its symbols in words; Try this; 2–3 questions, one `checked-by-code: Reward_ASquareTwoStepsFromTheGoalIsWorthEight` (with the lab's default reward, step cost and discount; computed with `QLearningAgent` on the lab's default maze and settings, `ExplorationRate` 1 for 5,000 episodes, on a square two steps from the goal). Connection `learning-by-reward` → `learning`. A "Brains and models" note only with a confirmed source (for example reward-prediction errors in dopamine neurons), never claiming the brain runs Q-learning.
- [ ] **Step 6: Run** the filters, build the App, render `learning-by-reward,reward-trained` (Dark 13 pt and Light 24 pt, `--full-lab`) and look.
- [ ] **Step 7: Run** the full suite (log to file) — PASS. **Commit** `Stage 2A: lesson 4.5, learning by reward`.

### Task 14: Final rendered check, self-test and records

**Files:**
- Modify: `docs/decisions.md`, `README.md`, `docs/final-check.md`, `docs/research/scientific-inconsistencies.md` (if anything was found), `private/AIAnatomy-issues.xlsx` (git-ignored)

- [ ] **Step 1: Self-test.** `dotnet build src/AIAnatomy.App`, then `AIAnatomy.App.exe --selftest all-labs`; expect exit code 0 and every section-4 lesson listed.
- [ ] **Step 2: Renders,** one launch per theme and size (6 launches): the five Part A lessons, one placeholder (`image-filters`), `ideamap-radial`, `ideamap-loop`, and the four new render views, in Dark and Light at 10, 13 and 24 pt, `--full-lab` where it applies. Look at every picture for clipping, overlap, unreadable text, contrast and the review-focus cases (tree at 24 pt; the Radial map's section 4). Fix what is wrong and render again only the views that changed.
- [ ] **Step 3: Records.** In `docs/decisions.md` under `## Stage 2 Part A`: the screens and branch anchors of each new lab, the render views, each Task's rulings, the citation checks. In `README.md`: section 4's five lessons and that 4.6–4.10 come later. In `docs/final-check.md`: this check. Issues found and not fixed go in the issues sheet with Severity and Priority.
- [ ] **Step 4: Run** the full suite (log to file) — PASS (read the log; every project passed).
- [ ] **Step 5: Commit** `Stage 2A: final rendered check and records`.
