---
id: step-through
section: whole
order: 2
title: Step through a prediction
lab: StepThroughLab
sources: vaswani2017, radford2019, anthropic2025-tracing, textbook
---
This lab follows one prediction through the whole model, with the real numbers at every step. Type some text, choose a **Token to follow**, and move through the steps with **Previous** and **Next** (or the arrow keys once the steps have focus). The step you are on is marked "current step".

The steps are the parts you have met, in the order the model runs them:

- **Tokens**: your text as ids; the followed token is marked.
- **Token embedding** and **Position embedding**: two rows looked up from learned tables, then **Token plus position**, added number by number.
- For each block: the **layer norm before attention**; **where each head looks** (the five largest weights of each head); the **attention output**; the **residual after attention**; then the **layer norm before feed-forward**, the ten largest **feed-forward** neurons, the **feed-forward output** and the **residual after feed-forward**.
- **Final layer norm**, then the **Logits**, one score for every token in the vocabulary, and the ten most likely next tokens as **Probabilities**.

Nothing is skipped and nothing is invented: each number on screen is read from a record the model keeps while it runs. That is the point of this lab. However clever its output looks, a transformer is this list of steps (Vaswani et al., 2017), in GPT-2's arrangement (Radford et al., 2019), done with exact arithmetic.

Two cautions. First, the followed token's prediction depends on all the tokens before it, through attention, so changing earlier text changes everything from the first attention step on. Second, seeing every number is not the same as understanding why the model works. Reading meaning into these numbers is the open research field of interpretability, and the [residual stream](lesson:residual-stream) lesson shows one of its simplest tools.

*Sources: Vaswani et al. (2017); Radford et al. (2019); Anthropic (2025) in the note below; the rest is standard textbook material.*

## Formula

The whole prediction for the token at position i, with L blocks:

$$
h_0 = E_{t_i} + P_i
$$

$$
h_{\ell}' = h_{\ell-1} + \mathrm{MultiHead}(\mathrm{LN}_{\ell,1}(h_{\ell-1}))
$$

$$
h_{\ell} = h_{\ell}' + \mathrm{FF}(\mathrm{LN}_{\ell,2}(h_{\ell}'))
$$

$$
z = \mathrm{LN}_f(h_L) \, W_{\mathrm{out}} + b_{\mathrm{out}}
$$

$$
p = \mathrm{softmax}(z)
$$

- E and P: the token and position tables; t with subscript i is the token at position i.
- h with subscript ℓ: the residual stream after block ℓ (ℓ from 1 to L); the prime marks the halfway point, after attention.
- LN: layer norms (two per block, and the final one, f); MultiHead and FF: the attention and feed-forward parts.
- z: the logits; p: the probabilities. Attention is the only step that mixes in other tokens.

## Brains and models

You cannot step through your own thoughts like this. Psychology experiments have long shown that people's explanations of their own choices can miss the real causes.

Models share the problem in their own way. Anthropic found that when Claude 3.5 Haiku adds numbers in its head it uses several paths in parallel, yet when asked how it did the sum it describes the carrying method taught in school (Anthropic, 2025). A model's account of its reasoning is more text that it writes, and it need not match what its numbers actually did.
