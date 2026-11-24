# AIAnatomy: UI rework (order, idea map, instrument-panel look)

Date: 2026-11-23
Status: draft for the owner's review
Amends: `2026-11-22-aianatomy-design.md` (the Stage 1 design). Where the two disagree, this document wins.

## 1. Intent and the why

Stage 1 works, but the owner found the interface **plain and confusing**:

- **No visible order:** it was not clear where to start or what comes next. The five top tabs (Map, Parts, Whole,
  Family, Beyond) and a flat lesson list did not show how the lessons build on each other.
- **The opening lesson hid its point:** in Rules versus learning it was hard to see *what the learned model writes*,
  and that writing is the whole point of the lesson.
- **A display bug:** some expander headers in the Explanation pane were black on dark grey in the Dark theme
  (already fixed, not yet committed; see 8).

The owner wants to move through the material in two ways, and keep both:

- **Like a tree view**, which acts as a table of contents, in order.
- **Like a mind map** that shows *how the ideas connect*, which works like an index. The owner reads spatial maps
  easily and prefers the **radial** map. The **loop** map (the path text takes through the model, with learning acting
  on it from outside) is also included, because it shows how the parts work together. The user chooses between them.

The owner chose the **instrument-panel look** (direction A in the mock-ups): dark, each part in its own colour,
and outputs on glowing "screens" that look different from the controls you change.

## 2. Goals, non-goals, anti-goals

**Goals**
- Order is visible everywhere: numbered sections and lessons, a "you are here" path, and Next/Back buttons.
- Every lesson page has the same shape: header band with the big idea, Try this, the lab, Check yourself,
  How this connects, Next.
- Outputs are unmistakable: what the model produces sits on a screen and briefly lights up when it changes.
- The idea map shows only the chosen bubble's connections, each explained in a sentence.
- Short, skippable Check yourself questions on every built lesson.

**Non-goals**
- No change to the models, the engine, the maths or what each lab computes. Labs are restyled and rearranged, not
  rewritten.
