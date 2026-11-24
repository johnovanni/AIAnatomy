---
id: architecture-map
section: map
order: 2
title: The architecture map
lab: ArchitectureMapLab
sources: vaswani2017, radford2019, templeton2024, textbook
branches: The ten most likely next characters=next-characters
idea: Text becomes tokens, flows through the model's blocks, and comes out as a probability for every possible next character.
part: whole
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

## Try this
1. Follow the arrows on the diagram from Text at the top to Probabilities at the bottom.
2. Read the bars under The ten most likely next characters for the starting text "The fox and the ".
3. Type more in Your text, one letter at a time, and watch the bars change as you type.
4. Click an underlined box, such as Attention, to open its lesson.

## Check yourself
? In the shipped Fables model, how many blocks does the text pass through?
- 1
* 2
- 64
why: The Fables model has 2 blocks (Block × 2 on the diagram). Each has multi-head attention and a feed-forward layer, which read the residual stream and add their results back.
see: explanation
checked-by-code: ArchitectureMap_FablesModelHasTwoBlocks

? In the shipped Fables model, with the lab's starting text "The fox and the ", what is the most likely next character?
* Capital F
- Small c, as in "crow"
- A space, to end the word
why: In the fables the animals are named with capitals, as in "the Lion" and "the Mouse", so after "and the " a capital letter is far more common than a small one. Nine of the model's top ten are capitals.
see: next-characters
checked-by-code: ArchitectureMap_FablesTopChoiceIsCapitalF

? To write a sentence, what does the model do once it has picked a character?
- Writes the rest of the sentence in the same run, all at once
- Starts again from an empty text and writes a new character
* Adds it to the text and runs again from the top
why: Each run gives probabilities for the next character only. To write more, the chosen character is added to the text and the whole model runs again.
see: explanation

## Brains and models

The brain, too, has parts with different jobs that pass signals to one another, so a diagram of boxes and arrows is a natural way to draw both.

The likeness is thin. The brain is full of feedback loops, never stops running, and keeps changing with experience; this model runs once from top to bottom for each new character, and its numbers do not change while you use it. Whether drawing either one as boxes tells us how it produces thought is an open question.
