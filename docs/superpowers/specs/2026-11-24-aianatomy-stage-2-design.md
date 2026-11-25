# AIAnatomy Stage 2 — The family of AI — Design

Date: 2026-11-24
Status: draft for the owner's review

Builds on: `docs/superpowers/specs/2026-11-22-aianatomy-design.md` (the app's design; its section 5.3 first described
Stage 2) and `docs/superpowers/specs/2026-11-23-aianatomy-ui-rework-design.md` (the lesson page, contents tree, idea
map, screens and Check yourself that every lesson now uses). Where this document differs from 5.3, this document wins.

## 1. Intent and the why

Stage 1 took the owner inside one kind of AI, the transformer. Stage 2 shows the **other members of the family**: what
each one is, how it works, how it learns (or doesn't), and what it is good and bad at. The owner asked for this to be
**general**, about the parts of AI in general. It is not about Claude: how an assistant like Claude fits the parts
together is Stage 3's subject. Claude may appear as one example among many, never as the focus.

The app's central idea carries on: every member is a strict computer doing exact arithmetic. What differs is where the
behaviour comes from: written by a person (rules), worked out by looking ahead (search), learned from examples
(classic machine learning and neural networks), or learned from rewards.

## 2. Goals, non-goals, anti-goals

**Goals**
- One lesson with a working lab for each of the seven family members, plus a second lesson for the two members whose
  inner workings are new ideas the transformer didn't have (the image network's filters, diffusion's steps), plus an
  opening family-tree lesson: **10 lessons** in section 4, "The family of AI".
- Each lab runs the real algorithm on real numbers you can see and change, as in Stage 1.
- The neural labs (image network, memory network, diffusion) **open already trained** and work at once, with a
  "train it again" button that trains a fresh network from scratch in front of you (owner's choice).
- Every lesson has the full UI rework treatment: big idea, part colour, Try this, screens, Check yourself, How this
  connects, a place in the contents tree and the idea map.

**Non-goals**
- How an assistant combines the members (moved to Stage 3; see section 8).
- State-of-the-art results. Each member is small on purpose.
- LSTMs, transformers for images, GANs, and other members beyond the seven. They may be mentioned in text.

**Anti-goals** (failure even if it "works"), carried over from the app design:
- A visual that looks like a computation but isn't one.
- Over-claiming either way ("it thinks like you" / "it's just statistics").
- Anything that depends on sound.

## 3. Constraints

All of the app design's constraints (section 3 there) and the UI rework's still hold. In particular:
- C#/WPF, .NET 10, MVVM with CommunityToolkit.Mvvm; WpfMath for formulas; MSAGL for map layout. No new third-party
  runtime libraries.
- All AI maths is our own code on our own `Core` engine. New engine operations get the same finite-difference
  gradient checks as Stage 1's.
- Every lab responds within about a second; training runs in the background with progress, Stop, and a UI that never
  freezes; "train it again" finishes in **about 2 minutes or less** on the owner's PC.
- Seeded randomness everywhere, with the seed shown and editable.
- 13 pt default text, 10–24 pt slider, Dark and Light, full keyboard use, colour never the only carrier of meaning.
- No sound. Visual checks by `--render-png` and `--selftest` only; few exe launches.
- Bundled data must be public domain or openly licensed and listed in `THIRD-PARTY-NOTICES.txt`.
- Git is local only. During the Stage 2 build the controller commits each task on its part's branch without asking
  (owner, 2026-11-24: "Yes, you commit"). Merging into main, and anything touching GitHub, still waits for the owner.

## 4. Approaches

**Chosen (owner, 2026-11-24): neural labs ship already trained, with "train it again".** The image network, memory
network and diffusion toy are trained ahead of time by `tools/AIAnatomy.Pretrain` (as the Fables model was, D20) and
their weights bundled in `content/models/`. Each lab works the moment it opens; "train it again" builds a fresh
network from a seed and trains it live. This also lets the image network learn from larger 28×28 handwritten digits,
which recognise mouse drawings far better than the tiny 8×8 set.

**Not chosen: train live every time.** Nothing prepared in advance, but each neural lab opens empty and waits a
minute or two, and the image network would have to use 8×8 digits to train fast enough, which recognise drawings
poorly.

**Depth (owner, 2026-11-24): one lesson per member, two members get a second lesson.** Considered: one lesson each
(smaller, but convolution and denoising would be explained without being shown on their own).

**My calls, stated to the owner:**
- The family tree is drawn by the same MSAGL map machinery as the idea map, top-down as a tree.
- Stage 2 is built in **two parts**, each with its own plan, built and reviewed like Stage 1 (section 9).

## 5. What the user sees

Section 4 of the contents tree, "The family of AI", replaces the `family-coming` placeholder with these lessons, in
this order. The idea map's Radial view shows them around section 4 automatically; they are no longer greyed.

| # | Lesson | Part | Built in |
|---|---|---|---|
| 4.1 | The family tree | family | Part A |
| 4.2 | Rules: an expert system | rules | Part A |
| 4.3 | Search: looking ahead | search | Part A |
| 4.4 | Classic machine learning | classic | Part A |
| 4.5 | Learning by reward | reward | Part A |
| 4.6 | Image networks: filters | vision | Part B |
| 4.7 | Image networks: the whole network | vision | Part B |
| 4.8 | Memory networks | memory | Part B |
| 4.9 | Diffusion: from noise to a shape | diffusion | Part B |
| 4.10 | Diffusion: the steps | diffusion | Part B |

*My call:* after Part A, lessons 4.6–4.10 exist as five short "coming later" pages (title, one paragraph, no lab,
no questions), so the family tree and the map can link to each one; Part B replaces them. The single
`family-coming` placeholder goes in Part A, and the content tests' rules for coming-later pages are updated to match.

*My call:* the order puts everything without a neural network first, then the neural members, so each lesson builds on
the one before. Lessons not yet built show as "later" in the tree and map, as the placeholders do now.

*My call:* new part colours and icons: `family`, `rules`, `search`, `classic`, `reward`, `vision`, `memory`,
`diffusion`. The part-colour set has to stay colour-blind safe and meet the contrast rules in both themes, so some
parts may share a hue family across sections; the icon and label always tell them apart. *Builder's call:* the exact
colours and icons, checked by the existing contrast tests.

### 5.1 The family tree (4.1)

The lab is a top-down tree, laid out by MSAGL like the idea map:

```
                              Programs that act intelligently
                ┌──────────────────────┼───────────────────────────┐
         Written by a person     Learned from examples       Learned from reward
           ┌──────┴──────┐        ┌────────┴─────────┐                │
     Rules (4.2)   Search (4.3)  Classic (4.4)   Neural networks   Q-learning (4.5)
                                            ┌───────┬──┴─────┬──────────┐
                                      Images (4.6–7) Memory (4.8) Transformer  Diffusion (4.9–10)
                                                                 (Stage 1)
```

- Choosing a bubble shows a side card with **what it is, good at, bad at, a real-world example**, and Open lesson.
  The Transformer bubble opens the Architecture map lesson.
- Tree labels are this document's working wording; the lesson author may improve them (*builder's call*), keeping
  the three top branches.
- The tree's nodes and card text live in a content file (`content/family-tree.json`, *builder's call* on the exact
  name and shape), validated on load like `idea-map.json`.
