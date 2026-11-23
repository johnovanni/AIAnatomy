---
id: residual-stream
section: parts
order: 7
title: The residual stream
lab: ResidualLab
sources: radford2019, nostalgebraist2020, textbook
---
Each token has one vector that travels through the whole model, like a page of running notes. It starts as the token's embedding plus its position vector. Then every attention layer and every feed-forward layer reads the notes and **adds** its result to them. Nothing ever replaces the notes; each part only writes more on top. This running total is called the **residual stream**.

Before reading the notes, each part tidies a copy of them with **layer norm**, and it is the tidied copy that the part works on; the notes themselves stay as they were. One last layer norm comes after the final block, before the output layer. Putting the norm at the start of each part, rather than after it, is the arrangement GPT-2 uses (Radford et al., 2019).

Choose a **Token to follow**. **How long the vector is after each stage** shows how the length of the notes changes as each block adds to them.

**What would the model guess at each stage?** reads the notes part-way through, as if the model stopped there: the notes go through the final layer norm and the output layer, and come out as five best guesses for the next character. This is the **logit lens** (nostalgebraist, 2020). Often you can watch a guess form and sharpen from stage to stage. Read the early rows as rough: the final layer norm and the output layer were trained only on the last block's output, so earlier notes are not quite in the form they expect. The last row is exactly the model's own prediction.

**Layer norm: tidying a vector** shows one norm at work on the token you chose: before, after moving the numbers to an average of 0 and a spread of 1, and after the learned scale (gamma) and shift (beta) that the model actually uses.

*Sources: Radford et al. (2019); nostalgebraist (2020); the rest is standard textbook material.*

## Formula

One block, with the norm first:

$$
h' = h + \mathrm{Attn}(\mathrm{LN}_1(h))
$$

$$
h'' = h' + \mathrm{FF}(\mathrm{LN}_2(h'))
$$

$$
\mathrm{LN}(x)_i = \gamma_i \, \frac{x_i - \mu}{\sqrt{\sigma^2 + \epsilon}} + \beta_i
$$

The logit lens after block ℓ:

$$
\mathrm{guess}_{\ell} = \mathrm{softmax}(\mathrm{LN}_f(h_{\ell}) \, W_{\mathrm{out}} + b_{\mathrm{out}})
$$

- h: the residual stream entering the block; h′ after attention; h″ after the feed-forward layer, which is the block's output.
- μ (mu) and σ² (sigma squared): the average and the variance of the vector's own numbers; ε (epsilon) is 0.00001, so there is never a division by zero.
- γ (gamma) and β (beta): the learned scale and shift, one pair for each place in the vector.
- LN with subscript f: the final layer norm; W and b with subscript out: the output layer. h with subscript ℓ is the stream after block ℓ.

## Brains and models

The logit lens resembles decoding studies in neuroscience, which try to read out from recorded brain activity what a person is seeing or about to do. Both share a caution: being able to read something out of a signal does not prove that the system itself uses it that way.

Here the reading is unusually easy, because the model's own output layer can be reused as the decoder. The brain offers no such ready-made readout.
