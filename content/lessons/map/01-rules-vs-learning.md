---
id: rules-vs-learning
section: map
order: 1
title: Rules versus learning
lab: RulesVsLearningLab
sources: textbook
idea: A program's behaviour can be written as rules by a person or learned from examples, and language models learn theirs.
part: intro
---
Two writers sit side by side in this lab. Both do the same small job: look at the text so far and write the next character, then the next, one at a time.

The left writer follows **hand-written rules**, the way computers were traditionally programmed. A person wrote ten rules, such as "After q, write u" and "After t, write h". The first rule that fits wins. You can read every rule and predict exactly what it will write, and you will see it fall into a loop, because the rules are all it has.

The right writer is **a model that learns**. It is a table of scores: one row for each character, one column for each character that might come next. At first every score is zero, so every next character is equally likely and the writing is gibberish. Press **Learn** and it trains on the opening pages of Aesop's fables: 400 steps per press, shown 20 at a time. Each step compares the table's guesses with what really came next in the text, and nudges every score a little so that the real next character becomes more likely. Watch the loss (a measure of how wrong the guesses are) fall on the chart, and the writing turn into letter patterns. The learned writer picks each character by a random draw from its scores; the **Seed** box fixes those draws, so a result can be repeated.

Both writers are strict, exact arithmetic. Neither does anything mysterious. The difference is where the behaviour came from: a person wrote the rules, but nobody wrote the table's scores. They were learned from examples.

That is the central idea of this app. A large language model is still that kind of strict computer underneath, but its behaviour was learned rather than written. This table looks only one character back; the [architecture map](lesson:architecture-map) shows a model that can look back over many.

*Sources: standard textbook material.*

## Try this
1. Read the entry under Learned from examples: before any training (after 0 steps) every next character is equally likely, so it writes gibberish.
2. Press Learn and wait for the run to end: a new entry, marked Newest, goes on top with its step count and loss.
3. Press Learn a few more times. Watch the loss fall on the chart and the writing turn into letter patterns.
4. Compare What the rules write: from "The " the written rules write "the the the", and they never change.

## Check yourself
? Where did the learned writer's scores come from?
- A person typed them in, one score at a time, from a book of rules
* Training on examples from the opening pages of Aesop's fables
- They were copied over from the ten written rules in the lab
why: Every score starts at zero. Each training step nudges the scores so that the character that really came next in the fables becomes more likely.
see: explanation
checked-by-code: RulesVsLearning_LearnsFromTheOpeningPagesOfTheFables

? With the start text "The ", what do the written rules write?
- A different sentence each time you press Learn
- Gibberish that slowly turns into real words as it trains
* "the the the ...", over and over
why: After a space, rule 5 writes t; then t gives h, h gives e, and e gives a space, so the rules go round the same loop for ever. Training never changes them.
see: lab
checked-by-code: RulesVsLearning_RulesRepeatTheFromTheStartText

? Before you press Learn, why is the learned writer's text gibberish?
* Every score starts at zero, so every character is equally likely
- The fables are written in an old kind of English that it cannot read yet
- The ten written rules in the lab get in its way and scramble it
why: Softmax turns equal scores into equal probabilities, so each character is a random draw from all 69 tokens (the 68 characters of the fables and the unknown token), and the loss is ln 69, about 4.234. Read the loss line under the two screens before you press Learn.
see: formula
checked-by-code: RulesVsLearning_UntrainedTableIsUniform

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
