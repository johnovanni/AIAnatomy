---
id: diffusion-shapes
section: family
order: 9
title: Diffusion: from noise to a shape
lab: DiffusionShapesLab
sources: sohldickstein2015, ho2020, rombach2022, textbook
idea: A diffusion network learns to say what noise was added to a point, so many small steps can turn pure noise into a shape.
part: diffusion
---
Spoiling a shape is easy. Take the points of a shape, such as the lab's spiral, add a little random noise to each, then a little more, step after step. After enough steps nothing of the shape is left: the points are a shapeless cloud of **pure noise**. Sohl-Dickstein and colleagues (2015) proposed learning to run this process backwards, so that structure comes back out of noise.

**Learning to undo noise.** In training, the network is shown a point of the shape with random noise added, and the number of the step that amount of noise belongs to. Its job is to say what the noise was. Ho, Jain and Abbeel (2020) trained networks on exactly this question and used them to make pictures. Each answer is compared with the noise really added, and the network's numbers are nudged to make the error smaller, as in the [learning](lesson:learning) lesson.

**Making something new.** Making points starts from pure noise: 500 random points in the lab. At each of 100 steps the network predicts the noise in every point, and a share of that prediction is taken away; a little fresh randomness is added back at every step but the last. Step by step the cloud gathers onto the shape. The shape is never handed to the network here: what it learned about the shape is held in its numbers. The next lesson looks inside one step.

The lab's networks are tiny: a point's x and y go in, and two numbers for its noise come out. Many image generators use the same method with far larger networks, either on the pixels themselves, as Ho and colleagues did, or on compressed versions of the images made by another network, as Rombach and colleagues (2022) did.

*Sources: Sohl-Dickstein et al. (2015) for learning to reverse a process that adds noise; Ho et al. (2020) for training a network to predict the noise and making samples step by step; Rombach et al. (2022) for diffusion on compressed images. Training by making an error smaller step by step is standard textbook material.*

## Try this
1. Press Play and watch the noise gather onto the spiral. Then move the step slider back to find the first step where you can see a spiral.
2. Change the seed: the finished spiral is made of different points, and its average distance from the shape changes a little.
3. Pick another shape, or draw one line of your own on the pad, then press Train it again and, when it has finished, Play.
4. Press Train it again on the spiral and compare your network's average distance with the shipped network's.

## Check yourself
? The shipped spiral network made 500 points with seed 1. After all 100 steps, how far is each point from the spiral on average? The shapes sit in a square from −1 to 1.
- About 0.31, around 15% of the square's width
* About 0.031, under 2% of the square's width
- About 0.0031, under 0.2% of the square's width
why: For every point the lab measures the distance to the nearest point of the spiral, then averages: 0.031, as recorded when the network was built and worked out again by the app's tests. It says how close the points land, not whether they cover the whole spiral.
see: lab
checked-by-code: DiffusionShapes_ShippedSpiralIsCloseToItsShape

? While the network is being trained, what is it asked about each noisy point?
- Where the nearest point of the shape is, to move it there
- Which of the four preset shapes the point came from
* What noise was added to the point, given its step number
why: Each training point is a point of the shape with a known amount of random noise added, and the network is asked what that noise was. Making new points then means taking predicted noise away, a share at each step, starting from pure noise.
see: explanation

? How do many image generators use the same method?
* They take noise out of pixels, or out of compressed images
- They copy and blend the training pictures closest to the request
- They draw each picture one line at a time, from left to right
why: Ho et al. (2020) ran diffusion on the pixels of images. Rombach et al. (2022) ran it on compressed versions made by another network, which their paper reports needs much less computing. Either way a network learns to predict noise, and a picture is made from pure noise step by step.
see: explanation

## Formula

A point of the shape after t steps of noise, made in one jump:

$$
x_t = \sqrt{\bar{\alpha}_t} \, x_0 + \sqrt{1 - \bar{\alpha}_t} \, \epsilon
$$

The training loss over a batch of N noisy points:

$$
L = \frac{1}{2N} \sum_{n=1}^{N} \left[ (\epsilon_{n,x} - \hat{\epsilon}_{n,x})^2 + (\epsilon_{n,y} - \hat{\epsilon}_{n,y})^2 \right]
$$

- x with subscript 0: a point of the shape (its x and y); x with subscript t: the same point after t steps of noise.
- ε (epsilon): the noise, two random numbers from a normal distribution with mean 0 and spread 1, one for x and one for y.
- ᾱ with subscript t (alpha bar): how much of the shape is left after t steps; it starts at 1 and falls close to 0 by the last step, where x with subscript t is almost pure noise.
- ε̂ (epsilon hat): the network's prediction of the noise, given the noisy point and t. Training makes L smaller; the lab's batches have N = 128 points.
