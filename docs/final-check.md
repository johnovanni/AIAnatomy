# Stage 1 final check

Date: 2026-11-23. Branch `stage-1`, after commit 6601e09 ("Write Stage 1 lesson texts and sources").

## Tests

`dotnet test AIAnatomy.slnx -c Release` (Release, so the slow induction-head test runs too):

| Project | Passed | Failed | Skipped |
|---|---:|---:|---:|
| AIAnatomy.Core.Tests | 61 | 0 | 0 |
| AIAnatomy.Models.Tests | 123 | 0 | 0 |
| AIAnatomy.Lessons.Tests | 46 | 0 | 0 |
| AIAnatomy.App.Tests | 1 | 0 | 0 |
| AIAnatomy.Presentation.Tests | 294 | 0 | 0 |
| **Total** | **525** | **0** | **0** |

In a Debug build the slow induction-head test is skipped on purpose (524 run, 1 skipped), because it needs Release speed.
The Release build of the app has 0 warnings.

## Self-tests (Release build)

- `AIAnatomy.App.exe --selftest all-labs`: exit 0. All 16 labs were created, reset and deactivated.
- `AIAnatomy.App.exe --selftest train`: exit 0. It ran 5 training steps with finite losses. The first step's training
  loss was 4.23, close to ln 69 ≈ 4.23, as expected for an untrained model guessing evenly among 69 tokens.

## What was checked by eye

Pictures were made with `--render-png`, which draws the window into a PNG without showing it or capturing the
screen.

- **Every lesson in Light at 20 pt** (16 pictures, 1600 × 1000): all lessons open with their lab and explanation, and
  nothing overlaps.
- **Five lessons in Dark at 32 pt** (attention, feed-forward, output and sampling, learning, step-through), at window
  heights of 1600 and 7000 pixels. At 32 pt the explanation moves below the lab (the stacked layout), as designed.
- **Numbers checked by hand:** the Scale lab's part counts add up to the total (4,416 + 4,096 + 33,280 + 66,176 +
  640 + 4,485 = 113,093). Each part also matches its shape: 69 tokens × 64 for the embeddings, 64 × 64 for the
  positions, and 64 × 69 + 69 for the output layer.

Found in this check and sent to the whole-branch review. All four were fixed in the final review's fix wave
(`.superpowers/sdd/2026-11-22-aianatomy-stage-1/final-fix-report.md`: I6, I7, I1 and I4), and are to be checked again
in new pictures:

- In the Attention toy at 32 pt the two small plots keep their size while the text grows, so labels overlap (K3 on
  K2, Q on K1, Answer on V1).
- The Step-through lab opens scrolled a little down, so its first lines are hidden until you scroll up.
- The Feed-forward lab numbers its bars from 0, while every other lab numbers tokens from 1.
- The architecture map and Position lab say "␣ is a space" but show plain spaces in "What the model reads".

Not yet seen in a picture: the logits table at the bottom of Output and sampling, and the loss formula at the end
of Learning, both at 32 pt. They sit below the lab's own scroll area even in a 7000-pixel-high window. The final
review's fix wave added `--render-png <lesson> <file.png> --full-lab`, which draws the lab pane by itself at its full
height (final review I8), so they can now be checked.

## What could not be checked without you

- **Dragging** the points in the Attention toy, and how it feels with a mouse. (The Learning loss surface has no
  points to drag: its path always starts at the same point, and only the learning rate is changed.)
- **How fast training feels**, and whether a run of a few minutes is pleasant to watch. Run a Release build
  (`dotnet run -c Release --project src\AIAnatomy.App`); a Debug build trains noticeably slower.
- **Keyboard-only use** all the way through: Tab order, arrow keys in the plots and steps, and Page Up/Page Down in
  the Step-through lab.
- **Your eyes at your usual text size and theme**, especially 26 pt and above, where the explanation moves below the
  lab.

## Try these yourself

1. **Rules versus learning:** press **Learn** a few times and watch the gibberish on the right turn into word-like
   text, while the rules on the left keep writing "the the the".
2. **Architecture map:** type "The fox and the" and watch the probability bars at the bottom change with each letter.
3. **Tokens:** move the **Merges** slider from 0 to 200 and watch "The fox and the crow" go from 20 tokens to about 8.
4. **Attention, part 1:** drag Q until it points at K2, and watch K2's weight grow and the Answer move towards V2.
5. **Attention, part 3:** run the induction-head experiment and look for the head that copies what followed the same
   token last time.
6. **Multi-head attention:** switch heads off one at a time and see which one changes the guesses most.
7. **Residual stream:** follow the last token and compare the logit-lens guesses after the embeddings, after block 1
   and after block 2.
8. **Output and sampling:** set Temperature to 0, then 2, and press **Generate 200 characters** each time.
9. **Learning:** choose XOR and press **Run**. It never settles, because no straight line separates XOR's points.
10. **Training a model:** train a small model (width 32, 2 layers) for a few hundred steps, press **Use in the labs**,
    then revisit the Attention and Residual stream labs.
11. **Step through a prediction:** press **Next** all the way through and watch one token's numbers change, part by
    part.
12. **Scale:** compare the bar for our model with GPT-2 and GPT-3 on the logarithmic scale.

# Stage 2 Part A final check

Date: 2026-11-24. Branch `stage2-part-a`, after commit 45dc716 ("Stage 2A: learning by reward review fixes"), with
the changes of this check (one shared check-box style and one placeholder sentence).

## Tests

`dotnet test AIAnatomy.slnx` (Debug), output saved to `.superpowers/sdd/2026-11-24-aianatomy-stage-2-part-a/test-task-14.log`:

