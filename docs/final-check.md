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
