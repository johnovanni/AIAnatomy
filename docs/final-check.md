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

# Stage 2 Part B final check

Date: 2026-11-25. Branch `stage2-part-b`, after commit 82f5b11 ("Stage 2B: lesson 4.9 review fixes"), with the changes
of this check (live text for warnings and questions, the Light memory colour and the family tree hint).

## Tests

`dotnet test AIAnatomy.slnx` (Debug), output saved to `.superpowers/sdd/2026-11-25-aianatomy-stage-2-part-b/test-task-18.log`:

| Project | Passed | Failed | Skipped |
|---|---:|---:|---:|
| AIAnatomy.Core.Tests | 78 | 0 | 0 |
| AIAnatomy.Lessons.Tests | 112 | 0 | 0 |
| AIAnatomy.App.Tests | 59 | 0 | 0 |
| AIAnatomy.Models.Tests | 376 | 0 | 1 |
| AIAnatomy.Presentation.Tests | 840 | 0 | 0 |
| **Total** | **1465** | **0** | **1** |

The skipped test is the slow induction-head training test, which runs only in Release. The known slow test
`Main_EveryRegisteredLab_ResetsAndDeactivatesWithoutThrowing` passed in this run.

## Self-test

`AIAnatomy.App.exe --selftest all-labs` (Debug): exit 0, before and after the changes of this check. All 25 labs were
created, reset and deactivated, with all ten section 4 lessons built: image filters (screens `feature-map`, `image`),
the whole image network (`maps`, `probabilities`, `training`), memory networks (`compare`, `hidden`, `memory`,
`samples`, `training`), diffusion shapes (`cloud`, `training`) and diffusion steps (`forward`, `prediction`,
`reverse`). Only section 5's lesson is still a placeholder.

## What was rendered

All pictures were made with `--render-png`, which draws the window into a PNG without showing it or capturing the
screen. They are in `.superpowers/sdd/2026-11-25-aianatomy-stage-2-part-b/final/`. Nine launches:

- **The five Part B lessons and their render views, Dark and Light at 10, 13 and 24 pt** (six launches, `--full-lab`):
  `image-filters`, `filters-learned`, `image-network`, `digit-network-sample`, `memory-networks`,
  `memory-network-measured`, `diffusion-shapes` (step 0), `diffusion-shapes-done`, `diffusion-steps`,
  `diffusion-steps-five`. Width 1600, except Light 24 pt at 900 (`light24-narrow-*`), to see a narrow window.
- **Also in those launches:** the family tree at Dark 13 pt; the earlier heat-map lessons `attention`, `multi-head`
  and `position` at Light 13 and Dark 24 pt (their in-square numbers became body size in cde40f0 and had not been seen
  since).
- **The idea maps** `ideamap-radial,ideamap-loop` in Dark at 13 pt, and in Light at 24 pt with a 900 px window, where
  the launch also drew the `image-network` lesson page (not `--full-lab`) to measure the lesson column.
- **One re-check** in Light at 13 pt after the fixes: `memory-networks` and `family-tree` as lesson pages, and
  `ideamap-radial`.

## What was checked by eye

- **Lesson 4.9 at step 0** (not seen before): the plot runs from −2 to 2 on both axes and the line under it says
  "48 points are outside the plot" (for 500 standard-normal points about 45 are expected beyond ±2). At step 100 the
  spiral is clearly formed; "0.031" matches the recorded 0.0315.
