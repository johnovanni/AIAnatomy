---
id: learning
section: parts
order: 9
title: Learning
lab: LearningLab
sources: lillicrap2020, textbook
branches: One neuron learning a rule=neuron; Walking down a loss surface=surface; Backpropagation on a small graph=backprop; The loss of the real model=crossentropy
---
Learning, for a model, means changing its numbers so that a **loss**, a single number that measures how wrong it is, gets smaller. The lab shows four small pieces of that idea.

**1. One neuron learning a rule.** A single neuron, the classic perceptron, answers 1 or 0 for two inputs. Choose AND, OR or XOR under **Rule to learn**, then **Step one epoch** (one pass over the four points) or **Run**. After each wrong answer the weights are nudged, and the dashed decision line moves. AND and OR are soon learned. XOR never is: no straight line can separate its points, which is why networks need hidden layers.

**2. Walking down a loss surface.** Here the loss is a bowl, w1² + 10·w2², drawn as a contour map. Gradient descent starts at the square and takes 30 steps downhill, each one against the slope and in proportion to the **Learning rate**. Too small and it creeps; just right and it settles at the bottom; above 0.1 each step overshoots further than the last, and the path flies off.

**3. Backpropagation on a small graph.** The graph computes L = (a·b + c)·f. Change the four numbers and every value and gradient is recomputed. A gradient says how much L would change if that one number changed. Backpropagation finds every gradient in one backward sweep, multiplying local slopes along the way: the chain rule. Real models do exactly this, for every one of their numbers.

**4. The loss of the real model.** For each place in your text, the loss is −ln p, where p is the probability the model gave to the character that really came next. A sure, right guess costs almost nothing; a confident wrong one costs a lot. Training lowers the average of these losses over a great deal of text, as the [train](lesson:train) lesson shows.

*Sources: standard textbook material (Lillicrap et al., 2020, is cited in the note below).*

## Formula

Perceptron rule, after each point:

$$
w \leftarrow w + \eta \, (y - \hat{y}) \, x
$$

Gradient descent, every step:

$$
w \leftarrow w - \eta \, \frac{\partial L}{\partial w}
$$

The chain rule, as used in the graph (there e = a·b):

$$
\frac{\partial L}{\partial a} = \frac{\partial L}{\partial e} \cdot \frac{\partial e}{\partial a} = \frac{\partial L}{\partial e} \cdot b
$$

Cross-entropy loss over N places:

$$
L = -\frac{1}{N} \sum_{n=1}^{N} \ln p_n
$$

- w: a weight; η (eta): the learning rate; x: an input; y: the right answer; ŷ (y hat): the neuron's answer.
- ∂L/∂w: the gradient, how fast the loss changes as w changes.
- p with subscript n: the probability the model gave to the real next character at place n.

## Brains and models

Brains learn too: connections between neurons strengthen or weaken with experience. But backpropagation needs exact error signals sent backwards through every connection, and whether brains do anything like it is an open question. Lillicrap et al. (2020) argue that the brain's feedback connections could approximate it, though this is a proposal, not an established fact.

One clear difference: a model learns only while it is being trained. Using it afterwards does not change its numbers, whereas your brain keeps changing with experience.
