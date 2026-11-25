---
id: learning-by-reward
section: family
order: 5
title: Learning by reward
lab: RewardLab
sources: watkins1992, suttonbarto2018, schultz1997, textbook
idea: An agent learns by trial and error from rewards alone: nobody shows it the right moves.
part: reward
---
In the other labs a program was given rules, or shown examples of right answers. Here nobody shows the **agent** the right way through the maze. It only gets **rewards**: 10 for stepping into the goal, and −1 for every other move, so wandering costs it. From these numbers alone it has to work out which moves are good. This is called reinforcement learning, and the method in this lab is **Q-learning**, first described by Watkins in 1989. Watkins and Dayan (1992) proved that, with a table like this one, it ends up with the best values as long as every move in every square keeps being tried.

The table holds a number for each square and each of the four moves: the agent's estimate of the reward still to come if it makes that move. A square's **value** is its best number, shown by the shading, and the **arrow** shows the move with that number. After every move the agent nudges one number towards what just happened: the reward it got, plus the value of the square it landed in. Good news from the goal spreads backwards, one square at a time, episode after episode.

The **discount** (0.9 here) says how much a reward one step later counts. With it, a square one step from the goal is worth 10, two steps away −1 + 0.9 × 10 = 8, three steps away 6.2, and so on.

The agent also has to choose between using what it knows and **exploring**. With an exploration rate of 0.2 it makes a random move one time in five. Exploring finds better routes and fills in the table; never exploring can lock in the first route that worked.

This maze is a tiny case. Sutton and Barto (2018), a standard textbook, also cover much bigger problems, where an estimate such as a neural network takes the place of the table; those are far harder to learn.

*Sources: Watkins and Dayan (1992) for Q-learning, its update and its proof; Sutton and Barto (2018) for reinforcement learning, values, the discount and exploring, which are standard textbook material.*

## Try this
1. Press Step a few times. The ring is the agent, and the line under the maze says which way it moved and the reward it got. Each step costs 1, so the move it just made scores lower and an arrow appears pointing another way.
2. Press Run. The chart shows the steps in each episode: the first takes over 100 steps, the last ones 10 to 20. The arrows now show a way to the goal, and the start is worth about −2.25, the value worked out by hand for a square 10 steps from the goal.
3. Press Reset, set the exploration rate to 0 and press Run: by the end the agent walks a 10-step route every time, but squares away from that route keep wrong values. Try 1 instead: every square gets its hand-worked value, such as 8 two steps from the goal, but the agent only wanders.
4. With the wall tool, click squares to add or remove walls; the agent starts learning again. Wall the goal in, and the lab says it can't be reached: every episode then stops at 200 steps.

## Check yourself
? With the lab's rewards (10 at the goal, −1 for each other move) and a discount of 0.9, what is a square two steps from the goal worth once the agent has learned?
- 10, because the goal is worth 10
- 9, because one step costs 1
* 8
why: From one step away, the move into the goal is worth 10. From two steps away, the first move costs 1 and the rest is worth 0.9 × 10, so −1 + 0.9 × 10 = 8. Set the exploration rate to 1 and press Run to see it.
see: lab
checked-by-code: Reward_ASquareTwoStepsFromTheGoalIsWorthEight

? What does the agent learn from?
* Rewards for where its moves led, with no examples of right moves
- Examples of the right move in each square, given by a person
- Rules about mazes that a person wrote for it
why: Nobody shows it a right answer. It tries moves, gets 10 at the goal or −1 for a step, and changes its numbers from that.
see: explanation

? Why does the agent sometimes make a random move?
- Because Q-learning cannot work out which move is best
* So it keeps trying moves it may be wrong about and can find better routes
- Because random moves earn a bigger reward than planned ones
why: That is exploring. With the exploration rate at 0 the agent repeats the first route that worked, and squares away from it keep wrong values; exploring keeps filling in the table.
see: lab

## Formula

After each move a from square s, landing in square s′ with reward r, the agent changes one number in its table:

$$
Q(s, a) \leftarrow Q(s, a) + \alpha \left( r + \gamma \max_{a'} Q(s', a') - Q(s, a) \right)
$$

A square's value is its best number. At the goal it is 0, because the episode ends there:

$$
V(s) = \max_{a} Q(s, a)
$$

- s: the square the agent was in; a: the move it made, up, right, down or left.
- s′: the square it landed in (the same square after bumping into a wall or the edge); a′: any move from there.
- r: the reward, 10 for stepping into the goal and −1 (the step cost) for any other move.
- Q(s, a): the agent's number for making move a in square s; every number starts at 0.
- α (alpha): the learning rate, 0.5 in this lab: how far each number moves towards its new target.
- γ (gamma): the discount, 0.9 at first: how much the next square's value counts.
- V(s): the square's value, shown by its shading; its arrow is the move with that best number.

## Brains and models

Animals learn from rewards too, and brain research has found a signal that looks related. Schultz, Dayan and Montague (1997) brought together recordings suggesting that dopamine neurons in primates signal changes or errors in the prediction of rewards, and linked them to mathematical theories of learning to act well. Such an error is like the gap in the formula above between what the agent expected and what happened.

The comparison has limits. A brain keeps no table of squares and moves, and this lab does not show that brains run Q-learning. How brains learn from reward is still being worked out; the lab shows the arithmetic of one simple method.
