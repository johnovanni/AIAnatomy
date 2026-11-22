# Decisions

- **D1** Tensors store `double`, not `float`. Finite-difference gradient checks are then exact enough to trust, and the speed cost is small for scalar C#. Lesson text notes that real models use 16- or 32-bit numbers.
- **D2** A WPF-free `AIAnatomy.Presentation` project holds the view-models, so they can be unit-tested. This refines the spec's App -> Lessons chain to App -> Presentation -> Lessons.
- **D3** Pre-layer-norm blocks as in GPT-2 (`x + Attn(LN1(x))`, then `x + FF(LN2(x))`, then a final LN), with a separate (untied) output matrix, which is easier to explain as "the output layer".
- **D4** The BPE tokenizer works on characters, not bytes, so its pieces stay readable. Words are pre-split with the leading space attached (" the"), as in GPT-2.
- **D5** "Rules vs. learning" task: predict the next letter. A readable list of hand-written rules is compared with a bigram model (a table of next-letter scores) that learns from the fables in a few seconds.
- **D6** The Family and Beyond tabs exist in Stage 1, each showing a "Coming in a later stage" page that lists what it will contain.
- **D7** The training text is *Aesop's Fables* (George Fyler Townsend translation, Project Gutenberg eBook #21, public domain in the US). The Project Gutenberg header, footer and licence are removed, as their terms require when the trademark isn't used. Downloading it needs the owner's okay (asked at plan review). If it is refused, the builder writes 40 short original fables instead.
- **D8** Each forward pass works on one sequence (`[T, D]` tensors). A training batch sums the losses of B sequences. This keeps traces simple to show.
- **D9** `GradCheck.MaxRelativeError` runs on parameter copies of its inputs (so callers may pass plain tensors) and measures |analytic - numeric| / max(1, |analytic|, |numeric|), so near-zero gradients do not inflate the error. `Backward()` clears intermediate (non-leaf) gradients before propagating; leaves keep accumulating until `ZeroGrad()`.