| Project | Passed | Failed | Skipped |
|---|---:|---:|---:|
| AIAnatomy.Core.Tests | 61 | 0 | 0 |
| AIAnatomy.Lessons.Tests | 111 | 0 | 0 |
| AIAnatomy.App.Tests | 19 | 0 | 0 |
| AIAnatomy.Models.Tests | 237 | 0 | 1 |
| AIAnatomy.Presentation.Tests | 681 | 0 | 0 |
| **Total** | **1109** | **0** | **1** |

The skipped test is the slow induction-head training test, which runs only in Release.

## Self-test

`AIAnatomy.App.exe --selftest all-labs` (Debug): exit 0. All 25 labs were created, reset and deactivated, including
the ten section 4 lessons (family tree, expert system, search, classic ML, learning by reward, and the five
placeholders 4.6 to 4.10). The learning-by-reward check played 5 episodes ("Episodes: 5. The last one took 27 steps.").

## What was rendered

All pictures were made with `--render-png`, which draws the window into a PNG without showing it or capturing the
screen. They are in `.superpowers/sdd/2026-11-24-aianatomy-stage-2-part-a/final/`.

- **Thirteen lab views in Dark and Light at 10, 13 and 24 pt** (six launches, `--full-lab`, 78 pictures):
  `family-tree`, `family-tree-chosen`, `expert-system`, `expert-system-edited`, `search`, `search-midgame`,
  `search-won`, `classic-ml`, `classic-ml-neighbours`, `learning-by-reward`, `reward-trained`, `image-filters`
  (a placeholder) and `multi-head` (for the check boxes).
- **The idea maps and two lesson pages in Dark at 13 pt and Light at 24 pt** (two launches): `ideamap-radial`,
  `ideamap-loop`, and `family-tree` and `classic-ml` without `--full-lab`, so the lesson header with its part icon shows.
- **One re-check** in Light at 24 pt: `image-filters` as a whole page, after the placeholder fix.

## What was checked by eye

- **The family tree at 24 pt** (the review focus): the whole tree fits the lab pane in both themes; no bubble or line
  overlaps; the chosen bubble (Rules) is filled, with dark text in Dark and white text in Light, and its card is
  complete down to "Open lesson 4.2".
- **Part icons:** the lesson header shows the family icon (People) on 4.1 and the classic icon (Filter) on 4.4, in
  both themes. The map bubbles and tree rows show the part colour without an icon; that is open issue AA-022.
- **The Radial map with section 4:** it reads. The map is 1383 × 869 px at 13 pt, the centre box about a third of
  the way across; 4.1 to 4.10 run in order round "4 The family of AI", the five later ones marked "later". 4.1 and
  4.2 sit to the right under section 3, on long lines. No tuning (the owner's checkpoint); noted on AA-007.
- **The Loop map** after section 4's connections became routed-only: no line crosses a flow label; the family tree
  sits in a corner (top right at 13 pt, top left at 24 pt) and does not push any box out of the flow.
- **Classic ML in Light at 13 pt:** the hint line under the plot's x axis ("Point at a point to see its class and
  place. Tab to the plot to use the keyboard.") shows in the muted colour, with no blank band.
- **The reward lab's steps chart in Light at 24 pt:** it is tall enough to read (ticks 50, 100 and 150, the early long
  episodes and the flat tail).
- **Check boxes:** 13 px at 10 and 13 pt, 24 px at 24 pt, in the expert system and Multi-head labs, ticked ones clearly
  ticked in both themes.
- **Numbers checked by hand:** the search look-ahead's options add up (4 × 6,812 + 4 × 7,064 + 1 = 55,505); the
  trained maze's start is worth −2.25, its value 10 steps from the goal (−(1 − 0.9⁹) / 0.1 + 0.9⁹ × 10 ≈ −2.25); the
  squares next to the goal are worth 10.00 and the next ones 8.00; nearest neighbours with k = 1 is right on all 27
  training points, the depth-3 tree on 26 of 27.

Nothing was clipped and no text overlapped in any picture.

## Fixed in this check

- **Check boxes grew with the text** (controller ruling M): one shared `CheckBox` style, 13 px at 13 pt as before,
  24 px at 24 pt; see `docs/decisions.md`.
- **The placeholder box** said the explanation was "on the right", which is wrong from 18 pt; it now says "beside this
  box, or below it at large text sizes". Re-rendered in Light at 24 pt, with the explanation under the lab.

## Sent to the issues sheet (not fixed)

- **AA-029** (Trivial, Low): in the search lab the winning line is drawn over the O marks, striping them.
- **AA-030** (Trivial, Low): before any learning, the reward lab's colour legend reads 0.00 at both ends.
- **AA-031** (Trivial, Low): a check box sits a pixel or two above the middle of its text at 13 pt.
- **AA-028** now names its Presentation test: `Main_EveryRegisteredLab_ResetsAndDeactivatesWithoutThrowing`, which timed
  out once under CPU load in Task 1 and passes alone.

## Try these yourself (section 4)

1. **The family tree:** choose Q-learning, then Rules, and compare where each one's behaviour comes from.
2. **Rules:** press **bat**: the rules call it a mammal and then have nothing to say. Add "if flies then bat" and
   press **albatross** to see the careless rule break it.
3. **Search:** play X in the centre and open the computer's chosen option to see your replies beneath it.
4. **Classic machine learning:** move the depth slider from 3 to 10, then switch to nearest neighbours and try k = 1
   and k = 9.
5. **Learning by reward:** press **Run** and watch the arrows form from the goal backwards; then add a wall on the
   path and run again.
