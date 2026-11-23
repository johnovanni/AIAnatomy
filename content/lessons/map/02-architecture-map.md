---
id: architecture-map
section: map
order: 2
title: The architecture map
lab: ArchitectureMapLab
sources: vaswani2017, radford2019, templeton2024, textbook
---
The diagram is the whole model on one page: text goes in at the top, and out at the bottom comes a probability for every possible next character. The real model runs underneath it: type in **Your text** and the bars at the bottom show its ten most likely next characters, recomputed as you type. Click an underlined box to open its lesson.

Follow the arrows from the top:

- **Tokens**: the text is split into pieces, here one per character, each with an id number.
- **Embeddings** and **Position**: each id becomes a list of numbers, and a second list that marks its place in the text is added to it.
- **Block**: the same kind of block is repeated several times. Inside, the **residual stream** carries each token's numbers through. **Multi-head attention** lets each token gather information from itself and earlier tokens, and the **feed-forward** layer works on each token by itself. Each reads a tidied (normalised) copy of the stream and adds its result back.
- **Final norm**, **Output** and **Probabilities**: the last numbers are tidied once more and turned into one score per possible next character, then into probabilities.

To write more than one character, the model picks one, adds it to the text, and runs again from the top.

This design is the transformer (Vaswani et al., 2017), in the form GPT-2 uses, with the normalising step at the start of each part (Radford et al., 2019). The Fables model that comes with the app has 2 blocks, 4 attention heads and 64 numbers per token, and reads at most 64 characters. Every box is exact arithmetic on 64-bit numbers; real models usually use 16- or 32-bit numbers, or fewer bits, to save memory and time.

What about Claude? Anthropic's published research on Claude 3 Sonnet studies its "residual stream", the same kind of running notes as here (Templeton et al., 2024). Beyond what Anthropic has published, Claude's exact design, its size and its training data are not public.

*Sources: Vaswani et al. (2017); Radford et al. (2019); Templeton et al. (2024); the rest is standard textbook material.*

## Brains and models

The brain, too, has parts with different jobs that pass signals to one another, so a diagram of boxes and arrows is a natural way to draw both.

The likeness is thin. The brain is full of feedback loops, never stops running, and keeps changing with experience; this model runs once from top to bottom for each new character, and its numbers do not change while you use it. Whether drawing either one as boxes tells us how it produces thought is an open question.