- No new lessons. Stages 2 and 3 stay greyed out ("later").
- No look switching beyond the existing Dark and Light themes. No Paper theme, no font choice (owner's decision).

**Anti-goals (failure even if it "works")**
- **A pretty map that teaches nothing:** a connection line without a sentence saying what the connection is.
- **A quiz that is wrong:** a Check yourself answer that disagrees with what the app's own models and tokenizers
  actually do.
- **Glow that lies:** a screen that lights up when nothing changed, or an animated value that does not come from the
  running code (the Stage 1 "no fake animations" rule still holds).
- **Colour as the only signal:** every part colour is paired with an icon and a name.
- **Anything that needs sound.**

## 3. Constraints

Everything in the Stage 1 design's constraints still holds, with these changes:

- **Text size:** default body text **13 pt**. The Settings slider runs from **10 to 24 pt**. This replaces
  "20 pt body, 28 pt headings, 16 to 32 pt", which was my assumption carried over from ASLCoach, not the owner's
  request.
  *My call, from the approved mock-up's proportions:* lesson titles are 2 × body, section headings 1.15 × body,
  small text (labels, captions) 0.85 × body but never under 9 pt. The separate heading-size setting goes; one
  slider sets everything. The old "small text never under 16 pt" floor goes.
  *My call:* an existing `settings.json` is moved to the new scale once: a saved body size of 16 or more (the old
  slider's range) becomes 13 pt; anything else is kept. The owner's own file holds 16 pt, the old minimum, so it
  moves to 13 pt as they chose.
- **Default theme:** Dark for a new install (*my call*; the look was designed dark first). A saved theme is kept.
- **New dependency:** `AutomaticGraphLayout` (Microsoft's MSAGL layout engine), version 1.1.12, MIT licence,
  .NET Standard 2.0, used **only to compute bubble positions and line routes** for the idea map. The app draws the map
  itself, so the theme, focus ring and text size apply. This amends the Stage 1 rule "no other third-party runtime
  libraries"; it is a layout library and does none of the AI maths. Its licence goes in `THIRD-PARTY-NOTICES.txt`.
  The MSAGL WPF viewer control is not used.
- **Animations:** the "light up on change" effect lasts under a second, never flashes more than three times a second,
  and is off when Windows' "Show animations" setting is off.
  *My call on when a screen lights up:* when its shown value changes after a direct user action (a typed word, a
  moved slider, a button), and once when a run the user started (training, a Learn press) finishes. Not on each
  live update during a run, because the changing numbers are motion enough. A recalculation that gives the same
  value stays dark. Each lab view-model says which screen changed; the `Screen` control does not guess.
- **Contrast:** body text at least 4.5:1 against its background, part colours and lines at least 3:1, in both themes.
- **Visual checks** still go through `--render-png`, never screen capture.

## 4. Approaches considered

**How the idea maps are laid out**

1. **Hand-placed positions** in a data file, copied from the approved mock-ups. Exact control and no new dependency,
   but each new lesson's bubble is placed by hand.
2. **Automatic layout with MSAGL.** *Chosen by the owner.* New lessons are placed automatically when Stages 2 and 3
   arrive. Trade-off: the result looks more generic, and the mock-ups' radial rings and loop shape are hard to get from
   a general layout engine. Hence the checkpoint in 5.3.
3. **Show the HTML mock-ups inside the app (WebView2).** Fastest to the exact mock-up look, but two kinds of screen
   to keep in step for themes, text size and keyboard use, plus an extra runtime. Rejected.

**The look**

- **A, instrument panel.** *Chosen by the owner.*
- **B, illustrated textbook** (paper, serif headings, margin notes). Not chosen. Offering both as a setting was
  discussed. Colours and fonts would be cheap to switch, but B's layout would double the screen work. The owner chose
  to keep only Dark and Light.

## 5. What the user sees

### 5.1 Shell

```
┌ AIAnatomy ───────────── Model: [Fables (shipped) ▾]   Sources   Settings ┐
│ [Contents | Idea map]  │ 2 The parts › 2.4                               │
│ 1 The big picture      │ ▌◆ Attention                                    │
│   ✓ 1.1 Rules vs learn │ ▌  Each token gathers what it needs from the     │
│   ✓ 1.2 Architecture   │ ▌  tokens before it.                            │
│ 2 The parts            │ ┌ Try this ──┐ ┌ Lab ─────────────────────────┐ │
│   ✔ 2.1 Tokens         │ │ 1. …       │ │ controls      ┌ screen ─────┐│ │
│   ✓ 2.2 Embeddings     │ │ 2. …       │ │               │ output      ││ │
│   · 2.3 Position       │ └────────────┘ │               └─────────────┘│ │
│   ▸ 2.4 Attention      │ Explanation    └──────────────────────────────┘ │
│       By hand          │ (text, Show formula, Brains and models)         │
│       In the model     │ ┌ Check yourself ───────────────── [Skip] ┐     │
│       Induction heads  │ └──────────────────────────────────────────┘     │
│   …                    │ How this connects: ● Multi-head ● Residual …    │
│ 4 The family of AI     │ [← 2.3 Position]           [Next: 2.5 Multi-head →] │
│ 5 Beyond the model     │                                                  │
└────────────────────────┴──────────────────────────────────────────────────┘
```

- The five top tabs are replaced by a **contents tree** on the left. It is always visible, with collapsible groups.
- **Numbered sections:** 1 The big picture (Rules vs learning, Architecture map), 2 The parts (2.1 Tokens to
  2.9 Learning), 3 The whole model (Train, Step through, Scale), 4 The family of AI and 5 Beyond the model (greyed,
  "later"). The numbers come from each lesson's section and order, so they never need typing by hand.
  *My call:* section 1 is renamed from "Map" to "The big picture", so it can't be confused with the idea map or the
  Architecture map.
- **Lesson branches:** a lesson made of several parts (Attention: by hand, in the model, induction heads; Learning:
  its four panels) shows them as child items. Clicking one scrolls the lesson to that part.
- **Marks:** a lesson gets a plain tick (✓) once opened, and a filled tick (✔) once its Check yourself questions
  have been answered or skipped. A dot means not yet opened. *My call:* skipping counts for the filled tick, so
  skipping never feels like a penalty. Settings has "Reset progress".
- **Contents | Idea map** switch at the top of the tree. Choosing Idea map replaces the lesson page with the map; the
  tree stays visible.
- The model picker, Sources and Settings move into one slim top bar.
- **Keyboard:** the tree uses the arrow keys; Tab moves on to the lesson page.

### 5.2 Lesson page

Top to bottom:

1. **Path:** "2 The parts › 2.4".
2. **Header band** in the lesson's part colour, with its icon, title and one-sentence **big idea**.
3. **Try this:** two to four numbered steps that tell the user what to change in the lab and what to look for.
4. **The lab** and the **explanation** side by side (the explanation drops below the lab when the window is narrow,
   as now). The explanation keeps Show formula and Brains and models.
5. **Check yourself** (5.5).
6. **How this connects:** a row of chips, one per connection of this lesson, each in the other lesson's colour.
   Hovering or focusing a chip shows its sentence; clicking opens that lesson.
7. **Back / Next** buttons, which follow the contents order.

**Screens:** any output the model produces (generated text, probabilities, predictions, heat maps, the loss) sits in
a "screen" panel. It has a darker inset background, a part-coloured edge, and in Dark a soft glow. When its value
changes because of something the user did, the edge brightens briefly and fades (see the Animations constraint).
Controls the user changes sit outside the screens, on the plain panel background.

**Part colours:** one per part, from a colour-blind-safe set (Okabe-Ito based), tuned separately for Dark and Light
so both meet the contrast rule. Each part also has an icon. The same colour marks that part in the tree, the header
band, the screens, token chips, the idea map and the How this connects chips.

### 5.3 Idea map

- A **Radial | Loop** switch. Radial is the default; the app remembers the last choice.
- **Radial:** "How a transformer works" in the centre, the five sections around it, and each section's lessons
  around their section. The edges are the contents tree.
- **Loop:** the path text takes: Your text → Tokens → Embeddings (+ Position) → Residual stream, with Attention,
  Multi-head and Feed-forward inside a "Block × N" group reading from it and adding back → Output and sampling →
  Next character → back to Your text. Learning sits outside and connects to the parts it changes. Rules vs learning,
  Train, Architecture map, Step through and Scale connect to Learning or to the whole loop, as in the mock-up.
  "Your text" and "Next character" are bubbles with no lesson; their card has a short description instead.
- **Chosen bubble only:** nothing but the tree or flow lines shows until the user chooses a bubble. Then its
  connections light up in gold, and a **side card** lists each connection's sentence plus an **Open lesson** button.
- Bubbles show the lesson's number, title, part colour and tick mark. Stage 2 and 3 bubbles are greyed.
- **Keyboard:** Tab moves between bubbles in contents order; Enter or Space chooses one; Enter on Open lesson opens it.
- **Layout by MSAGL** (*builder's choice of MSAGL algorithm and settings*, aiming for the mock-ups' shapes): the root
  pinned in the centre for Radial; a layered top-to-bottom flow with the return edge routed round the side for Loop.
  The layout is computed once per text size, not on every click.
- *My call:* when the map is bigger than its area (large text, small window), it **scrolls** rather than shrinks, so
  the user's text size always holds. Choosing a bubble by keyboard scrolls it into view. At the default 13 pt in a
  1280 × 800 window, the whole map fits without scrolling.
- **Checkpoint (must happen before the rest of the map work):** render both maps to PNG in both themes and show the
  owner. If they don't read clearly, the owner decides whether to tune MSAGL further or switch to hand-placed
  positions (approach 1). That choice changes only the layout step; the data and drawing stay the same.

### 5.4 Rules versus learning

- Two screens side by side: **Written rules** on the left (the rule program's output, with its rules listed below)
  and **Learned from examples** on the right.
- The learned screen holds a **history**. Each time the user presses Train, a new entry goes on top: the step count,
  the loss, and the text the model wrote at that point. The newest entry is highlighted; older ones are dimmer. Entry
  zero is the untrained model's gibberish. *My call:* up to 8 entries, oldest dropped first, plus Reset.
- *My call:* during a Learn run, the top entry updates live after each chunk (as the text does now) and freezes when
  the run ends. Entries are frozen records and are never rewritten. Changing the start text or the seed clears the
  history and starts a new one from the model as it is now, so the list never mixes outputs from different start
  texts. The history header names the start text it was written from.
- The loss chart stays, smaller, below the two screens.
- Try this, for example: "1. Read the gibberish under Learned. 2. Press Train three times. 3. Watch words appear."

### 5.5 Check yourself

- Two or three questions per built lesson (Stage 2 and 3 placeholders have none).
- **Multiple choice:** click an answer and get feedback straight away: right or wrong, one or two sentences of why,
  and a link that scrolls to the part of the lesson that explains it. A wrong answer can be retried.
- Where it helps, the feedback suggests a lab action to see the answer for yourself (for example "Type ' foxes' in the
  lab and count the chips").
- **Skip** button. No score is shown or kept beyond answered or skipped.
- **Accuracy:** any question whose answer depends on the app's own models, tokenizers or bundled text is marked as
  such in its file and checked by a unit test that computes the real answer. If the shipped model or tokenizer changes,
  that test fails rather than the question going quietly wrong.
- *My call:* a question may depend only on things the user can't change: general ideas, the character tokenizer's
  rules, or the **shipped** Fables model and BPE tokenizer at their default settings, named as such in the question
  ("In the shipped Fables model…"). Questions are never about the active model, a user-trained model, or a slider
  position. A lab suggestion in the feedback names the settings to use ("with the merges slider at its default").
  The answers are fixed in the files; nothing is worked out live.

## 6. Architecture

The Stage 1 layering stays: App → Presentation → Lessons → Models → Core.

### 6.1 Content (Lessons project and `content/`)

- **Lesson front matter gains:** `idea:` (the one-sentence big idea), `part:` (a colour key such as `tokens`,
  `attention`, `learning`), and optional `branches:` (child items, each a label plus the lab section it scrolls to).
- **Lesson body gains** a `## Try this` section (a numbered list) and a `## Check yourself` section. Questions are
  written in a small fixed format: the question line, answer lines with the correct one marked, a `why:` line, a
  `see:` anchor, and an optional `checked-by-code: <test name>`. *Builder's call:* the exact syntax, kept readable
  for someone editing in Notepad.
- **`content/connections.json`:** one list of connections, each `{ from, to, sentence, sources }`, where `to` may be
  `LOOP` for "the whole loop". The idea map, the How this connects row and the side card all read it.
- **`content/idea-map.json`:** the map's nodes that are not lessons ("Your text", "Next character", the Block group,
  the centre "How a transformer works") with their descriptions, and the Loop flow edges with their labels. Lesson
  bubbles come from the catalogue. No coordinates are stored.
- **Validation when loading:** every connection end and flow end exists; every built lesson has an idea, a part,
  Try this and at least two questions; every question has exactly one correct answer; every `see:` anchor exists.
  Errors name the file, as now.

### 6.2 Presentation

- `ContentsTreeViewModel`: sections, lessons, branches, numbers and marks.
- `ProgressService`: opened and checked lessons, saved in `%LOCALAPPDATA%\AIAnatomy\progress.json`, with Reset.
- `IdeaMapViewModel`: nodes, edges, the chosen bubble, its connections, Radial/Loop mode.
- `IGraphLayout` with `MsaglGraphLayout`: input is nodes with sizes plus edges plus a mode; output is a position for
  each node and a polyline for each edge. Kept behind an interface so hand placement (approach 1) could replace it.
- `CheckYourselfViewModel`: questions, chosen answers, feedback, Skip.
- `LessonPageViewModel`: path, header, Try this, the lab, connections, Back and Next. `MainViewModel` loses the
  section tabs.
- `RulesVsLearningLabViewModel` gains the history list.

### 6.3 App

- `ContentsTree` (WPF `TreeView` with the theme's styles), `IdeaMapView` (draws bubbles and lines itself from the
  layout result), `LessonHeader`, `TryThisBox`, `Screen` (a styled `ContentControl` with the glow and the
  light-up-on-change effect), `CheckYourselfView`, `ConnectionChips`.
- `Themes/Dark.xaml` and `Light.xaml` gain the part colours, screen brushes and glow settings. `Shared.xaml` gains the
  new styles.
- Existing lab views are moved onto the screen and controls pattern. Their view-models and computations don't change.
- `--render-png` learns `ideamap-radial`, `ideamap-loop` and `contents`.

## 7. Checks

- **Unit tests:** content validation (6.1), contents numbering and order, progress saving and Reset, Next/Back
  order, the settings migration, the history cap, and every `checked-by-code` question.
- **Layout tests:** for both map modes, at the smallest and largest text size: no two bubbles overlap, every bubble is
  inside the layout's own bounds, and no line passes through a bubble it doesn't connect. At 13 pt the bounds fit a
  1280 × 800 window beside the contents tree.
- **Contrast test:** every part colour and text brush meets the contrast rule against its background, in both themes.
- **Rendered checks:** every lesson page, both idea maps and Settings rendered to PNG in Dark and Light at 10 pt,
  13 pt and 24 pt, and looked over for clipping, overlap and unreadable text.
- **Accuracy:** every sentence in `connections.json` and every question is held to the Stage 1 accuracy rule
  (section 7.2 of that design).

## 8. Delivery

- One implementation plan, built on its own branch and merged into local `main` on the owner's go-ahead.
- First, with the owner's OK, the two uncommitted display fixes (expander header colour, Explanation focus ring)
  are committed to `main`.
- The idea-map checkpoint (5.3) is an early task. The owner sees both maps before the rest of the map work is built.
- Decisions are logged in `docs/decisions.md`; issues in `private/AIAnatomy-issues.xlsx`, as before.

## 9. Left to the builder

- Icons for each part, and the exact part colours within the contrast rule.
- MSAGL algorithm choice and settings for each map mode, up to the checkpoint.
- Wording of each big idea, Try this, connection sentence and question, held to the accuracy rule.
- Glow strength and light-up timing within the Animations constraint.
- The question file syntax.
