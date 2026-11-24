---
id: multi-head
section: parts
order: 5
title: Multi-head attention
lab: MultiHeadLab
sources: vaswani2017, textbook
branches: Switch a head off and compare the guesses=compare-guesses; Joining the heads at one token=joining
idea: Several attention heads run side by side, each with its own learned numbers, and their outputs are joined and mixed.
part: attention
---
One attention head makes one weighted average per token, so it tends to follow one kind of pattern. A transformer runs several heads side by side in each layer, so one head can, for example, look at the token just before while another looks further back. The Fables model has 4 heads in each of its 2 layers.

Every head reads the token's whole vector, all 64 numbers in the Fables model, and makes its own smaller query, key and value from it, 16 numbers each. The heads do not split the input between them; they each look at all of it, through their own learned matrices.

In the lab, type some text and choose a **Layer**. Each head has a panel with its own heat map, which often looks quite different from its neighbours'. Nobody assigned the heads their jobs; any division of labour came out of training, and it is often not tidy enough to name.

Clear a head's check box to switch it off: its output becomes zeros, though its squares stay on screen, dimmed, to show where it would have looked. Compare **With your choices** and **With every head working** to see how much the guesses depend on that head. Researchers use the same trick, called ablation, to find out what parts of a model do.

Under **Joining the heads at one token**, pick a **Token**. Each head's output is a short vector; the four are joined side by side into one vector of 64 numbers, and then a learned matrix, the **output projection**, mixes them. The result is what attention adds to the [residual stream](lesson:residual-stream).

The original transformer used 8 heads of 64 numbers each, on vectors of 512 numbers (Vaswani et al., 2017).

*Sources: Vaswani et al. (2017); the rest is standard textbook material.*

## Try this
1. With Layer 1 chosen, compare the head panels (four in the Fables model): each head spreads its attention in its own way.
2. Clear the check box of Head 2 to switch it off, and compare With your choices and With every head working.
3. Tick Head 2 again. Under Joining the heads at one token, pick a Token and watch the heads' short outputs join into one long vector.

## Check yourself
? In the shipped Fables model, how many numbers does each head use for its own query, key and value?
- 4
* 16
- 64
why: Each head reads the whole 64-number vector and makes its own query, key and value of 64 ÷ 4 = 16 numbers. The four outputs, joined, give 64 numbers again.
see: joining
checked-by-code: MultiHead_FablesHeadsUse16NumbersEach

? In the shipped Fables model, with the lab's starting text "the fox and the ", where does head 2 of layer 1 look most?
* At the token just before
- At the first token of the text, whatever the text is
- Evenly at every token it is allowed to see
why: In every row of its heat map after the first, the darkest square is the token just before. Nobody assigned it that job; it came out of training. Choose Layer 1 and look at the Head 2 panel: the dark squares run just left of the diagonal.
see: lab
checked-by-code: MultiHead_FablesLayer1Head2LooksAtThePreviousToken

? You switch a head off and the guesses hardly change. What does that show?
- That the head does nothing at all and could be removed
- That the head is broken and needs to be trained again
* Not much: other parts may make up for it
why: As with lesion studies in neuroscience, "no change" does not prove that the part did nothing.
see: brains

## Formula

$$
\mathrm{MultiHead}(X) = \mathrm{Concat}(\mathrm{head}_1, \ldots, \mathrm{head}_H) \, W^{O} + b^{O}
$$

$$
\mathrm{head}_h = \mathrm{Attention}(X W^{Q}_{h} + b^{Q}_{h}, \; X W^{K}_{h} + b^{K}_{h}, \; X W^{V}_{h} + b^{V}_{h})
$$

$$
d_{\mathrm{head}} = \frac{d}{H}
$$

- X: the normalised vectors of all the tokens; every head reads all of X.
- H: the number of heads; head with subscript h is the output of head h, with d over H numbers per token.
- W and b with Q, K, V and subscript h: head h's own learned matrices and biases.
- Concat: joins the head outputs side by side; W with superscript O and b with superscript O are the output projection.

## Brains and models

The brain also works on many things at once: in vision, for example, different areas respond more to motion, to colour or to faces. Heads are a far plainer kind of parallel work. They are identical in design and differ only in their learned numbers.

Switching off a head is like a lesion study in neuroscience, with the same pitfall. If nothing changes, other parts may be making up for the loss, so "no change" does not prove that the part did nothing.
