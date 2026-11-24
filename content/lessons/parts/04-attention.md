---
id: attention
section: parts
order: 4
title: Attention
lab: AttentionLab
sources: vaswani2017, olsson2022, textbook
branches: Attention by hand=toy; Attention in the model you are using=heads; Can attention learn to copy?=induction
---
Attention is how a token gathers information from itself and the tokens before it. Each token makes three vectors: a **query** (what am I looking for?), a **key** (what do I offer?) and a **value** (what I hand over if chosen).

**Part 1** does this by hand with two numbers per vector. Drag Q and the keys, or type their numbers. Step 1 multiplies the query with each key (a dot product): a bigger result means a better match. Step 2 scales the scores down. Step 3, softmax, turns them into weights that add up to 1. Step 4 adds up the values, each times its weight: that is the answer.

**Part 2** shows the real thing in the model you are using. Pick a **Layer** and a **Head**. Each row of the heat map is one token looking back; its squares add up to 1. Hatched squares are blocked: a token may not look at later tokens, so it can never peek at the token it must predict. This is the **causal mask**. Each head reads a token's whole vector and makes its own query, key and value from it (Vaswani et al., 2017).

**Part 3** repeats a published experiment (Olsson et al., 2022). A tiny two-layer model sees letter sequences in which a stretch appears twice. To continue the second copy, it must find the first copy and look at the letter after it. Olsson and colleagues found two heads doing this together: a **previous-token head** in an earlier layer and an **induction head** in a later one. Use **Show trained demo**, or **Train it yourself** and watch the scores. The loss often drops sharply part-way through training, similar to the sudden change they reported, but not identical and not at a set step.

Our first version used repeats at a fixed distance, and the model found a shortcut: it looked back a fixed number of places, and no induction head formed. The sequences now vary, so only matching by content works.

*Sources: Vaswani et al. (2017); Olsson et al. (2022); the rest is standard textbook material.*

## Formula

$$
\mathrm{Attention}(Q, K, V) = \mathrm{softmax}\left(\frac{Q K^{\top}}{\sqrt{d_k}} + M\right) V
$$

$$
M_{ij} = 0 \;\; \mathrm{if} \; j \le i, \;\;\;\; M_{ij} = -\infty \;\; \mathrm{if} \; j > i
$$

$$
Q = X W^{Q} + b^{Q}, \;\; K = X W^{K} + b^{K}, \;\; V = X W^{V} + b^{V}
$$

$$
\mathrm{softmax}(s)_j = \frac{e^{s_j}}{\sum_{k} e^{s_k}}
$$

- X: the (normalised) vectors of all the tokens, one row each; each head has its own learned W and b.
- Q, K, V: the queries, keys and values, one row per token; Kᵀ is K turned on its side.
- d with subscript k: the number of numbers in each key. Dividing by its square root keeps the scores from growing with the size of the vectors.
- M: the causal mask. Row i is the token that is looking and column j the token it looks at; −∞ becomes a weight of exactly 0.

## Brains and models

People attend too: you look at one face in a crowd and the rest fades. The model's attention is different. Its weights must add up to 1, a kind of fixed budget, but it is a plain weighted average, worked out afresh for every token, in every head, all at once, with nothing like effort or tiredness.

The shortcut in our first experiment has a human echo. A pupil who notices that the answer is always "c" can score well without learning the subject. Models, like people, learn whatever works on the examples they are given, which is not always what their teachers intended.
