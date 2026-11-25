---
id: classic-ml
section: family
order: 4
title: Classic machine learning
lab: ClassicMlLab
sources: breiman1984, coverhart1967, textbook
idea: The two classic methods in this lab learn from examples without gradients: a tree searches for good questions; nearest neighbours keeps every example.
part: classic
---
The two classic machine learning methods in this lab learn from **examples**, as neural networks do, but with no gradients (others, such as logistic regression, use them). Both are decades old and still widely used. Each point is one example, a place with its class (A, B or C); each method learns to give every place a class.

A **decision tree** asks yes-or-no questions such as "Is x < 3?". Its questions are found by **search**: the tree tries every cut on x and on y and keeps the one that leaves the two sides **purest** by the Gini impurity below. It repeats this on each side until a side is pure or reaches the depth limit. This is how CART, from Breiman, Friedman, Olshen and Stone (1984), grows a tree, though CART then prunes it back; this lab just stops at a depth limit. The tree picks the best question one at a time, so it can miss a pair of questions that would only help together.

**Nearest neighbours** works nothing out in advance: it keeps every example. To classify a place it finds the k nearest stored points and takes a vote. Cover and Hart (1967) analysed the simplest case, k = 1, and showed that with unlimited examples its error rate is at most twice the lowest error rate any method could reach.

Both can **overfit**. A deep tree, or k = 1, gets every training point right, even odd points in another class's area, by carving small regions round them. That looks perfect, but those regions would probably misclassify new points. A shallower tree or a larger k gets a few training points wrong and draws smoother regions, which usually do better on new examples. The "Right on" line counts only training points, so it cannot show this.

*Sources: Breiman, Friedman, Olshen and Stone (1984) for growing a tree by choosing the purest split, measured by Gini impurity, and then pruning it; Cover and Hart (1967) for analysing the nearest neighbour rule and its error bound; the k-nearest-neighbour vote and overfitting are standard textbook material.*

## Try this
1. Move the depth slider from 3 to 10. The tree adds questions until it carves round the three odd points, and the line under the plot says it is right on all 27.
2. Choose Nearest neighbours and set k to 1: small regions appear round each odd point. Move k up to 9 and they are gone.
3. Press "Corners (XOR)" and choose the decision tree. At depth 2 it gets only 17 of 28 right, though two levels of well-chosen questions would separate the corners: no single first question helps on its own. At depth 5 it gets all 28.
4. Add a few points of your own, or press Clear and build a set, and watch the questions and regions change at once.

## Check yourself
? With the depth at 10, how many of the 27 starting points does the tree get right?
- About half of them
* All 27 of them
- None, because a tree cannot carve round single points
why: A deep tree keeps asking questions until each region holds points of only one class, so it carves round even the odd points. Set the depth to 10 and read the line under the plot.
see: lab
checked-by-code: ClassicMl_ADepth10TreeGetsEveryDefaultPointRight

? Why is a tree that gets every training point right not always the best tree?
- A deeper tree takes longer to grow, so it is too slow to use on new points
- Gini impurity rewards mistakes, so a perfect training score means it learned badly
* Its small regions round odd points can give new points the wrong class
why: That is overfitting: fitting the training points so closely that the odd ones shape the regions. A shallower tree gets a few training points wrong but usually does better on points it has not seen.
see: explanation

? How does nearest neighbours decide the class of a place?
* It takes a vote among the k stored points nearest to it
- It asks yes-or-no questions about x and y, like a tree
- It adjusts weights with gradients until its loss is small
why: Nearest neighbours keeps every example and compares distances; it asks no questions and uses no gradients.
see: explanation

## Formula

The Gini impurity of a group of points, where p with subscript c is the share of the group's points that are of class c:

$$
G = 1 - \sum_{c} p_c^2
$$

A pure group has G = 0; an even mix of two classes has G = 0.5. A cut sends n with subscript L points to one side and n with subscript R to the other, out of n; the tree keeps the cut with the lowest weighted impurity:

$$
\frac{n_L}{n} G_L + \frac{n_R}{n} G_R
$$

Nearest neighbours measures the straight-line distance from a place (x, y) to a stored point i:

$$
d_i = \sqrt{(x - x_i)^2 + (y - y_i)^2}
$$

- c: a class, A, B or C; p with subscript c: the share of the group's points in class c.
- G, with subscript L or R: the Gini impurity of a whole group, or of one side of a cut.
- n: how many points the group has; n with subscript L or R: how many go to each side.
- x and y: the place being classified; x and y with subscript i: where stored point i is.
- d with subscript i: the distance to point i; the k smallest distances give the k nearest neighbours.
