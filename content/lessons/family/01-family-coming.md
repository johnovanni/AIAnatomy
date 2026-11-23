---
id: family-coming
section: family
order: 1
title: Family of AI (coming later)
lab: ComingLaterLab
sources: lecun1998, suttonbarto2018, ho2020, textbook
---
The transformer is one member of a large family. This part of the app comes in a later stage. It will open with a family tree: rules and search first, then learning from examples (classic machine learning and neural networks), and learning by reward. Each branch will have a working lab, built from scratch like everything else here:

- **Rules: an expert system.** An animal-identifying rule system ("Does it have feathers?") whose rules you can read, edit and trace, to show both the strength and the brittleness of hand-written rules.
- **Search.** Tic-tac-toe against minimax, with its look-ahead tree shown and scored.
- **Classic machine learning.** Place coloured points on a plot, and watch a decision tree draw its splits (with a depth slider) and nearest-neighbour show its regions (with a k slider).
- **Image networks.** Draw a digit with the mouse; a small convolutional network, the kind of network described by LeCun et al. (1998), shows its filters, its feature maps and its probabilities.
- **Memory networks.** A recurrent network trained on the same fables, compared side by side with the transformer for loss, speed and how far back it remembers, to show why attention replaced it.
- **Learning by reward.** A grid maze with walls you can edit. A Q-learning agent (Sutton and Barto, 2018) explores it, and you see the value it gives each square and the arrows it learns.
- **Diffusion.** A toy that learns to turn random noise into a shape, one denoising step at a time, the method that image generators apply to pixels, or to a compressed version of them (Ho et al., 2020).

It will end with a map of how the family fits together in a modern assistant: typically a transformer at the core, shaped in training partly by learning from reward, often reading images by cutting them into patches that act like tokens, and finding text by meaning with embeddings. For Claude in particular, it will say only what Anthropic has published.

*Sources: LeCun et al. (1998); Sutton and Barto (2018); Ho et al. (2020); the rest is standard textbook material.*
