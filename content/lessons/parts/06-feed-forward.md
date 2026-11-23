---
id: feed-forward
section: parts
order: 6
title: Feed-forward layers
lab: FeedForwardLab
sources: geva2021, anthropic2024-mapping, textbook
---
Attention moves information between tokens. The **feed-forward layer** then works on each token by itself. It is one small network, the same one for every token in the text: each token's vector goes through it separately, and no token sees another here.

The network has two layers. The first widens the vector into many **neurons**: 256 in each layer of the Fables model, four times the width of 64. Each neuron adds up the vector's numbers with its own learned weights and then applies a smooth bend called GELU, which lets large positive sums through and squashes negative ones to near zero. The second layer combines the neurons back into a vector of 64 numbers, which is added to the [residual stream](lesson:residual-stream).

In the lab, choose a **Layer** and a **Neuron**. **Along your text** shows how strongly that neuron fires at each character you type. **Where this neuron fires most** searches the first 20,000 characters of the fables and lists the ten places where it fired hardest, with the text just before and the character it had just read in a box. Try a few neurons and ask what, if anything, their places have in common.

Often you will find no single tidy meaning. Single neurons are often *polysemantic*, responding to several unrelated things. Anthropic, describing models like Claude, writes that each concept is represented across many neurons, and each neuron is involved in representing many concepts (Anthropic, 2024b).

What do these layers do in a large model? Geva et al. (2021) found that feed-forward layers act partly like key-value memories: the first layer's weights detect patterns in the text, and the second layer's weights push the prediction towards tokens that tend to follow those patterns. This is a research finding about how these layers can work, not settled fact about every model.

*Sources: Geva et al. (2021); Anthropic (2024b); the rest is standard textbook material.*

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
