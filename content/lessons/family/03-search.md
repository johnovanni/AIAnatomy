---
id: search
section: family
order: 3
title: Search: looking ahead
lab: SearchLab
sources: shannon1950, textbook
idea: A search program picks its move by looking ahead through the possible replies, assuming the other side always plays its best.
part: search
---
Some programs are not given rules for good play, and they do not learn it either. They work it out afresh each time by **search**: looking ahead at what could happen. This lab plays tic-tac-toe with the classic method, **minimax**.

To choose a move, the computer tries each free square in turn. After each one it tries every reply you could make, then every answer it could make to that, and so on to the end of every game. A finished game is scored as a win, a draw or a loss for the computer. The scores are then **backed up**: where it is the computer's turn, a position is worth its best move; where it is your turn, it is worth your best reply, which is the computer's worst. That is the key assumption: the other side always plays its best.

If two moves both win, the computer takes the one that wins soonest. Without that tie-break a win later scores the same as a win now, and it could dawdle. If every move loses, it takes the one that holds out longest; among moves that are still equal, it takes the first square in reading order.

Tic-tac-toe is tiny, so the computer can look all the way to the end and play perfectly: it never loses. Even so, the count grows fast. For its first move on the empty board it looks at 549,946 positions.

Real games are far too big for this. Shannon (1950) estimated about 10 to the power 120 ways a game of chess could go. He proposed looking only a few moves ahead, scoring the positions there with an **estimate** of how good they are, and backing those scores up in the same way. Textbooks add **alpha-beta pruning**, which skips branches that cannot change the choice. This lab does not prune, so you see the full count.

*Sources: Shannon (1950) for the size of chess and for looking a few moves ahead and backing up estimated scores; minimax on tic-tac-toe and alpha-beta pruning are standard textbook material.*

## Try this
1. Press "New game: you play O (the computer goes first)". The count says the computer looked at 549,946 positions for its first move, and every option in the look-ahead is a draw.
2. Play any square. The next count is far smaller: with fewer free squares there is much less to look through.
3. Start a new game as X and play a corner. In the look-ahead only the centre is a draw; every other option says "computer loses". Open the centre to see your replies beneath it: none of them beats the computer.
4. Try to win. You never will, and if you leave a line open, the computer takes the quickest win.

## Check yourself
? How many positions does the computer look at to choose its first move on the empty board?
- 9
- 362,880
* 549,946
why: It plays out every game to the end from each of the nine squares and counts every position on the way, the empty board included. Games that end early in a win stop there. Play as O and read the count.
see: lab
checked-by-code: Search_TheComputerLooksAt549946PositionsForItsFirstMove

? When the computer looks ahead, what does minimax assume about your moves?
- That you choose each of your squares at random from the free ones
* That you always make the reply that is worst for the computer
- That you copy the computer's last move whenever you can
why: Backing up the scores takes the computer's best move on its turns and your best reply on yours. If you play worse than that, the computer can only do better.
see: explanation

? Why can't a chess program simply look ahead to the end of every game, as this lab does?
* There are far too many ways a game can go to look at them all
- The rules of chess are too hard to write down for a computer
- Minimax only works on a board with nine squares or fewer
why: Shannon (1950) estimated about 10 to the power 120 ways a game of chess could go. Programs look a few moves ahead, score the positions there with an estimate, and skip branches that cannot change the choice.
see: explanation

## Formula

Minimax gives each position p a value V(p) for the computer: when the game is over, +1 for a win, 0 for a draw and −1 for a loss. Otherwise the value is backed up from the positions one move later. When it is the computer's turn in p:

$$
V(p) = \max_{m} V(p_m)
$$

When it is your turn in p:

$$
V(p) = \min_{m} V(p_m)
$$

The computer then plays a move m whose V(p with subscript m) is highest.

- p: a position on the board; p with subscript m is the position after move m is played in it.
- m: one of the free squares, a move that can be made in p.
- max: the computer takes its best move; min: you are assumed to take the reply that is worst for the computer.
- V: the value, +1, 0 or −1, always from the computer's point of view.
