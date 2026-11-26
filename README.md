# AI Anatomy

AI Anatomy is a C#/WPF teaching app that explains how transformers work. Every number it shows comes from a small transformer engine written from scratch in this repository, with no machine-learning libraries, so you can watch real tokens, attention and training happen step by step.

## The app

The window has a **contents tree** on the left, numbered like a book (1 The big picture, 2 The parts, 3 The whole model, 4 The family of AI, 5 Beyond the model). Each lesson can have branches under it (for example Tokens has Byte-pair encoding and Count the letters). Each lesson shows a mark: a small dot (not opened yet), a tick (opened), or a heavy tick (you answered, or skipped, its Check yourself questions). Click a lesson, or move with the arrow keys and press Enter or Space; headings open and close the same way. Section 4, the family of AI, is complete, with ten lessons: 4.1 The family tree (where a program's behaviour comes from: a person, examples or reward), 4.2 Rules (an expert system whose if-then rules you can edit), 4.3 Search (tic-tac-toe against a computer that looks ahead), 4.4 Classic machine learning (a decision tree and nearest neighbours on points you place), 4.5 Learning by reward (Q-learning in a maze you can change), 4.6 Image networks: filters (a 3 by 3 filter sliding over small 8 by 8 handwritten digits, or your own drawing), 4.7 Image networks: the whole network (a small shipped network reading a digit layer by layer, which you can train again), 4.8 Memory networks (a recurrent network beside the Fables transformer: how far back each remembers, its memory after each character, and what each writes), 4.9 Diffusion: from noise to a shape (500 random points taken step by step onto a spiral, a circle, two moons, a letter S or a line you draw) and 4.10 Diffusion: the steps (adding noise, the network's guess of it, and taking it out again in fewer, bigger steps). Section 5 has one lesson on what turns a model into an assistant; it is a placeholder for now ("Coming later") and shows no marks. The Loop map shows only the family tree from section 4. Back and Next at the foot of a lesson follow the contents order, and Next skips placeholders.

**Idea map.** The Idea map page draws every idea in the contents and how they connect, in two layouts you switch between at the top: **Radial** (the default; the contents round a centre, grouped by section) and **Loop** (the path your text takes through the model, with the block drawn as a repeating frame). Your last choice is remembered. The map scrolls rather than shrinking. Lesson bubbles carry the same marks as the contents, in words too ("✓ opened", "✔ checked"; "later" for placeholders), and the bubbles never move when a mark changes. Pick a bubble to see its connections, then open its lesson (with the keyboard, press Enter on the chosen bubble again).

**A lesson page** has, from the top: the path (for example "2 The parts > 2.4"), a header band in the colour of the lesson's part of the transformer with the title and the one-sentence big idea, **Try this** (numbered steps), the lab and the explanation, **Check yourself**, **How this connects** (a chip for each connection; hover or focus shows its sentence; a chip opens the lesson or the map), and Back/Next.

**Labs and screens.** A lab shows its results on framed **screens**. A screen has a thin edge in the part's colour and briefly lights up when something you did changes it (or once when a run you started ends); it does not flash for live updates during a run, and nothing animates when Windows "Show animations" is off.

**Check yourself** asks two or three multiple-choice questions per lesson. You pick an answer, see at once whether it is right and why, and can skip the questions. No score is kept. Questions that depend on the app's models are checked by unit tests against the real answer.

**Progress** (which lessons you have opened and checked) is saved on your PC; Reset progress on the Settings page forgets it after you confirm. The app opens at the lesson you had open last.

**Text size and colours.** Settings has one slider for the text size, 10 to 24 pt (13 pt by default), and a Dark or Light theme (Dark by default for a new install). Titles are twice the body size, section headings 1.15 times, small text 0.85 times and never under 9 pt. From 18 pt the explanation goes under the lab instead of beside it. Every message is shown on screen; nothing depends on sound.

## Running the tests

The default run is fast and works in Debug; the slow induction-head training test is skipped there:

```
dotnet test AIAnatomy.slnx
```

To include the slow tests, run them in Release:

```
dotnet test AIAnatomy.slnx -c Release --filter Category=Slow
```

`tests/AIAnatomy.App.Tests` needs Windows: it checks that every lesson formula can be read and drawn by WpfMath, the formula renderer the app uses.