- **Lesson 4.7's bottom** at Dark 24 pt and 1600 px (7,239 px tall, under the renderer's 12,000 px cap): the end of
  layer 2 (maps 25 to 32 and their scale), the pooling and fully connected sentences ("71 of the 128 numbers are above
  zero") and Train it again are all there. In the 900 px Light 24 render 4.6 and 4.7 still reach the cap (see AA-035).
- **Lesson 4.10's numbers checked by hand:** at step 20, ᾱ = 0.823, √0.823 = 0.907 and √0.177 = 0.421 as the line says;
  β at step 20 is 0.0001 + 19 × 0.0999 / 99 = 0.0193. The two arrow sets differ by line and head (solid with open heads,
  dashed with filled heads), not by colour alone, in both themes.
- **Heat maps after cde40f0:** 4.6's 8 × 8 picture and feature map show three decimals at 10, 13 and 24 pt; at 24 pt the
  negative numbers nearly touch (AA-036). The attention, multi-head and position maps lay out well in both checks.
  *Corrected in the final fix wave:* the multi-head, position and hidden-state maps never write numbers
  (`ShowCellValues="False"`, by design, before and after cde40f0); only lesson 2.4's two attention maps (AttentionView)
  lost their numbers with cde40f0, because their squares (about 33 px) were too narrow for "−0.500" at body size. The
  readout under each gives every value. 4.7's input picture drops its numbers in the 900 px window (squares too narrow),
  as the rule says. Both the attention numbers and AA-036 were fixed in the final fix wave (see the end of this part).
- **The family tree:** no bubble says "later" any more (every section 4 lesson in the tree is built); the idea maps
  mark only section 5 "later". The hint about "later" bubbles now shows only when there is one (fixed below).
- **The Radial map** is 1393 × 874 px at 13 pt (1383 × 869 in Part A); the centre box is about a third of the way
  across, as before, and 4.6 to 4.10 sit in order round "4 The family of AI". The layout tests pass, so the Task 11
  centre rule was not needed.
- **The Light memory colour** #3C264C read as black: the 4.8 bubble on the Light idea map looked like the black 1.2
  bubbles. Changed (below) and re-checked: the 4.8 bubble and the lesson header band are a clear violet.
- **The narrow lesson column** (measured in the Light 24 pt, 900 px page): the contents column is about 464 px and the
  lesson page about 355 px. The cause is the contents column's minimum width at 24 pt (464 px), not the title; the
  title itself breaks inside a word ("networ / ks"). Logged as AA-035.
- **Readability:** in the tiles looked at, nothing was clipped and no text overlapped, in either theme, apart from the
  small crowding items logged as AA-038 and AA-039 (and AA-036, above). Body text, captions and numbers were readable
  at 13 and 24 pt in those tiles; colour is never the only signal (the chosen digit is marked "chosen", filters carry
  + and −, the arrows differ in style).
- **Not inspected tile by tile** (judged from tiles of the same layout seen elsewhere, so not checked): the lower
  tiles of pictures that show the same lab state as another (`image-filters` against `filters-learned` below the
  filter; `image-network` against `digit-network-sample`, the same picture); most 10 pt tiles below the first; and the
  Light 24 pt, 900 px strips from the third onwards. The bottom of 4.6 and 4.7 in the 900 px Light 24 render was not
  seen at all (the renderer's 12,000 px cap; see AA-035).

## Fixed in this check

- **Live text that never spoke (controller ruling X):** the status line, Train's paste note, message and overwrite
  question, and Learning's "too large" message are now assertive live text (`c:LiveText.IsAssertive`); Settings'
  "Progress forgotten." is live text and the reset question is announced when it appears. The status line and the
  paste note are cleared before being set, so the same words twice are said twice. Tests: `LiveTextXamlTests` (the
  lines, a new test that every hand-set LiveSetting is raised by its view, and the assertive setting in either order),
  `Main_SameStatusMessageTwice_IsClearedInBetween_SoItIsReadAgain`,
  `RefusedPasteTwice_ClearsTheNoteInBetween_SoItIsReadAgain`. **Not heard with a screen reader:** these live-text
  changes are tested in code only, and an owner Narrator pass is needed before ruling X can be called done.
- **Light memory colour** #3C264C → #4B2A66 (theme contrast and colour-distance tests pass).
- **Family tree hint:** "A bubble marked later is a lesson not built yet." is shown only when a bubble is marked later
  (`HasLaterBubbles`, tested).
- **Records:** README section 4 complete; the Task 14 and Task 13 fix entries in `docs/decisions.md` separated.

## Sent to the issues sheet (not fixed)

- **AA-035** (Minor, Medium): at 24 pt in a 900 px window the lesson column is squeezed to its 360 px minimum.
- **AA-036** (Minor, Medium): three-decimal heat-map numbers nearly touch at 24 pt (a fix would reverse the Task 13
  fix's pinned three decimals at 24 pt). *Fixed in the final fix wave.*
- **AA-037** (Trivial, Low): the memory lab's map shows a scroll bar that scrolls a pixel or two at 13 pt.
- **AA-038** (Trivial, Low): in 4.7 the chosen bar's percentage sits left of the others, and two map captions nearly
  touch at 24 pt.
- **AA-039** (Trivial, Low): the two "−2" axis labels crowd the corner of the point-cloud plots.

## For the final review (lesson text, not changed)

- Lesson 4.1's Try this step 4 still says "A bubble marked later is a lesson not built yet.", though no bubble is now.
  *Removed in the final fix wave.*

## Final fix wave (after the whole-branch review)

- **Heat-map numbers:** the number of decimals now comes from the numbers actually in the map (3, then 2, then 1, then
  none), with a gap of 0.4 em between neighbours that grows with the text; numbers in squares are never in e-notation.
  8 × 8 maps keep three decimals at 13 pt and drop to two at 24 pt in a 900 px pane (AA-036 fixed).
- **Lesson 2.4's attention maps** write each weight as a whole percentage ("35"), with a note that each row's shares add
  up to 100 and the rounded numbers to about 100. The multi-head, position and hidden-state maps are unchanged.
- **Check yourself** names the answer chosen ("Not quite: you chose …"), so a second wrong answer is read out too.
- **The connection-chip sentence** is polite live text (ruling 4).
- **Still not heard with a screen reader:** every live-text change of Part B needs an owner Narrator pass.
- Renders of this wave: `render-fixwave-light13.png` and `render-fixwave-dark24.png` in the Part B folder (what was
  seen is in `fixwave-report.md` there).

## Try these yourself (section 4, Part B)

1. **Image filters:** pick Vertical edge, then Learned 14, and compare the feature maps; draw a digit on the pad.
2. **The whole network:** pick a few test digits, then draw a strongly slanted 7 and see what it reads.
3. **Memory networks:** type a word in the text box above the memory map and change its first letter; watch every row after it change.
4. **Diffusion shapes:** press Play on the spiral, then pick Two moons; draw a line, press Train it again, then Play.
5. **Diffusion steps:** move the noise step slider, then take the noise out in 100 and then 5 steps and compare.
