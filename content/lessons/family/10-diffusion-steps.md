---
id: diffusion-steps
section: family
order: 10
title: Diffusion: the steps
lab: DiffusionStepsLab
sources: ho2020, nichol2021, textbook
idea: Each step removes a share of the network's guess of the noise; in this lab, fewer, bigger steps land points further from the shape.
part: diffusion
---
**Adding noise.** The lab spoils the spiral with a straight-line schedule like the one Ho, Jain and Abbeel (2020) used, but with 100 steps instead of their 1,000. β, the noise each step adds, grows from 0.0001 at step 1 to 0.1 at step 100. ᾱ says how much of the shape is left: it falls from 1 to 0.006. Training never walks through the steps one by one: the formula below takes a point to any step t in one jump.

**The network's guess.** Shown a noisy point and its t, the network guesses the noise that was added. The lab marks 20 points and draws the true noise and the guess as arrows. Their mean squared error is the error that training made smaller, as in the [learning](lesson:learning) lesson.

**Taking noise out.** Making points runs the other way, from pure noise. Each step takes away a share of the guessed noise, scales the points up and, at every step but the last, adds some fresh randomness. Nichol and Dhariwal (2021) describe how to sample in fewer steps by skipping steps of the schedule: with 10 steps, each one jumps 10 of the 100. The network stays the same; only the jumps change.

**What fewer steps cost.** In the lab, with seed 1, fewer steps land further from the spiral: 0.031 from it on average after 100 steps, against 0.142 after 5, and the 5-step points cover much less of it (coverage is the share of the spiral with a point of the cloud within 0.05). With very few steps, how much fresh randomness each step adds matters too; Nichol and Dhariwal report that learning that amount lets a network sample in far fewer steps with little loss of quality.

*Sources: Ho et al. (2020) for the noise schedule, the network that guesses the noise and the reverse step; Nichol and Dhariwal (2021) for sampling in fewer steps by skipping steps of the schedule, and for learning how much randomness to add. The mean squared error is standard textbook material.*

## Try this
1. Move the noise step from 0 to 100 and watch the spiral dissolve. On the chart, find the step where ᾱ falls below one half.
2. At a few steps, compare the solid and dashed arrows and the error under the plot. At which steps is the network's guess closest?
3. Take the noise out in 100 steps, then in 5. Compare the two results, then move the step slider through the 5 big steps.
4. Change the seed and choose 5 steps again. Is the cloud still further from the spiral than with 100?

## Check yourself
? With seed 1, the lab makes 500 points from pure noise in 100 steps, then in 5 steps, with the same network. How far are the 5-step points from the spiral on average?
- Closer to it: 0.031 on average, against 0.142
- The same distance: both runs use the same network
* Further from it: 0.142 on average, against 0.031
why: Each of the 5 steps jumps 20 steps of the schedule. The 5-step points land further from the spiral and cover only 28% of it, against 96% after 100 steps, as the lab measures and the app checks again.
see: lab
checked-by-code: DiffusionSteps_FewerStepsLandFurtherFromTheShape

? What does ᾱ (alpha bar) at step t tell you?
- How much noise the network takes away at step t
* How much of the shape is left in a point after t steps
- How far the finished points land from the shape
why: ᾱ is the product of (1 − β) over the first t steps. A point after t steps is the square root of ᾱ times its place on the shape, plus the square root of 1 − ᾱ times its noise.
see: formula

? How does the lab take the noise out in only 10 steps?
* It skips steps of the schedule: each step jumps 10 of the 100
- It trains a new, smaller network that was taught on 10 steps
- It runs the first 10 of the 100 steps and then stops early
why: Nichol and Dhariwal (2021) describe sampling on a subset of the steps a network was trained on. The lab keeps steps 10, 20 and so on up to 100, with the same network, and works out each jump's β so that ᾱ at each kept step stays the same.
see: explanation

## Formula

A point of the shape after t steps of noise, in one jump:

$$
x_t = \sqrt{\bar{\alpha}_t} \, x_0 + \sqrt{1 - \bar{\alpha}_t} \, \epsilon
$$

One step of taking the noise out:

$$
x_{t-1} = \frac{1}{\sqrt{1 - \beta_t}} \left( x_t - \frac{\beta_t}{\sqrt{1 - \bar{\alpha}_t}} \, \hat{\epsilon} \right) + \sigma_t \, z
$$

How much fresh randomness it adds:

$$
\sigma_t^2 = \frac{1 - \bar{\alpha}_{t-1}}{1 - \bar{\alpha}_t} \, \beta_t
$$

- β with subscript t (beta): the noise step t adds; ᾱ with subscript t (alpha bar): (1 − β) multiplied over the first t steps, how much of the shape is left.
- ε (epsilon): the noise, two random numbers from a normal distribution with mean 0 and spread 1; ε̂ (epsilon hat): the network's guess of it, given the noisy point and t.
- z: two fresh random numbers like ε, drawn at every step but the last, where none is added.
- With fewer steps, t − 1 is the step before t that is kept, and β with subscript t is worked out from ᾱ so that ᾱ at each kept step stays the same.
