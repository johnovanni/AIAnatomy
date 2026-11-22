# AIAnatomy — Design

Date: 2026-11-22
Status: draft for the owner's review

## 1. Intent and the why

AIAnatomy is a Windows desktop app (C#, WPF) that teaches how modern AI works, "from head to toe", through small
working examples that the owner can change and watch respond.

The owner works with Claude (an AI assistant built on a transformer language model) every day and wants to understand
how it does what it does. They gave three reasons, and the app serves all three:

1. **General knowledge:** to know how the machinery actually works.
2. **Cooperating better:** to understand behaviour they see in Claude (tokens, context, no memory between
   conversations, varied answers, confident mistakes), so they can work with it better.
3. **Becoming smarter themselves:** to compare the mechanism with human thinking, and see what might carry over.

What led them here: they grew up with computers that were strict and limited, and are surprised that an AI can code,
reason and relate to people. **The app's central idea answers that surprise:** underneath, a model is still that kind
of strict computer. Every step is exact arithmetic. What changed is that the behaviour was *learned from examples*,
not written as rules. The app opens with this contrast.

The owner wants a top-down structure: the high-level architecture first, then each key component and how it works,
then everything put together. After that come the wider family of AI types and how they fit together, and then
what turns a bare model into an assistant.

The owner values accuracy highly. They are very hard of hearing, so nothing may depend on sound. They care about
clean object-oriented design and architecture in the code itself, which they may read.

## 2. Goals, non-goals, anti-goals

**Goals**
- Real computation everywhere: each lab runs the actual algorithm on real numbers, and the user can see those numbers.
- Something to change in every lab: sliders, typed text, editable values, retraining, with immediate visible effect.
- Top-down teaching path: Map, then Parts, then Whole, then Family, then Beyond. Each step can also be visited directly.
- Plain-language explanations first. Real numbers are always visible, and formulas are one click away.
- Clean, readable C# with a clear layered architecture, so the code itself teaches.
- Honest about the limits of understanding, both of models and of brains.

**Non-goals**
- Training anything large or producing fluent English from our own model. Our models are tiny on purpose.
- Being a general machine-learning toolkit or a speed benchmark.
- Explaining Claude's private internals. The app covers only what is publicly known about models like Claude.
- Internet access at run time. Everything is bundled.

**Anti-goals (failure even if it "works")**
- **Fake animations:** a visual that looks like a computation but isn't one. Every animated value must come from the
  running code.
- **Over-claiming in either direction:** saying "the model understands exactly like you", or "it's just statistics,
  nothing more". Where science is unsure, the app says so.
- **Inventing facts about Claude:** for example its parameter count, its tokenizer or its training data. These are
  not public, and the app says so.
- **Black boxes:** no ML library whose math we can't show. All math is our own code.
- **Anything that needs sound.**

## 3. Constraints

- C#, WPF, .NET 10 (`net10.0-windows10.0.19041.0`, as in the owner's other apps). The SDK 10.0.404 is installed.
- MVVM with `CommunityToolkit.Mvvm`, as in ASLCoach. For typeset formulas, `WpfMath` (MIT licence, renders LaTeX
  maths in WPF; *my call*, builder confirms current package name, version and licence). It only draws formulas
  and does none of the AI math. No other third-party runtime libraries, apart from test-framework packages.
- Runs on the CPU of an ordinary Windows PC. Every lab responds within about a second. Training runs in the
  background with live progress and can be paused or stopped, and the UI never freezes.
- Deterministic: every random choice uses a seeded generator. The seed is shown and can be changed, so a result can
  be reproduced.
- Accessibility: large text by default (20 pt body, 28 pt headings, adjustable from 16 to 32 pt, as in ASLCoach),
  light and dark themes, full keyboard use, and colour never the only carrier of meaning (heat maps also show
  numbers on hover or selection).
- No sound anywhere.
- Visual checks without screen capture (because of the owner's antivirus): the app gets a diagnostic option
  `--render-png <view> <file>` that renders a named view to PNG with `RenderTargetBitmap`, and
  `--selftest <name>` for scripted checks. Keep launches few.
- Git: local repository only (`C:\repos\AIAnatomy`). Committing or creating a GitHub repository needs the owner's
  go-ahead.
- Bundled texts and datasets must be public domain or openly licensed, and listed in `THIRD-PARTY-NOTICES.txt`.

## 4. Approaches considered

1. **Transformer and all other models written from scratch in plain C#.** *Chosen by the owner.* Every number and
   every operation is our own code, so the app can show and expose anything. Trade-off: models stay tiny and training
   is slower than with professional tools, but at this size it takes seconds to a few minutes.
2. **A professional library (TorchSharp).** Faster and allows bigger models, but the math runs inside a native black
   box we can't open up and show. Rejected: it works against the reason for the app.
3. **Load a real pretrained model (GPT-2 small, 124 million parameters).** Real English, but it can only be inspected,
   not trained, and it needs a ~500 MB download. Not for now.
   *Possible later extension (not in scope):* GPT-2 has the same architecture as our engine, so its published weights
   could be loaded into our own from-scratch code. This would need the owner's approval for the download.

## 5. The app: what the user sees

### 5.1 Shell

```
┌ Map │ Parts │ Whole │ Family │ Beyond ─────────────── [Settings] ┐
│ Lessons      │  Interactive view                  │ Explanation   │
│ ▸ Tokens     │  (diagram, heat map, sliders,      │ plain words;  │
│ ▸ Embeddings │   real numbers, Run / Reset)       │ [Show formula]│
│ ▸ Attention  │                                    │ [Brains and   │
│ ...          │                                    │  models] note │
└──────────────┴────────────────────────────────────┴───────────────┘
```

- Top tabs for the five sections. The left list holds that section's lessons, the centre holds the interactive lab,
  and the right pane holds the explanation.
- Explanation pane: plain-language text, a "Show formula" expander, and where it is relevant a **"Brains and
  models"** note that compares the mechanism with human or animal thinking. It says where the comparison holds and
  where it breaks down, and marks open questions as open.
- Each lab has **Reset** (back to defaults) and, where relevant, a **seed** box.
- Settings: text size, theme. Kept in `%LOCALAPPDATA%\AIAnatomy\settings.json`.
- A **Sources** page lists the papers and references behind the lesson claims (see 7.3).

### 5.2 Stage 1 — Map, Parts, Whole (the transformer)

**Opening lesson, "Rules vs. learning"** (first lesson under Map): the same small task done two ways. One is a
hand-written rule program, and the other is a tiny model trained from examples, which the user watches go from
gibberish to something sensible. The message: both are strict arithmetic, but only one had its behaviour written by
a person.

**Map:** one diagram of the whole transformer, with text going in and a probability for each possible next token
coming out. The real trained model runs live under the diagram: the user types text and sees the prediction.
Clicking any box opens that part's lesson.

**Parts** (one lab each, in this order):

| Lesson | What the user can do |
|---|---|
| Tokens | Type text and see it cut into tokens with their ids. Compare character tokens (what our model uses) with sub-word tokens from a small byte-pair-encoding (BPE) tokenizer trained on the bundled text. Show why letter-counting is hard for a model that sees sub-word pieces. Note that real assistants use vocabularies of tens of thousands to ~200k tokens, and that Claude's exact tokenizer is not public. |
| Embeddings | Each token's vector of numbers, shown as a bar strip. A 2-D projection (PCA) of all token vectors from the trained model, where similar tokens group together. Compare before training (random) with after. |
| Position | Why order needs marking (attention alone ignores order). Show the learned position vectors and the classic sine/cosine scheme, and the effect of removing position information on predictions. |
| Attention (the focusing) | **Toy part:** set query and key vectors in 2-D by dragging, and watch dot products, softmax weights and the weighted sum of values update. **Real part:** a heat map of each head of the trained model on the user's own text, including the causal mask (no looking ahead). **Experiment, "induction heads":** train a tiny 2-layer attention model on sequences with repeats, and watch a head learn to look back to "what came after this token last time". This is a real published finding linked to learning from examples in the prompt (Olsson et al., 2022). |
| Multi-head attention | Several heads side by side on the same text, each attending to different things. Concatenation and output projection. |
| Feed-forward (the refining) | The two-layer network applied to each position. Pick a neuron and see the inputs that activate it most. Explain the research view that much stored knowledge lives here (Geva et al., 2021), marked as a research finding and not settled fact. |
| Running notes (residual stream) and layer norm | Show the residual vector growing layer by layer as each block adds to it. **"Logit lens":** decode the running notes after each layer into a next-token guess, so the user sees the guess refined layer by layer. Show what layer norm does to a vector. |
| Output and sampling | Scores (logits), then softmax probabilities as bars. Temperature, top-k and top-p controls. Sample many times and watch the frequencies. This explains why the same question can get different answers, and why a confident-sounding pick can still be wrong. |
| Learning | (a) One neuron learning AND/OR, with its decision line moving. (b) A loss surface in 2-D with gradient descent walking downhill: change the learning rate and watch it creep, converge or overshoot. (c) Backpropagation on a tiny expression graph, with the value and gradient shown on every node. (d) Cross-entropy loss for next-token prediction. |

**Whole:**
- **Train:** train the tiny transformer on a bundled public-domain text, or on text the user pastes. Live loss curve,
  sample output every N steps, and pause/stop/resume. Hyperparameters (layers, heads, width, context length,
  learning rate, steps) can be edited, with the parameter count shown and broken down by part.
- **Ready model:** a model pre-trained by us at build time ships with the app, so the Map and Parts labs work
  immediately without waiting.
- **Step through one prediction:** a pipeline view that follows a single prediction through every part (tokens,
  embedding plus position, each block's attention and feed-forward, the residual updates, the final probabilities)
  with the real numbers at every stage.
- **Which model the labs use** (*my call*): the app keeps a small **model library**: the shipped model plus any the
  user trains and saves, each with a name, its settings, its training text and its loss. These are saved in
  `%LOCALAPPDATA%\AIAnatomy\models\` and survive restarts. One model is the **active model**, which every Map and
  Parts lab reads. A picker in the shell shows the active model and lets the user change it. Training always works
  on its own copy, so the labs never see half-changed weights. While training runs, the other labs keep using the
  active model. When the user pauses or finishes, they can save the trained model and make it active. The labs read
  the model's shape (vocabulary, layers, heads, width) and adapt to it. The shipped model can't be deleted, so the
  labs always have something to show.
- **Scale:** our model's parameter count beside publicly known figures (for example GPT-2 124M, GPT-3 175B), with
  the note that Claude's size has not been published.

### 5.3 Stage 2 — The Family of AI

A family tree screen (rules and search, then learning from examples: classic ML, neural networks, learning by
reward). Each branch opens a working lab:

| Branch | Lab |
|---|---|
| Rules: expert system | An animal-identifying rule system ("Does it have feathers?") whose rules can be read, edited and traced. It shows the strengths and brittleness of hand-written rules. |
| Search | Tic-tac-toe played against minimax, with its look-ahead tree shown and scored. |
| Classic ML | The user places coloured points on a plot. A decision tree draws its splits (depth slider), and nearest-neighbour shows its regions (k slider). |
| Image networks (CNN) | Draw a digit with the mouse. A small convolutional network shows its filters, its feature maps per layer and its probabilities. Trained on a bundled openly licensed digits dataset (builder to choose and check the licence, for example the UCI 8×8 digits, CC BY 4.0), with an option to add the user's own drawings. |
| Memory networks (RNN) | A character RNN trained on the same text as the transformer, compared side by side for loss, speed and how far back it remembers. Explains why attention replaced it. |
| Learning by reward | A grid maze with editable walls. A Q-learning agent explores, and the user sees the value of each square and the learned arrows. Reward, exploration rate and discount can be changed. |
| Diffusion | A toy that learns to turn random noise into a 2-D shape (spiral, or one the user draws), with each denoising step visible. Explains that image generators use the same method on pixels. |

**How they fit together:** a diagram mapping an assistant like Claude onto the family: a transformer at its core,
shaped in training partly by learning from reward (human-feedback and Constitutional AI methods), seeing images by
cutting them into patches that act like tokens, and searching by meaning with embeddings.

### 5.4 Stage 3 — Beyond the model (what makes an assistant)

Shown as "what is publicly known about models like Claude", never as inside knowledge.
- Training stages: pretraining on text, then fine-tuning on conversations, then learning from feedback (RLHF,
  Constitutional AI as Anthropic has published it).
- Context window: our model visibly cannot use text beyond its context length. Assistants have the same limit,
  much larger.
- No memory between conversations: chatting does not change the weights. Show the weights unchanged before and
  after a "chat" with our model. Memory features work by putting saved text back into the context.
- Thinking as extra text: intermediate steps the model writes for itself become part of its context, and can help.
- Tools: the model writes a special request, a program runs it (for example a calculator), and the result goes back
  into the context.

Stage 3's labs are only sketched here. Before building it, there is a short design refresh with the owner.

## 6. Architecture

### 6.1 Projects

```
AIAnatomy.slnx
src/
  AIAnatomy.Core        engine: tensors, automatic differentiation, randomness, optimisers, losses
  AIAnatomy.Models      transformer parts and the other model families, all built on Core
  AIAnatomy.Lessons     lesson catalogue and lesson text (loaded from content files), sources list
  AIAnatomy.App         WPF shell, views, view-models, custom visual controls
content/                lesson text (Markdown or similar), bundled texts and datasets, pre-trained model files
tests/
  AIAnatomy.Core.Tests, AIAnatomy.Models.Tests
tools/
  AIAnatomy.Pretrain    console tool that produces the bundled pre-trained models
docs/                   this design, decisions.md, plans
```

Dependencies point one way only: App → Lessons → Models → Core. Core and Models have no WPF dependency.

### 6.2 Core

- `Tensor`: a small n-dimensional array (row-major `float[]` plus shape) with operations: matmul, add, multiply,
  softmax, layer norm, GELU/ReLU, reshape, transpose, masking and indexing.
- **Automatic differentiation:** each operation records how to pass gradients back (a tape or graph of nodes).
  `Backward()` fills `Grad` for every parameter. The node graph is inspectable, which the Learning lab uses directly.
- `Parameter`, `IOptimizer` (`Sgd`, `Adam`), losses (`CrossEntropy`, `MeanSquaredError`), `SeededRandom`.
- Number type: `float`, as real models use. *Builder's call:* use `double` instead if gradient checks prove too
  imprecise.

### 6.3 Models

- Transformer parts as classes behind small interfaces: `ITokenizer` (`CharTokenizer`, `BpeTokenizer`),
  `TokenEmbedding`, `IPositionEncoding` (`LearnedPositions`, `SinusoidalPositions`), `AttentionHead`,
  `MultiHeadAttention`, `FeedForward`, `LayerNorm`, `TransformerBlock`, `Transformer`, `ISampler`
  (`TemperatureSampler` with top-k and top-p), `Trainer`.
- **Inspection by design:** a forward pass can return a `ForwardTrace` holding every intermediate result (per-layer
  residuals, attention weights per head, feed-forward activations, logits). The labs read the trace and never
  recompute the math themselves. This is how "no fake animation" is guaranteed.
- **Interventions:** the labs can change things the model uses (disable a head, remove position information,
  edit a value) through explicit options passed to the forward pass, never by reaching into private state.
- Model save and load in a simple documented format (JSON header plus binary weights).
- Stage 2 families as their own namespaces on the same Core: `Rules`, `Search`, `ClassicMl`, `Vision` (CNN),
  `Recurrent` (RNN), `Reinforcement` (Q-learning), `Diffusion`.

### 6.4 Lessons

- A `Lesson` has an id, section, title, explanation text, optional formula, optional "Brains and models" note,
  source references, and the id of the lab view it uses.
- Text is kept in content files, not in code, so wording can be corrected without touching the program.
  *My call:* the files sit loose in a `content` folder beside the exe (copied there by the build), so a wording fix
  needs no rebuild. The format is a small Markdown subset (headings, paragraphs, bold and italic, bullet lists,
  links to other lessons, and `$$…$$` formula blocks). The app reads it with its own small reader. Formulas are
  written in LaTeX and drawn by `WpfMath` as properly typeset maths.

### 6.5 App

- MVVM: one view-model per lab, with views as `DataTemplate`s chosen by view-model type.
- Reusable visual controls: `HeatMap`, `VectorStrip`, `ScatterPlot`, `ProbabilityBars`, `LineChart` (loss),
  `NetworkDiagram`, `PipelineView`, `DrawingPad`, `GridWorldView`.
- Long work (training) runs on a background task with progress reports and cancellation. The UI thread only draws.
- Diagnostics: `--render-png <view> <file>` and `--selftest <name>` (see Constraints).

## 7. Correctness and accuracy

### 7.1 Tests (in the unit tests, run with `dotnet test`)

- **Gradient checks:** every differentiable operation and every model part compared with numerical
  (finite-difference) gradients.
- Invariants: attention weights in each row sum to 1, the causal mask blocks every future position, softmax is
  stable for large inputs, and layer norm output has mean 0 and variance 1 before scale and shift.
- Determinism: the same seed gives identical results.
- Learning sanity: the tiny transformer overfits a short text, and the loss falls below a set threshold. The induction
  experiment produces a head with a clear induction pattern, measured by a score.
- Each Stage 2 algorithm checked against hand-worked small cases (for example minimax never loses at tic-tac-toe).

### 7.2 Lesson-text accuracy

- Every factual claim is standard textbook material or tied to a source in the Sources list.
- Claims about Claude are limited to what Anthropic has published. Unknowns are stated as unknown.
- "Brains and models" notes mark open questions as open (consciousness, animal awareness, whether models
  "understand").

### 7.3 Sources (starting list)

Vaswani et al. 2017 (attention, the transformer); Radford et al. 2019 (GPT-2); Brown et al. 2020 (GPT-3);
Olsson et al. 2022 (induction heads); Geva et al. 2021 (feed-forward layers as memories); nostalgebraist 2020
(logit lens); Sennrich et al. 2016 (BPE); Ouyang et al. 2022 (RLHF for instructions); Bai et al. 2022 (Constitutional
AI); Ho et al. 2020 (diffusion); LeCun et al. 1998 (CNNs); Watkins 1989 / Sutton & Barto (Q-learning).
The builder confirms each reference before citing it.

## 8. Delivery

- Three stages, each complete and usable before the next starts. Each stage gets its own implementation plan.
  1. Shell, Core engine, Map, Parts, Whole.
  2. Family of AI.
  3. Beyond the model (after a short design refresh).
- Decisions made during the build are logged in `docs/decisions.md` with reasons.
- Issues are tracked in `private/AIAnatomy-issues.xlsx` (Severity and Priority), excluded from git through
  `.git/info/exclude`.

## 9. Left to the builder

- Exact visual style, colours and layout details within the shell above.
- Which bundled public-domain training text to use (short, varied, plain English, for example a set of Aesop's fables
  from Project Gutenberg), with its licence noted.
- Default hyperparameters, chosen so that training on the default text gives visible progress within about
  2 minutes, and so the shipped pre-trained model produces recognisable word fragments.
- Performance work (parallel loops, spans) as needed to meet the responsiveness constraint.
- Lesson wording, held to 7.2.
