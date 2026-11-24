---
id: learning
section: parts
order: 9
title: Learning
lab: LearningLab
sources: lillicrap2020, textbook
branches: One neuron learning a rule=neuron; Walking down a loss surface=surface; Backpropagation on a small graph=backprop; The loss of the real model=crossentropy
idea: A model learns by nudging its numbers downhill, guided by gradients from backpropagation, to make its loss, a measure of error, smaller.
part: learning
---
Learning, for a model, means changing its numbers so that a **loss**, a single number that measures how wrong it is, gets smaller. The lab shows four small pieces of that idea.

**1. One neuron learning a rule.** A single neuron, the classic perceptron, answers 1 or 0 for two inputs. Choose AND, OR or XOR under **Rule to learn**, then **Step one epoch** (one pass over the four points) or **Run**. After each wrong answer the weights are nudged, and the dashed decision line moves. AND and OR are soon learned. XOR never is: no straight line can separate its points, which is why networks need hidden layers.

**2. Walking down a loss surface.** Here the loss is a bowl, w1² + 10·w2², drawn as a contour map. Gradient descent starts at the square and takes 30 steps downhill, each one against the slope and in proportion to the **Learning rate**. Too small and it creeps; just right and it settles at the bottom; above 0.1 each step overshoots further than the last, and the path flies off.

**3. Backpropagation on a small graph.** The graph computes L = (a·b + c)·f. Change the four numbers and every value and gradient is recomputed. A gradient says how much L would change if that one number changed. Backpropagation finds every gradient in one backward sweep, multiplying local slopes along the way: the chain rule. Real models do exactly this, for every one of their numbers.

**4. The loss of the real model.** For each place in your text, the loss is −ln p, where p is the probability the model gave to the character that really came next. A sure, right guess costs almost nothing; a confident wrong one costs a lot. Training lowers the average of these losses over a great deal of text, as the [train](lesson:train) lesson shows.

*Sources: standard textbook material (Lillicrap et al., 2020, is cited in the note below).*

## Try this
1. In part 1, choose XOR under Rule to learn and press Run: some point always stays on the wrong side of the dashed line.
2. In part 2, move the Learning rate above 0.1 and watch the path fly off the plot.
3. In part 3, change a, b, c or f and read the working, from L back to the inputs.
4. In part 4, pick a Place in the text: the lower the probability the model gave the character that really came next, the higher the loss.

## Check yourself
? Why can the single neuron in part 1 never learn XOR?
* No straight line separates its 1s from its 0s
- Its learning rate is too small; a larger one would fix it
- XOR needs more than the 100 epochs the lab allows
why: The neuron splits the plane with one straight line: 1 on one side, 0 on the other. XOR's points cannot be split that way, which is why networks need hidden layers. Choose XOR and press Run: after 100 epochs some points are still wrong.
see: neuron
checked-by-code: Learning_PerceptronNeverLearnsXor

? On the loss surface in part 2, what happens with a Learning rate of 0.12?
- The path creeps along and stops well short of the bottom
* Each step overshoots further and the path flies off
- It reaches the bottom faster than at 0.04 and stops there
why: Along w2 each step multiplies w2 by 1 − 20 × 0.12 = −1.4, so every step jumps over the bottom and lands further away. Set the Learning rate to 0.12 and read the bold message above the plot.
see: surface
checked-by-code: Learning_RateAbovePointOneOvershoots

? In part 4, which kind of guess adds the most to the loss?
- A sure guess that turns out right
- A guess that gives the real character 50%
* A confident guess that turns out wrong
why: The loss at one place is −ln p, where p is the probability given to the character that really came next. A sure, right guess costs almost nothing and 50% costs about 0.69, but a confident wrong guess leaves p tiny, so −ln p is large.
see: crossentropy

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
