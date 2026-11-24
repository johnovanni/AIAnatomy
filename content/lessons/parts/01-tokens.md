---
id: tokens
section: parts
order: 1
title: Tokens
lab: TokensLab
sources: sennrich2016, radford2019, gemma2024, textbook
branches: Byte-pair encoding=bpe; Count the letters=count-letters
idea: A model reads text as a list of numbered pieces called tokens; in this app's models each token is one character.
part: tokens
---
A model never sees letters on a page. It sees a list of whole numbers. The first step is to cut the text into pieces called **tokens** and give each piece its number, its **id**. Type in **Your text** and the lab cuts it up two ways.

**One token per character** is what the models in this app use. The Fables model knows 69 tokens: the characters that occur in the fables, plus one "unknown" token, shown as �, for any character it has never seen (an emoji, say).

**Byte-pair encoding (BPE)** starts from single characters and then repeatedly joins the pair of pieces that occurs most often in the training text. Each join is one **merge**. Move the **Merges** slider: with 0 merges every piece is one character; by 50 merges, common words such as " the" and " and" (each with its leading space) are single tokens, and the same text needs fewer tokens. Sennrich et al. (2016) adapted BPE, an older compression method, to split words for machine translation; GPT-2 uses a version that works on bytes (Radford et al., 2019). Ours works on characters, so its pieces stay readable.

**Count the letters** shows a side effect. Type a word and a letter (the lab starts with "strawberry" and "r"). The model receives the pieces, not the letters inside them. Whatever it knows about the spelling of a piece had to be learned from its training text. This is one reason why models that read sub-word pieces can stumble over questions like "how many r's are in strawberry?".

Large language models, including those behind assistants, use sub-word tokenizers with vocabularies from tens of thousands to a few hundred thousand tokens. GPT-2's has 50,257 (Radford et al., 2019); Gemma's has about 256,000 (Gemma Team, 2024). Anthropic has not published Claude's current tokenizer, so its exact pieces and vocabulary size are not known.

*Sources: Sennrich et al. (2016); Radford et al. (2019); Gemma Team (2024); the rest is standard textbook material.*

## Try this
1. Count the chips under One token per character: "The fox and the crow" is 20 tokens, one per character, spaces (␣) included.
2. Drag the Merges slider down to 0, then up to about 50: common words with their leading space, such as "␣the", become single chips.
3. Type an emoji in Your text and find its chip: �, with a thicker outline, for a character the tokenizer has never seen.
4. Under Count the letters, see how many pieces "strawberry" becomes, and how many r's are hidden inside them.

## Check yourself
? With one token per character, how many tokens is "The fox and the crow"?
- 5, one per word
- 16, one per letter
* 20, one per character
why: Every character is a token, and a space is a character too: 16 letters and 4 spaces. Count the chips under One token per character.
see: lab
checked-by-code: Tokens_FoxAndCrowIsTwentyCharacterTokens

? With the Merges slider at its default 200, the BPE tokenizer cuts "strawberry" into 7 pieces. What does a model that uses this tokenizer receive?
- 10 ids, one for each letter of the word, so it can count them
* 7 ids, one per piece, with no letters inside
- The whole word as one id, with its 3 r's marked on it
why: The model receives the pieces' ids. Whatever it knows about the letters inside a piece had to be learned from its training text. With Merges at 200, the chips under Count the letters show the 7 pieces.
see: count-letters
checked-by-code: Tokens_StrawberryIsSevenPiecesAtDefaultMerges

? What does the character tokenizer do with an emoji, which never occurs in the fables?
* Gives it the unknown token, shown as �
- Splits it into the letters of its name, such as s, m, i, l, e
- Leaves it out of the list of tokens altogether
why: The Fables model knows only the characters that occur in the fables, plus one unknown token for everything else: 69 tokens in all. Type an emoji in Your text and look for the chip with the thicker outline.
see: explanation
checked-by-code: Tokens_EmojiIsTheUnknownToken

## Formula

Each merge joins the most frequent neighbouring pair in the training text:

$$
(a, b) = \mathrm{argmax}_{(x, y)} \; \mathrm{count}(x, y)
$$

$$
\text{characters per token} = \frac{N_{\mathrm{characters}}}{N_{\mathrm{tokens}}}
$$

- x, y: two pieces that stand next to each other inside one word; count(x, y) is how often that pair occurs.
- (a, b): the winning pair, which becomes one new piece "ab" with its own id. In this lab a tie goes to the pair seen first, and merges never join across words.
- N with subscripts: the number of characters and of tokens in your text; the lab shows their ratio under the BPE pieces.

## Brains and models

If you know American Sign Language, you know two ways to give a word: fingerspell it letter by letter, or sign it as one sign. Character tokens are like fingerspelling; BPE pieces are like signs.

The comparison breaks down at the letters. When you sign a word, you still know how it is spelled. A model that receives a piece knows nothing about the letters inside it unless its training taught it.