- Same keyboard and chosen-bubble behaviour as the idea map.
- *My call:* the tree is this lesson's lab; it is not a third Radial/Loop/… switch on the idea map.

### 5.2 Rules: an expert system (4.2)

An animal-identifying rule system. It asks yes/no questions ("Does it have feathers?", "Does it eat meat?") and
reaches an answer.
- The rules are listed and **editable** (add, change, delete a rule; *builder's call* on the editing form).
- A **trace** screen shows which rules fired, in order, and why the answer was reached.
- It shows **brittleness**: an animal the rules never considered (for example a penguin, or a bat) gets a wrong or no
  answer, and the lesson invites the user to patch a rule and see another case break.
- Connects to Rules versus learning (1.1).
- *My call:* forward chaining over if-then rules with a small fact base, in the classic textbook style; about 15
  rules and 8 animals by default; Reset restores them.

### 5.3 Search: looking ahead (4.3)

Tic-tac-toe against minimax.
- The board: the user plays X or O (*builder's call* on how to choose); the computer replies.
- A **look-ahead tree** screen shows the computer's options for its next move with each one's minimax score
  (win / draw / loss), and the user can open a branch to see the replies beneath it, a few levels deep.
- A count of how many positions the search looked at.
- Accuracy test: minimax never loses, from any reachable position, and every score matches a hand-worked case.
- *My call:* plain minimax, no alpha-beta pruning in the lab (the lesson text mentions pruning as how real programs
  cut the count).

### 5.4 Classic machine learning (4.4)

The user places coloured points of two or three classes on a plot (click to add, choose the class; a few starter
sets).
- **Decision tree:** draws its axis-aligned splits as lines and shaded regions, with a **depth** slider; the tree's
  questions are listed ("x < 3.2?").
- **Nearest neighbour:** shades each region by the majority of the k nearest points, with a **k** slider.
- A switch between the two, or side by side (*builder's call*, by space).
- Shows overfitting: a deep tree or k = 1 carves round single odd points.
- *My call:* the tree is grown by the Gini impurity rule (CART style); the lesson states this.

### 5.5 Learning by reward (4.5)

A grid maze with editable walls, a start and a goal.
- A Q-learning agent explores. The user sees each square's **value** (shading plus the number on hover or selection)
  and the **arrow** the agent currently prefers.
- Sliders for **reward** (at the goal, and the cost of each step), **exploration rate** and **discount**; Run, Step,
  Stop, Reset.
- Shows the agent learning by trial and error, with no examples of right answers, only rewards.
- Accuracy test: on a small maze the learned values converge to the hand-computed values.

### 5.6 Image networks: filters (4.6)

One **3×3 filter** sliding over a digit.
- The user draws a digit (or picks one from the bundled set).
- Edit the filter's nine numbers, or choose a preset (vertical edge, horizontal edge, blur); see the **feature map**
  it produces, with the numbers on hover.
- Shows the learned filters of the shipped network's first layer beside the hand-made presets.
- Explains convolution as the same small set of weights reused at every position, the idea the transformer did not
  have.

### 5.7 Image networks: the whole network (4.7)

Draw a digit with the mouse; the shipped small convolutional network shows its **feature maps** layer by layer and the
**probabilities** for 0–9.
- Train it again: a fresh network trains live from a seed, with the loss chart and test accuracy.
- *Builder's call* within these limits: about two convolution layers, pooling, one or two fully connected layers;
  small enough that "train it again" meets the 2-minute limit.
- **Data:** 28×28 handwritten digits. *Builder's call, with a hard check:* the dataset's licence must allow bundling
  and be recorded in `THIRD-PARTY-NOTICES.txt`; the builder confirms it from the source before using it. If no 28×28
  set's licence can be confirmed, fall back to the UCI 8×8 optical digits (licence confirmed the same way) and say so
  to the owner. *My call:* bundle about 10,000 images (training subset plus a separate test subset), not the full set.
- The drawing is scaled and centred the way the training digits were, so the network sees what it was trained on.
- **Downloading the dataset needs the owner's OK at the time** (the plan has a gate step for it): before downloading,
  the builder names the file, its source and its size, and waits for a yes. If the owner says no, or no 28×28 set's
  licence can be confirmed, Part B carries on with the 8×8 set without stopping (the owner accepted this fallback
  when choosing "already trained"), and the lessons are written for whichever set is used.

### 5.8 Memory networks (4.8)

A character-level recurrent network (RNN) trained on the same Aesop's fables as the Fables model.
- Side by side with the shipped Fables transformer: **loss**, **training speed**, and **how far back it remembers**,
  shown as loss by position in the context or by a copy test (*builder's call*, as long as it is a real measurement).
- The hidden state screen: the RNN's memory vector after each character, with the numbers on hover.
- Text sampled from each model.
- *My call:* the transformer's training loss and speed are the **recorded** results of the shipped Fables model (D20),
  labelled as such; it is not retrained here. The RNN's are recorded for the shipped RNN and re-measured live when the
  user trains it again. "How far back it remembers" is measured **live** on both models, since it needs no training.
  Pretrain stores each shipped model's measured results in its model file's info.
- The lesson explains why attention replaced recurrence (long-range memory, training in parallel), with sources, and
  does not overstate it: RNNs are still used in places.
- *My call:* a plain (Elman) RNN with tanh; LSTMs are mentioned, not built.

### 5.9 Diffusion: from noise to a shape (4.9)

A toy that learns to turn random 2-D points into a shape (a spiral by default, a few other presets, or one the user
draws).
- *My call:* drawing stays in scope, as in the app design. A stroke is resampled to a fixed number of evenly spaced
  points (*builder's call* on the number); a drawing too short or too small to train on is refused with a message
  saying why, and the previous shape stays.
- Shows a cloud of random points moving, step by step, onto the shape, every step from the running model.
- Train it again on the chosen or drawn shape.
- The lesson says that image generators use the same method on pixels, or on compressed versions of them, with
  sources.

### 5.10 Diffusion: the steps (4.10)

Inside one denoising step.
- The **forward process**: noise added to the shape step by step, with the schedule shown.
- The model's job: predict the noise in a noisy point; the screen shows the prediction against the true noise for a
  chosen step.
- The reverse process one step at a time, with a step slider; changing the number of steps shows the quality cost of
  fewer steps.
- *My call:* DDPM-style training (Ho et al. 2020) with a small fully connected network on 2-D points.

## 6. Architecture

The layering stays: App → Presentation → Lessons → Models → Core.

- **Core** gains the operations the new members need, each gradient-checked: 2-D convolution and pooling (the image
  network), tanh (the memory network), and anything else the builder finds missing. *Builder's call:* convolution
  through an unfold-then-multiply form or directly, whichever reads more clearly.
- **Models** gains the family namespaces from the app design: `Rules`, `Search`, `ClassicMl`, `Reinforcement`,
  `Vision`, `Recurrent`, `Diffusion`. Each has unit tests against hand-worked small cases (app design 7.1).
- **Shipped models:** `tools/AIAnatomy.Pretrain` gains the image network, the RNN and the diffusion toy, trained in
  Release with a fixed seed; their files use the app's model format (D14) and the run times and results are logged in
  `docs/decisions.md` as D20 did.
- *My call:* the model file format (D14) may change for the new model kinds, with a version bump; Stage 1's shipped
  and user-saved files must still load, and a test proves it.
- **If "train it again" can't meet 2 minutes** (*my call*, in this order): shrink the network, then train on a smaller
  data subset or fewer steps, and only then accept a longer run, never past about 3 minutes, with progress and Stop
  as always. The shipped models may be larger than what "train it again" trains if needed, and the lab says so. Part
  B's plan starts with a speed check of convolution on the real engine before any lab work.
- **Presentation:** one lab view-model per lesson, using `LabViewModel`'s screen reporting (`ReportScreen`,
  `BeginRun`) and the screens rules in `docs/decisions.md`.
- **App:** views as DataTemplates; new reusable controls only where needed (the app design lists `DrawingPad` and
  `GridWorldView`; the builder may add a tree view for search). Every new view gets a `--render-png` id, and the
  `all-labs` self-test covers the new labs.
- **Content:** lesson files under `content/lessons/family/`, connections added to `content/connections.json` (for
  example Rules ↔ Rules versus learning, Memory network ↔ Attention, Learning by reward ↔ Learning, Image network
  filters ↔ Feed-forward), sources added to `content/sources.json`.

## 7. Correctness and accuracy

- Every algorithm is checked against hand-worked small cases: the expert system's trace on a fixed fact base,
  minimax never losing, the decision tree's splits and k-nearest-neighbour regions on a tiny set, Q-learning values on
  a 2×3 maze, a convolution worked by hand, RNN and diffusion loss falling in a short training run.
- Shipped neural models have measured results recorded (test accuracy for digits; validation loss for the RNN; a
  sample-quality measure for diffusion, *builder's call*), and Check yourself questions that quote them are
  `checked-by-code`.
- Lesson text follows the app design's 7.2: standard textbook material or a confirmed source. Starting sources to
  confirm and add: LeCun et al. 1998 (CNNs); Elman 1990 (simple recurrent networks); Hochreiter & Schmidhuber 1997
  (LSTM, mentioned only); Watkins 1989 and Sutton & Barto 2018 (Q-learning); Breiman et al. 1984 (CART); Cover & Hart
  1967 (nearest neighbour); Shortliffe 1976 (MYCIN, expert systems); Shannon 1950 (computer chess and minimax);
  Sohl-Dickstein et al. 2015 and Ho et al. 2020 (diffusion); Rombach et al. 2022 (latent diffusion, already in the
  list). The builder confirms each reference before citing it, as in Stage 1.
- Mismatches between models, research and what the labs show go in `docs/research/scientific-inconsistencies.md`.

## 8. Changes to the app's plan

- **The closing "how the family fits together in an assistant" map moves from Stage 2 to Stage 3.** Stage 3 is about
  what turns a model into an assistant, and the owner wants Stage 2 general. Stage 2 instead opens with the family tree,
  whose cards give each member's strengths and weaknesses.
- The `family-coming` placeholder is replaced by the real lessons as they are built; its sources move to the
  lessons that use them. The `beyond-coming` placeholder gains a line saying the "how they fit together" map will be
  there.

## 9. Delivery

- **Part A:** 4.1 to 4.5 (family tree, rules, search, classic machine learning, learning by reward). No neural
  networks and no shipped models, so it is the lighter half. Ends complete and usable; lessons 4.6–4.10 show as later.
- **Part B:** 4.6 to 4.10 (image network ×2, memory network, diffusion ×2), the new Core operations, the shipped
  models and the digits dataset.
- Each part gets its own implementation plan and is built and reviewed like Stage 1 and the UI rework, on its own
  branch. Task commits on that branch are delegated to the controller (section 3); the merge into main waits for the
  owner's OK.
- Decisions logged in `docs/decisions.md`; issues in `private/AIAnatomy-issues.xlsx`.

## 10. Left to the builder

- Wording of lessons, tree labels and cards, held to section 7.
- Exact layouts of each lab within the lesson page, the screens rules and the accessibility constraints.
- Default sizes and hyperparameters, chosen so the shipped models work well and "train it again" meets 2 minutes.
- Starter data for the expert system, the classic ML point sets and the default maze.
- Which 28×28 digits set, after its licence is confirmed (section 5.7).
