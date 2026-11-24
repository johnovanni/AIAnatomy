---
id: feed-forward
section: parts
order: 6
title: Feed-forward layers
lab: FeedForwardLab
sources: geva2021, anthropic2024-mapping, textbook
branches: One neuron along your text=along-text; Where this neuron fires most=fires-most
idea: After attention, a feed-forward layer works on each token's vector by itself, widening it into many neurons and narrowing it back.
part: feedforward
---
Attention moves information between tokens. The **feed-forward layer** then works on each token by itself. It is one small network, the same one for every token in the text: each token's vector goes through it separately, and no token sees another here.

The network has two layers. The first widens the vector into many **neurons**: 256 in each layer of the Fables model, four times the width of 64. Each neuron adds up the vector's numbers with its own learned weights and then applies a smooth bend called GELU, which lets large positive sums through and turns large negative sums into nearly zero (a small negative sum gives a small negative result, never below about −0.17). The second layer combines the neurons back into a vector of 64 numbers, which is added to the [residual stream](lesson:residual-stream).

In the lab, choose a **Layer** and a **Neuron**. **Along your text** shows how strongly that neuron fires at each character you type. **Where this neuron fires most** searches the first 20,000 characters of the fables and lists the ten places where it fired hardest, with the text just before and the character it had just read in a box. Try a few neurons and ask what, if anything, their places have in common.

Often you will find no single tidy meaning. Single neurons are often *polysemantic*, responding to several unrelated things. Anthropic, describing models like Claude, writes that each concept is represented across many neurons, and each neuron is involved in representing many concepts (Anthropic, 2024b).

What do these layers do in a large model? Geva et al. (2021) found that feed-forward layers act partly like key-value memories: the first layer's weights detect patterns in the text, and the second layer's weights push the prediction towards tokens that tend to follow those patterns. This is a research finding about how these layers can work, not settled fact about every model.

*Sources: Geva et al. (2021); Anthropic (2024b); the rest is standard textbook material.*

## Try this
1. Choose a Layer and a Neuron, then read the bars under Along your text: one bar for each character of Your text.
2. Under Where this neuron fires most, read the ten places and the boxed character at the end of each. What, if anything, do they share?
3. Pick a few other numbers under Neuron and compare their ten places.
4. Type one of those places into Your text and see whether the bar for its last character is tall there too.

## Check yourself
? In the feed-forward layer, what does one token's vector see of the other tokens?
- All of them, weighted by attention, as in the step before
* None of them: each token goes through on its own
- Only the token just before it, as a previous-token head does
why: Attention is the only part that mixes tokens. The feed-forward layer is one small network, the same for every token, and each token's vector goes through it separately.
see: explanation

? How many neurons does each feed-forward layer of the shipped Fables model have?
- 64, one for each number in the vector
- 69, one for each character it knows
* 256, four times the width of 64
why: The first layer widens each token's 64 numbers into 256 neurons; the second layer combines them back into 64. The Neuron list in the lab runs from 1 to 256.
see: explanation
checked-by-code: FeedForward_FablesLayersHave256Neurons

? Under Where this neuron fires most, a neuron's ten places have nothing obvious in common. What is the usual explanation?
* Many neurons respond to several unrelated things
- The neuron is broken and does nothing useful at all
- The search failed, since every neuron has one meaning
why: Single neurons are often polysemantic. Anthropic writes that each concept is represented across many neurons, and each neuron helps represent many concepts (Anthropic, 2024b). Try a few numbers under Neuron and compare their places.
see: fires-most

## Formula

$$
\mathrm{FF}(x) = \mathrm{GELU}(x W_1 + b_1) \, W_2 + b_2
$$

$$
\mathrm{GELU}(z) \approx \frac{z}{2} \left(1 + \tanh\left(\sqrt{2/\pi} \, (z + 0.044715 \, z^3)\right)\right)
$$

- x: one token's normalised vector (64 numbers in the Fables model).
- W with subscript 1 and b with subscript 1: the first layer's weights and biases, which make the 256 neurons; GELU(xW₁ + b₁) are the neuron activations the lab shows.
- W with subscript 2 and b with subscript 2: the second layer, which turns the 256 activations back into 64 numbers.
- The GELU line is the usual tanh form of GELU, the one this app computes.

## Brains and models

The artificial neuron is named after the nerve cell, but it is far simpler: a weighted sum and a fixed curve. A real neuron is a living cell with thousands of connections, firing in brief pulses over time and shaped by chemistry.

"Knowledge stored in the feed-forward layers" sounds like memory, and the research above suggests the comparison has something to it. How the brain itself stores memories is understood only in part, so whether the two work alike is an open question.
