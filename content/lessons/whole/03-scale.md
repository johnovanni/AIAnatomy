---
id: scale
section: whole
order: 3
title: Scale
lab: ScaleLab
sources: radford2019, openai-gpt2-release, brown2020, azevedo2009, textbook
---
How big is the model you are using next to real ones? Size here means the number of **parameters**: the learnable numbers inside the model, every entry of every table and matrix. **Our model, part by part** lists where they are. In the Fables model there are 113,093, and more than half of them are in the feed-forward layers.

**Next to real models** draws the counts as bars on a logarithmic scale, where each faint line is ten times the one before. On an ordinary scale our model's bar would be far too small to see.

- **GPT-2 small**: 124 million. The GPT-2 paper's table lists 117 million (Radford et al., 2019), but OpenAI later said its original counts were wrong, and the released small model has 124 million (OpenAI, 2019). It has 12 blocks, 768 numbers per token and a context of 1,024 tokens.
- **GPT-3**: 175 billion (Brown et al., 2020).
- **Claude**: Anthropic has not published Claude's size, so no bar is drawn and no number is guessed.

GPT-2's design is nearly the same as ours (GPT-2 reuses its token table as its output layer), and GPT-3's is close to GPT-2's (it also uses a sparse kind of attention in some layers); mainly the sizes differ. Make every part wider and deeper, train on vastly more text, and the same arithmetic scales up. Bigger models trained on more text have generally predicted text better, but size is not the only thing that matters: the training text and the later training that turns a model into an assistant matter a great deal too.

Our model stores each number in 64 bits, so that the app's own checks of its arithmetic are precise. Large models usually store theirs in 16 or 32 bits, or fewer, because at billions of parameters every bit costs memory and time.

*Sources: Radford et al. (2019); OpenAI (2019); Brown et al. (2020); Azevedo et al. (2009) in the note below; the rest is standard textbook material.*

## Formula

Parameters of a model like ours (learned positions, feed-forward layers on), written over two lines:

$$
N = V d + T d + n_L (4 d^2 + 4 d) \; +
$$

$$
n_L (2 d F + F + d) + (2 n_L + 1) \cdot 2 d + (d V + V)
$$

- V: vocabulary size; d: width; T: context length; n with subscript L: number of blocks; F: feed-forward width.
- The terms in order: token table, position table, attention (queries, keys, values and output projection, with biases), feed-forward, layer norms (a gamma and a beta for each), and the output layer.
- The Fables model has V = 69, d = 64, T = 64, two blocks and F = 256, which gives 113,093. With GPT-2 small's shape (V = 50,257, d = 768, T = 1,024, 12 blocks, F = 3,072), and no separate output layer because GPT-2 reuses its token table for that, the same counting gives 124,439,808.

## Brains and models

The human brain has about 86 billion neurons (Azevedo et al., 2009), and its connections, the synapses, are commonly estimated at around a hundred trillion or more; such estimates are rough. Setting parameters beside synapses is tempting, but it breaks down: a synapse is a living structure, not one stored number, and a neuron is far more than a weighted sum. Neither count alone tells you what a system can do. That depends on how its parts are organised and what they learned.
