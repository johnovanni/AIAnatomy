---
id: position
section: parts
order: 3
title: Position
lab: PositionLab
sources: vaswani2017, haviv2022, openai-gpt2-release, textbook
branches: Predictions with and without position=with-without
---
"Dog bites man" and "man bites dog" use the same words. Only the order differs. Yet attention on its own has no idea of order: in the first block, if you swapped two earlier tokens, a later token would gather exactly the same mixture from them. So the model is told each token's place in a second way: every position (1, 2, 3, …) has its own vector of numbers, which is added to the token's embedding before the first block.

The lab shows two ways of making those vectors, as heat maps with one row per position and one column per number in the vector:

- **Learned** position vectors, which start random and are trained like everything else. GPT-2 works this way, with a table of position vectors in its code (OpenAI, 2019), and so does the Fables model, for its 64 positions.
- **The sine and cosine scheme** of the original transformer (Vaswani et al., 2017): fixed waves, fast in some columns and slow in others, so that each position gets its own pattern. Nothing is learned. Its authors found it worked about as well as learned vectors, and chose it because it might cope with texts longer than those seen in training.

Under **Predictions with and without position**, type some text. The left bars are the model's usual top five guesses; the right bars are the same model with the position vectors left out. A sentence below says how much probability moved.

Removing positions takes away the model's direct signal of order, but not every trace of it. Each token can look only at earlier tokens (the causal mask in the [attention](lesson:attention) lesson), so a later token sees more tokens than an earlier one. A model with two or more blocks can use that difference to work out roughly where it is. Haviv et al. (2022) found that language models trained with no position information at all still learn positional information, and they suggest that the causal mask is how.

*Sources: Vaswani et al. (2017); Haviv et al. (2022); OpenAI (2019); the rest is standard textbook material.*

## Formula

The vector that enters the first block is the token's embedding plus its position's vector:

$$
x = E_{t} + P_{\mathrm{pos}}
$$

The sine and cosine scheme fills P with:

$$
P_{(\mathrm{pos},\, 2i)} = \sin\left(\frac{\mathrm{pos}}{10000^{2i/d}}\right)
$$

$$
P_{(\mathrm{pos},\, 2i+1)} = \cos\left(\frac{\mathrm{pos}}{10000^{2i/d}}\right)
$$

- E with subscript t: the embedding of token t; P with subscript pos: the vector for its position.
- pos: the position, counted from 0 in the formula, so the lab's position 1 is pos = 0.
- i: which pair of numbers in the vector (0, 1, 2, ...); d: the width of the vector, 64 in the Fables model.

## Brains and models

When you read a sentence or watch someone sign, the words arrive one after another, so their order comes with the experience. The model receives all its tokens at once, so order has to be added as numbers, and nothing like time passes between one token and the next. How the brain keeps track of the order of words or events is still an open research question.
