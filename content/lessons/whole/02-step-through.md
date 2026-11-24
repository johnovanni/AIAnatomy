---
id: step-through
section: whole
order: 2
title: Step through a prediction
lab: StepThroughLab
sources: vaswani2017, radford2019, anthropic2025-tracing, textbook
idea: A prediction is a fixed list of steps, from token ids to probabilities, each one plain arithmetic on the numbers before it.
part: whole
---
This lab follows one prediction through the whole model, with the real numbers at every step. Type some text, choose a **Token to follow**, and move through the steps with **Previous** and **Next** (or the arrow keys once the steps have focus). The step you are on is marked "current step".

The steps are the parts you have met, in the order the model runs them:

- **Tokens**: your text as ids; the followed token is marked.
- **Token embedding** and **Position embedding**: two rows looked up from learned tables, then **Token plus position**, added number by number.
- For each block: the **layer norm before attention**; **where each head looks** (the five largest weights of each head); the **attention output**; the **residual after attention**; then the **layer norm before feed-forward**, the ten largest **feed-forward** neurons, the **feed-forward output** and the **residual after feed-forward**.
- **Final layer norm**, then the **Logits**, one score for every token in the vocabulary, and the ten most likely next tokens as **Probabilities**.

Nothing is skipped and nothing is invented: each number on screen is read from a record the model keeps while it runs. That is the point of this lab. However clever its output looks, a transformer is this list of steps (Vaswani et al., 2017), in GPT-2's arrangement (Radford et al., 2019), done with ordinary arithmetic.

Two cautions. First, the followed token's prediction depends on all the tokens before it, through attention, so changing earlier text changes everything from the first attention step on. Second, seeing every number is not the same as understanding why the model works. Reading meaning into these numbers is the open research field of interpretability, and the [residual stream](lesson:residual-stream) lesson shows one of its simplest tools.

*Sources: Vaswani et al. (2017); Radford et al. (2019); Anthropic (2025) in the note below; the rest is standard textbook material.*

## Try this
1. Choose a Token to follow, then press Next and Previous to move through the steps. The line beside them says which step you are on.
2. Stop at a step called "where each head looks" and see which earlier tokens each head weighs most.
3. Go on to the last two steps, Logits and Probabilities, and compare the top characters in each.

## Check yourself
? Following the last token, you change the first character of Your text. After the tokens themselves, which is the first step whose numbers change?
- Token embedding: every number changes at once
* Block 1: where each head looks
- Probabilities: only the final guesses change
why: The two embeddings, their sum and the first layer norm use only the followed token and its place. Attention is the first step that reads other tokens, so from there on everything can change. Try it with Previous and Next.
see: explanation
checked-by-code: StepThrough_ChangingAnEarlierCharacterFirstChangesAttention

? What does the Logits step show?
- The probabilities, which always add up to 1
- The ten largest feed-forward neurons
* One score per token in the vocabulary
why: The output layer gives every token in the vocabulary a score, a logit. The last step, Probabilities, turns the scores into probabilities with softmax. Point at the Logits step to see every score.
see: explanation

? Anthropic (2025) asked Claude 3.5 Haiku how it had added two numbers. What did it describe?
* The carrying method taught in school
- The parallel paths its numbers really used
- Nothing: it said it could not tell
why: Anthropic found that Claude 3.5 Haiku adds using several paths in parallel, yet describes the school method when asked. A model's account of its reasoning is more text that it writes, and it need not match what its numbers did.
see: brains

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
