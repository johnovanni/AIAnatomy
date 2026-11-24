---
id: position
section: parts
order: 3
title: Position
lab: PositionLab
sources: vaswani2017, haviv2022, openai-gpt2-release, textbook
branches: Predictions with and without position=with-without
idea: Each place in the text gets its own vector, added to the token's embedding, since attention alone has no direct sense of order.
part: position
---
"Dog bites man" and "man bites dog" use the same words. Only the order differs. Yet attention on its own has no idea of order: in the first block, if you swapped two earlier tokens, a later token would gather exactly the same mixture from them. So the model is told each token's place in a second way: every position (1, 2, 3, …) has its own vector of numbers, which is added to the token's embedding before the first block.

The lab shows two ways of making those vectors, as heat maps with one row per position and one column per number in the vector:

- **Learned** position vectors, which start random and are trained like everything else. GPT-2 works this way, with a table of position vectors in its code (OpenAI, 2019), and so does the Fables model, for its 64 positions.
- **The sine and cosine scheme** of the original transformer (Vaswani et al., 2017): fixed waves, fast in some columns and slow in others, so that each position gets its own pattern. Nothing is learned. Its authors found it worked about as well as learned vectors, and chose it because it might cope with texts longer than those seen in training.

Under **Predictions with and without position**, type some text. The left bars are the model's usual top five guesses; the right bars are the same model with the position vectors left out. A sentence below says how much probability moved.

Removing positions takes away the model's direct signal of order, but not every trace of it. Each token can look only at earlier tokens (the causal mask in the [attention](lesson:attention) lesson), so a later token sees more tokens than an earlier one. A model with two or more blocks can use that difference to work out roughly where it is. Haviv et al. (2022) found that language models trained with no position information at all still learn positional information, and they suggest that the causal mask is how.

*Sources: Vaswani et al. (2017); Haviv et al. (2022); OpenAI (2019); the rest is standard textbook material.*

## Try this
1. Compare the two heat maps: the left shows the model's own position vectors (learned, in the Fables model); the right shows fixed waves, fast in the left columns and slow in the right.
2. Under Predictions with and without position, compare the top 5 bars with position information and without it.
3. Read the sentence under the bars: how much of the probability moved to other characters.
4. Type your own text in Your text and see whether the top choice changes when positions are left out.

## Check yourself
? What kind of position vectors does the shipped Fables model use?
- The fixed sine and cosine waves of the original transformer
- None at all: it has no position information
* Learned ones, trained like everything else
why: Like GPT-2, the Fables model has a table of position vectors, one for each of its 64 positions. They started random and were adjusted in training.
see: lab
checked-by-code: Position_FablesUsesLearnedPositions

? In the shipped Fables model, with the lab's starting text "The fox and the ", what happens to the top choice when the position vectors are left out?
* It changes from F to t
- Nothing changes: F stays on top with the same share
- Every character becomes equally likely, as before training
why: Without positions the model loses its direct signal of where each token is, and about two thirds of the probability moves to other characters. With "The fox and the " in Your text, the sentence under the bars says how much moved.
see: with-without
checked-by-code: Position_FablesTopChoiceMovesFromFToT

? With the position vectors left out, is a model with two blocks completely blind to order?
- Yes: once the position vectors are gone, nothing tells it the order
* No: the causal mask still leaks some order
- Yes, but only for texts longer than its 64 positions
why: The causal mask lets each token look only at itself and earlier tokens, so later tokens see more. Haviv et al. (2022) found that models trained with no position information still learn positional information, and suggest the mask is how.
see: explanation

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
