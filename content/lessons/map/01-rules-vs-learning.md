---
id: rules-vs-learning
section: map
order: 1
title: Rules versus learning
lab: RulesVsLearningLab
sources: textbook
---
Two writers sit side by side in this lab. Both do the same small job: look at the text so far and write the next character, then the next, one at a time.

The left writer follows **hand-written rules**, the way computers were traditionally programmed. A person wrote ten rules, such as "After q, write u" and "After t, write h". The first rule that fits wins. You can read every rule and predict exactly what it will write, and you will see it fall into a loop, because the rules are all it has.

The right writer is **a model that learns**. It is a table of scores: one row for each character, one column for each character that might come next. At first every score is zero, so every next character is equally likely and the writing is gibberish. Press **Learn** and it trains on the opening pages of Aesop's fables: 400 steps per press, shown 20 at a time. Each step compares the table's guesses with what really came next in the text, and nudges every score a little so that the real next character becomes more likely. Watch the loss (a measure of how wrong the guesses are) fall on the chart, and the writing turn into letter patterns. The learned writer picks each character by a random draw from its scores; the **Seed** box fixes those draws, so a result can be repeated.

Both writers are strict, exact arithmetic. Neither does anything mysterious. The difference is where the behaviour came from: a person wrote the rules, but nobody wrote the table's scores. They were learned from examples.

That is the central idea of this app. A large language model is still that kind of strict computer underneath, but its behaviour was learned rather than written. This table looks only one character back; the [architecture map](lesson:architecture-map) shows a model that can look back over many.

*Sources: standard textbook material.*

## Formula

The learned table turns the scores in row i (the character just written) into probabilities with softmax:

$$
p(j \mid i) = \frac{e^{W_{ij}}}{\sum_{k} e^{W_{ik}}}
$$

Training lowers the loss, the average surprise at what really came next:

$$
L = -\frac{1}{N} \sum_{n=1}^{N} \ln p(c_{n+1} \mid c_n)
$$

Each step moves every score a little downhill:

$$
W \leftarrow W - \eta \, \frac{\partial L}{\partial W}
$$

- W: the table of scores; W with subscripts ij is the score for character j coming after character i.
- p(j given i): the probability that j comes next after i.
- c with subscript n: the n-th character of the training text; N is the number of character pairs.
- η (eta): the learning rate, 20 in this lab; ∂L/∂W is the slope of the loss for each score.

## Brains and models

Much of what you know was never given to you as rules. You can recognise a friend's face, or feel that a sentence is wrong, without being able to state the rules you use. In that sense people learn from examples too.

The comparison breaks down quickly. This table learns only how often one character follows another, by plain arithmetic repeated many times. How brains learn is understood only in part. People do pick up how often one sound or letter follows another, but a brain is far more than one table of counts. Whether learning from examples, at very large scale, amounts to understanding is an open question; this app shows the mechanism and does not settle it.
