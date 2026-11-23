---
id: embeddings
section: parts
order: 2
title: Embeddings
lab: EmbeddingsLab
sources: textbook
---
A token id is only a label: id 40 is not "more" than id 20. So the first thing the model does with an id is look it up in a table with one row per token. The row it finds is the token's **embedding**, a list of numbers (a vector). In the Fables model each embedding has 64 numbers. Nobody chose them: they started random and were adjusted during training, like every other number in the model.

Training often gives similar vectors to tokens that play similar roles in the text. In large models trained on words, nearby vectors often belong to words with related meanings. Our model works on single characters, so look instead for groups such as letters, digits, punctuation and spaces.

You cannot draw 64 directions on a flat screen, so the lab squashes the vectors onto a picture with two directions. It uses principal component analysis, which picks the two directions along which the vectors are most spread out. The percentage under each picture says how much of the spread survives. Tokens that look close in the picture may still be far apart in the full 64 numbers, so read the picture as a rough map.

The upper picture is the model you are using. The lower one is a model of the same shape before any training, with random numbers; its **Seed** box picks those random numbers. Each group of tokens has its own marker shape. Click a point, or use the arrow keys, to see that token's numbers under **One token's numbers**, trained and untrained side by side.

The embedding is only the starting point. Every later part of the model adds to these numbers, as the [residual stream](lesson:residual-stream) lesson shows.

*Sources: standard textbook material.*

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
