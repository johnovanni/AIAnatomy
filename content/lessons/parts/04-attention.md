---
id: attention
section: parts
order: 4
title: Attention
lab: AttentionLab
sources: vaswani2017, olsson2022, textbook
branches: Attention by hand=toy; Attention in the model you are using=heads; Can attention learn to copy?=induction
idea: Each token gathers information from itself and the tokens before it, weighted by how well its query matches their keys.
part: attention
---
Attention is how a token gathers information from itself and the tokens before it. Each token makes three vectors: a **query** (what am I looking for?), a **key** (what do I offer?) and a **value** (what I hand over if chosen).

**Part 1** does this by hand with two numbers per vector. Drag Q and the keys, or type their numbers. Step 1 multiplies the query with each key (a dot product): a bigger result means a better match. Step 2 scales the scores down. Step 3, softmax, turns them into weights that add up to 1. Step 4 adds up the values, each times its weight: that is the answer.

**Part 2** shows the real thing in the model you are using. Pick a **Layer** and a **Head**. Each row of the heat map is one token looking back; its squares add up to 1 (100%). Hatched squares are blocked: a token may not look at later tokens, so it can never peek at the token it must predict. This is the **causal mask**. Each head reads a token's whole vector and makes its own query, key and value from it (Vaswani et al., 2017).

**Part 3** repeats a published experiment (Olsson et al., 2022). A tiny two-layer model sees letter sequences in which a stretch appears twice. To continue the second copy, it must find the first copy and look at the letter after it. Olsson and colleagues found two heads doing this together: a **previous-token head** in an earlier layer and an **induction head** in a later one. Use **Show trained demo**, or **Train it yourself** and watch the scores. The loss often drops sharply part-way through training, similar to the sudden change they reported, but not identical and not at a set step.

Our first version used repeats at a fixed distance, and the model found a shortcut: it looked back a fixed number of places, and no induction head formed. The sequences now vary, so only matching by content works.

*Sources: Vaswani et al. (2017); Olsson et al. (2022); the rest is standard textbook material.*

## Try this
1. In part 1 (Attention by hand), drag Q towards K2, or type its numbers, and watch K2's weight grow in Step 3.
2. Check that the weights in Step 3 add up to 1, wherever you put the points.
3. In part 2, pick a Layer and a Head. Each row of the heat map is one token looking back; hatched squares are later tokens it may not look at.
4. In part 3, press Show trained demo and find the head with the highest induction score.

## Check yourself
? Why are some squares of the heat map in part 2 hatched?
- Those tokens are spaces, which attention always skips
* A token may not look at later tokens
- Those weights were too small to show on the colour scale
why: This is the causal mask. Blocking every later token means a token can never peek at the token it must predict; those weights are exactly 0. Pick another Layer and Head: the hatched squares stay in the same places.
see: heads

? In the toy at its starting numbers, which key gets the most weight?
- K2, because it is the highest point on the plot
- All three keys get the same weight, a third each
* K1: Q · K1 is the largest dot product
why: Q · K1 = 1, Q · K2 = 0.5 and Q · K3 = −0.75. Softmax gives the largest score the largest weight, about 50%. Reset the lab to get the starting numbers back, then read Step 1 and Step 3.
see: toy
checked-by-code: Attention_ToyStartGivesK1TheMostWeight

? An induction head finds an earlier copy and looks at the letter after it. What does it need from an earlier layer?
* A previous-token head
- A head that always looks only at the first letter of the text
- Nothing: an induction head does all of the work on its own
why: Olsson et al. (2022) found the two working together: the previous-token head copies "what came before me" into each token, and the induction head uses that to find the earlier copy.
see: induction

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
