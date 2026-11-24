---
id: train
section: whole
order: 1
title: Training a model
lab: TrainLab
sources: anthropic2024-claude3, textbook
branches: The model and its learnable numbers=the-model; Training and the loss chart=the-run; What the model writes=what-it-writes; Keep your model=keep
---
Now put all the parts together and train a transformer of your own.

**1. The text to learn from**: Aesop's fables, or text you paste in. **2. The model**: a new one with the shape you set (**Width**, **Layers**, **Heads**, **Context**), or a copy of the active model to carry on training. The lab counts the learnable numbers in each part. **3. Training**: the **Learning rate**, how many **Steps**, the **Batch** (how many pieces of text each step learns from) and the **Seed**, which fixes the starting numbers and the choice of pieces, so a run can be repeated exactly.

Press **Start**. Each step takes a few random stretches of the text, has the model predict each next character, and nudges all the numbers downhill with backpropagation and Adam, an improved gradient descent. **Pause**, **Resume** and **Stop** do what they say.

Watch two lines on the chart. **Training loss** is measured on the pieces each step learned from. **Validation loss** is measured every 25 steps on the end of the text, which is kept out of training (for a very short text the two can overlap). If validation loss stops falling while training loss keeps falling, the model is memorising its text rather than learning patterns that carry over.

Every 50 steps the model writes 200 characters with the same seed, so the changes come from training, not luck. With the default settings (32,005 numbers, 600 steps) the writing goes from random characters to letter patterns and short words in well under a minute on the PC this app was built on. Set the learning rate too high and the loss runs away; training stops and says so.

Keep a result with **Save to library…**, or **Use in the labs** to make it the model the other labs use.

**Context** is how many characters the model can read at once; anything earlier is not seen. Assistants have the same kind of limit, much larger: Anthropic gave the Claude 3 models a context window of 200,000 tokens at launch (Anthropic, 2024a).

*Sources: Anthropic (2024a); the rest is standard textbook material.*

## Formula

The loss of one step is the average over the batch:

$$
L = \frac{1}{B} \sum_{b=1}^{B} L_b
$$

Adam, for each number w with gradient g:

$$
m \leftarrow \beta_1 m + (1 - \beta_1) \, g
$$

$$
v \leftarrow \beta_2 v + (1 - \beta_2) \, g^2
$$

$$
w \leftarrow w - \eta \, \frac{\hat{m}}{\sqrt{\hat{v}} + \epsilon}
$$

- B: the batch size; L with subscript b is the cross-entropy loss of piece b (see the [learning](lesson:learning) lesson).
- m and v: running averages of the gradient and of its square; m̂ and v̂ are the same with a correction for their start at zero.
- β₁ = 0.9, β₂ = 0.999, ε = 0.00000001 here; η (eta) is the learning rate, 0.003 by default.
- Before each step the gradients are scaled down if their combined size is above 1 (gradient clipping), so early steps cannot jump too far.

## Brains and models

Validation loss is the model's exam on unseen questions. People face the same trap it guards against: you can memorise a text word for word without being able to use its ideas, and a good teacher tests with new questions.

The comparison breaks down in how the learning happens. The model sees the same text again and again, with no goals or interest of its own, and stops learning the moment training ends.
