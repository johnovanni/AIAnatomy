---
id: output-sampling
section: parts
order: 8
title: Output and sampling
lab: SamplingLab
sources: anthropic2025-tracing, textbook
---
At the end of the model, the output layer gives every character in the vocabulary a score, called a **logit**. Higher means more likely, but a score on its own is not a probability. **Softmax** turns the scores into probabilities that add up to 100%, shown as bars. **The scores (logits)** at the bottom lists both side by side.

The model does not choose; something has to pick one character. **Sampling** picks at random, with each character's chance equal to its probability. Press **Sample 100 times** and compare **How often each was drawn** with the probabilities: close, but not equal, because the draws are random. The same **Seed** always gives the same draws.

Three settings change the probabilities before the draw:

- **Temperature** divides the scores before softmax. Below 1 the top choice becomes even more likely; above 1 the probability spreads out. At 0 the model always takes its top choice.
- **Top-k** keeps only the k most likely characters (0 turns it off).
- **Top-p** keeps the fewest most likely characters whose probabilities add up to at least p.

Whatever is left is scaled to add up to 100% again.

**Generate 200 characters** writes text one random draw at a time: each new character is added to the text, and the model runs again. This is a large part of why the same question can get different answers from an assistant; differences in what came before the question in the conversation play a part too.

"Confident" means a high probability, not a correct answer. The probability measures how text like the training text tended to continue, not what is true, so a confident-sounding choice can still be wrong.

Writing one token at a time does not mean a model only ever thinks one token ahead. Anthropic reports that Claude (in research on Claude 3.5 Haiku) writes text one token at a time, yet when writing rhyming verse it picks out possible rhyming words before writing the line that ends with one (Anthropic, 2025).

*Sources: Anthropic (2025); the rest is standard textbook material.*

## Formula

$$
p_i = \frac{e^{z_i / T}}{\sum_{j} e^{z_j / T}}
$$

- z with subscript i: the score (logit) of character i; p with subscript i is its probability.
- T: the temperature. At T = 1 this is plain softmax; as T goes towards 0 nearly all the probability goes to the top score.
- Top-k sets every p outside the k largest to 0. Top-p sorts the p from largest down and keeps the shortest run whose total reaches p. In this app temperature is applied first, then top-k, then top-p, and the survivors are rescaled to add up to 1 after each step.

## Brains and models

People do not give the same answer every time either, and psychology has long found that how sure people feel is an imperfect guide to whether they are right.

The causes differ. The model's variety is a deliberate dice roll after it has worked out exact probabilities; turn the temperature to 0 and it is gone. Human variety has many sources, most of them not understood. Whether the model "believes" its top choice in any sense is an open question.
