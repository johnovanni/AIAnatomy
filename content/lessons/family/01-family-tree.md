---
id: family-tree
section: family
order: 1
title: The family tree
lab: FamilyTreeLab
sources: vaswani2017, rombach2022, textbook
idea: A useful way to sort AI programs is by where their behaviour comes from: a person, examples, or reward.
part: family
---
The transformer in sections 1 to 3 is one member of a large family. Every member is a strict computer doing exact arithmetic, like any other program. What sets the members apart is **where their behaviour comes from**. There are three main answers, and they are the three branches of this tree.

**Written by a person.** A person works out what the program should do and writes it down. An expert system follows if-then rules that people supplied. A search program is given the rules of a game and a way to score a position, then looks ahead through possible moves. Nothing in the program changes from data.

**Learned from examples.** The program is shown many examples, often with the right answers, and fits itself to them: it adjusts its numbers or a tree's questions, or simply keeps the examples to compare new cases with. Classic machine learning, such as a decision tree or a nearest-neighbour rule, learns from a table of examples. Neural networks learn the same way, with far more adjustable numbers. The transformer is one kind; image networks, memory networks and diffusion models are others.

**Learned from reward.** The program tries actions and receives a reward or a penalty. Over many tries it learns which actions pay off. Nobody tells it the right action at each step.

The tree is a simplified picture. It leaves out many members, and real systems often mix the branches: a chess program can search ahead and also use a neural network, trained on examples or by reward, to score positions. Each bubble shows the main idea of one member.

Choose a bubble to see what it is, what it is good and bad at, and a real example. The transformer's card follows Vaswani et al. (2017), and the diffusion card's example, Stable Diffusion, is built on the latent diffusion of Rombach et al. (2022).

*Sources: Vaswani et al. (2017); Rombach et al. (2022); the rest is standard textbook material.*

## Try this
1. Choose the Transformer bubble (click it, or Tab to it and press Space): its card says what it is and offers lesson 1.2.
2. Follow the lines up from the Transformer: Neural networks, then Learned from examples, then the top of the tree.
3. Choose Rules, then Q-learning, and compare where each one's behaviour comes from.
4. Press Enter on the chosen bubble to open its lesson. A bubble marked later is a lesson not built yet.

## Check yourself
? On the family tree, which of the three main branches is the transformer on?
- Written by a person
* Learned from examples
- Learned from reward
why: A transformer is a neural network, and its numbers are learned from many examples of text. Follow the lines up from the Transformer bubble: Neural networks, then Learned from examples.
see: lab
checked-by-code: FamilyTree_TransformerIsLearnedFromExamples

? What do Rules and Search have in common?
- Both learn their numbers from rewards and penalties
* A person wrote down how they behave, and they do not change from data
- Both are neural networks with many adjustable weights
why: Both sit under Written by a person. An expert system follows rules people wrote; a search program follows the game's rules and a way of scoring that people wrote. Neither changes itself from data.
see: explanation

? How does a program on the Learned from reward branch find out what to do?
- A person writes down the right action for every situation
- It is shown the right answer for each of many examples
* It tries actions and learns from the rewards and penalties they bring
why: Nobody gives it the right action. Over many tries it learns which actions pay off, as Q-learning does in lesson 4.5.
see: explanation
