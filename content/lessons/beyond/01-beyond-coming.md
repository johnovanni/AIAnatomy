---
id: beyond-coming
section: beyond
order: 1
title: Beyond the model (coming later)
lab: ComingLaterLab
sources: ouyang2022, bai2022, textbook
---
A bare model like the ones in this app only continues text. An assistant such as Claude needs more around it. This part of the app comes in a later stage, after a short design review with you. It will describe what is publicly known about models like Claude, never inside knowledge, and it is planned to cover:

- **Training stages.** Pretraining on a large amount of text; then fine-tuning on example conversations; then learning from feedback. That includes reinforcement learning from human feedback (Ouyang et al., 2022) and Constitutional AI, in which a model is trained to follow a written set of principles, as Anthropic has published it (Bai et al., 2022).
- **The context window.** Our model visibly cannot use text beyond its context length. Assistants have the same kind of limit, only much larger.
- **No memory between conversations.** Chatting does not change a model's numbers. The lab will show our model's weights unchanged before and after a "chat". Memory features work by putting saved text back into the context.
- **Thinking as extra text.** Steps a model writes out for itself become part of its context, and can help it reach a better answer.
- **Tools.** The model writes a special request, an ordinary program carries it out (a calculator, say), and the result is put back into the context for the model to read.

The stage will also end with a map of how the family of AI fits together in a modern assistant. Each of these builds on what you can already see here: tokens, the context length, sampling, and the fact that the numbers stay fixed once training ends.

*Sources: Ouyang et al. (2022); Bai et al. (2022); the rest is standard textbook material.*
