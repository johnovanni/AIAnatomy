---
id: embeddings
section: parts
order: 2
title: Embeddings
lab: EmbeddingsLab
sources: textbook
branches: The model before training=untrained; One token's numbers=token-numbers
idea: Each token id picks a row from a learned table: a list of numbers, 64 in the Fables model, called its embedding.
part: embeddings
---
A token id is only a label: id 40 is not "more" than id 20. So the first thing the model does with an id is look it up in a table with one row per token. The row it finds is the token's **embedding**, a list of numbers (a vector). In the Fables model each embedding has 64 numbers. Nobody chose them: they started random and were adjusted during training, like every other number in the model.

Training often gives similar vectors to tokens that play similar roles in the text. In large models trained on words, nearby vectors often belong to words with related meanings. Our model works on single characters, so look instead for groups such as letters, punctuation, and spaces and new lines (digits too, in a model trained on text that has them).

You cannot draw 64 directions on a flat screen, so the lab squashes the vectors onto a picture with two directions. It uses principal component analysis, which picks the two directions along which the vectors are most spread out. The percentage under each picture says how much of the spread survives. Tokens that look close in the picture may still be far apart in the full 64 numbers, so read the picture as a rough map.

The upper picture is the model you are using. The lower one is a model of the same shape before any training, with random numbers; its **Seed** box picks those random numbers. Each group of tokens has its own marker shape. Click a point, or use the arrow keys, to see that token's numbers under **One token's numbers**, trained and untrained side by side.

The embedding is only the starting point. Every later part of the model adds to these numbers, as the [residual stream](lesson:residual-stream) lesson shows.

*Sources: standard textbook material.*

## Try this
1. In the upper picture, look for groups: do the punctuation marks (triangles) sit near one another?
2. Compare the lower picture, the same model before training, when its numbers were still random.
3. Read the percentage under each picture: how much of the spread the flat picture keeps.
4. Click a point, such as e, and compare its numbers under One token's numbers, trained and before training.

## Check yourself
? Why does the model look up an embedding instead of using the token's id itself?
* An id is only a label: id 40 is not "more" than id 20
- The ids are too large for the model to add up quickly
- The ids change every time the model runs, so they cannot be used
why: An id only names a piece. The embedding gives each token a list of learned numbers that the later parts of the model can work with.
see: explanation

? In the shipped Fables model, how many numbers are in each token's embedding?
- 2, one per direction of the picture
* 64
- 69, one per token
why: Each row of the Fables model's embedding table has 64 numbers. The picture squashes them onto 2 directions, which is why it is only a rough map.
see: token-numbers
checked-by-code: Embeddings_FablesEmbeddingsHave64Numbers

? Two tokens sit close together in the flat picture. What can you conclude?
- Their full lists of numbers must be almost exactly the same
- They must be the same letter, once small and once capital
* Not much: in the full list they may be far apart
why: The picture keeps only part of the spread (the percentage under it), so closeness in the picture is only a rough guide.
see: explanation

## Formula

The embedding of token t is row t of the table E:

$$
x = E_{t}
$$

For the picture, the vectors are centred and their spread is summarised by the covariance matrix:

$$
C = \frac{1}{V} \sum_{i=1}^{V} (e_i - \bar{e})(e_i - \bar{e})^{\top}
$$

$$
\text{kept} = \frac{\lambda_1 + \lambda_2}{\sum_{j} \lambda_j}
$$

- E: the embedding table, V rows (one per token) by d columns (64 in the Fables model).
- e with subscript i: the embedding of token i; ē (e bar) is their average.
- λ (lambda) with subscripts: the spreads along C's main directions, largest first. The picture uses the first two directions, and "kept" is the percentage shown under each picture.

## Brains and models

Neuroscience textbooks describe many things the brain represents as patterns of activity spread across many neurons, rather than one neuron per thing. An embedding is a pattern of that general kind: a token is a whole list of numbers, not one switch.

The likeness ends there. An embedding is a fixed row of numbers, looked up the same way every time. Activity in the brain changes from moment to moment with context, attention and mood. Whether either kind of pattern has meaning in the sense that words have meaning for you is an open question.
