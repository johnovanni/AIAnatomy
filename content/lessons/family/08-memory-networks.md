---
id: memory-networks
section: family
order: 8
title: Memory networks
lab: MemoryNetworkLab
sources: elman1990, bengio1994, hochreiter1997, vaswani2017, textbook
idea: A memory network reads one character at a time and carries all it has read in one vector of numbers.
part: memory
---
Before attention, a common way to read text was a **recurrent network**, a memory network. The lab's is the simple kind Elman described in 1990. It reads one character at a time, mixing each into its **memory**, a vector of numbers (256 in the shipped network), and that memory is all it carries on. The transformer instead looks back at every earlier character in its window directly, through attention.

Both networks learned from the same fables with the same training budget, and were checked on the same 32 windows. Their validation losses are **about the same**: 1.711 for the transformer, 1.678 for the memory network. With 2,048 predictions and one training run each, that gap is too small to call.

**How far back it remembers** is measured live when the lab opens: the loss at each position of those windows. Both curves fall steeply over the first few characters, then stay about level. At this size, neither network gets much help from characters more than about 8 back.

So why did attention largely replace recurrence for language? Vaswani and colleagues (2017), who introduced the transformer, pointed to two things. A recurrent network passes information through every step in between, and Bengio, Simard and Frasconi (1994) showed that learning long-range links that way by gradient descent gets harder as the gap grows; attention links any two positions in one step. And a recurrent network must go one character after another, while a transformer trains on every position of a window at once, which suits parallel hardware such as graphics chips. This app runs on the processor (CPU), so its speed numbers can't show that.

Recurrent networks are still used in places, among them the LSTM of Hochreiter and Schmidhuber (1997), a memory network built to keep information for longer.

*Sources: Elman (1990) for the simple recurrent network; Bengio, Simard and Frasconi (1994) for why long-range links are hard to learn this way; Vaswani et al. (2017) for attention's one-step links and training on all positions at once; Hochreiter and Schmidhuber (1997) for the LSTM. The history is standard textbook material.*

## Try this
1. Read the table at the top. The two shipped networks' losses are about the same, and the note says why the gap is too small to call. The speed note says why the two speeds are real times on one PC but not a fair count of the arithmetic.
2. Look at the chart of how far back each network remembers, and the averages under it: both losses drop over the first few characters, then level off.
3. Type a word in the hidden-state box and point at squares to read their numbers: each row is the memory after that character. Change the first letter and watch every row after it change too.
4. Press Train it again to train a smaller memory network yourself (about a minute), then compare its loss and speed with the recorded ones. Reset brings back the shipped network.

## Check yourself
? In the lab's chart of loss by position, what do the two networks' curves do?
- The transformer's keeps falling to 64 characters; the memory network's stops early
* Both fall steeply over the first few characters, then stay about level
- The memory network's rises again after about 10 characters, as it forgets
why: That is what the live measurement shows: about 2.6 for both with 1 character read, about 1.8 with 5 to 8 read, and about 1.7 with 33 to 64. At this small size neither network gets much help from characters more than about 8 back, so the chart does not show the transformer's longer reach.
see: lab
checked-by-code: Memory_BothCurvesFallFastThenLevelOff

? The shipped transformer's validation loss is 1.711 and the shipped memory network's is 1.678. What does that show?
- The transformer is clearly better, as attention should be
- The memory network is clearly better, so recurrence wins
* About the same: the gap is too small to call a win
why: Both were checked on the same 32 windows, 2,048 predictions, and each was trained once from one seed. A gap of 0.033 is only about one standard error; telling them apart would take more training runs with other seeds.
see: lab
checked-by-code: Memory_TheValidationLossesAreAboutTheSame

? Which is one main reason attention largely replaced recurrence for language?
* A transformer trains on all positions of a window at once
- A transformer always gets a lower loss on a small text
- A memory network can't learn anything useful from text
why: A memory network has to go one character after another, so it can't use parallel hardware such as graphics chips as well. Vaswani and colleagues (2017) also pointed to long-range links: attention connects any two positions in one step. This lab doesn't show those advantages, because its networks are small and the app runs on the processor.
see: explanation

## Formula

After reading character t, the memory network's new memory comes from the character and its old memory:

$$
h_t = \tanh\left( x_t W_{in} + h_{t-1} W_{rec} + b \right)
$$

Its scores for the next character come from the new memory alone:

$$
s_t = h_t W_{out} + b_{out}
$$

- t: the position, counted in characters read; x with subscript t: character t as a one-hot vector (a single 1 in that character's place).
- h with subscript t: the memory after character t, 256 numbers between −1 and 1 in the shipped network (128 when you train it again); the memory before the first character is all zeros.
- W with subscript in, W with subscript rec and b: learned numbers that mix in the character, carry the old memory forward, and shift the result.
- tanh: squashes each number into the range −1 to 1, which keeps the memory bounded.
- s with subscript t: a score for every character the network knows; softmax turns them into probabilities, as in the transformer. W with subscript out and b with subscript out are learned too.
