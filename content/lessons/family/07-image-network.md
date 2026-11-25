---
id: image-network
section: family
order: 7
title: Image networks: the whole network
lab: DigitNetworkLab
sources: lecun1998, alpaydin1998, textbook
idea: An image network stacks layers of learned filters and pooling; a fully connected layer then turns what they found into probabilities.
part: vision
---
The lab's network reads a digit in stages, following the pattern LeCun and colleagues (1998) described for reading handwritten digits: layers of learned filters, each followed by a step that shrinks the maps, then fully connected layers.

**Layer 1** has 16 learned 3 by 3 filters, like those in the last lesson. Each makes its own **feature map** of the 8 by 8 picture; its bias is added and every number below zero is set to 0. **Max pooling** then keeps only the largest number in each 2 by 2 block, so each map becomes 4 by 4: the network keeps how strongly a pattern was found and loses a little of exactly where. (LeCun's network shrank its maps by averaging; keeping the largest is a common later choice.)

**Layer 2** has 32 filters, each reading all 16 pooled maps at once, so it can respond to combinations of the first layer's patterns. Pooling again leaves 32 maps of 2 by 2: 128 numbers. A **fully connected layer** mixes all 128 into 128 new numbers, and a last layer gives one score for each digit from 0 to 9. **Softmax** turns the scores into probabilities, as it does for a transformer's next character. Training on 3,760 digits set all 22,602 numbers; nobody chose them by hand.

The digits come from Alpaydin and Kaynak (1998). They are coarse: each pixel is the ink counted in a 4 by 4 block of a 32 by 32 picture. The network has only ever seen pictures like these, which is why your drawing is first made into the same kind of 8 by 8 picture, scaled and centred like the training digits. It is right on about 98 of every 100 test digits, but it still makes mistakes, and more of them on drawings unlike the ones it learned from.

*Sources: LeCun et al. (1998) for networks of learned filters, shrinking steps and fully connected layers that read handwritten digits; Alpaydin and Kaynak (1998) for the digits (the UCI optical digits). Max pooling, fully connected layers and softmax are standard textbook material.*

## Try this
1. Click Next a few times and watch the probabilities. When the highest bar is well under 100%, see which other digit takes the rest.
2. Draw a digit on the pad, then add one extra stroke to it and watch how the reading and the probabilities change.
3. Point at squares of the layer 1 maps to read their numbers, then show a 1 and a 0 and compare which maps light up for each.
4. Press Train it again and compare your network's accuracy on the test digits with the recorded one.

## Check yourself
? The shipped network was tested on 1,740 digits written by people whose handwriting it never trained on. How often was it right?
- Right on 1,392 of 1,740, about 80%
* Right on 1,705 of 1,740, about 98%
- Right on 1,740 of 1,740, every one
why: Its most probable digit matched the label on 1,705 of the 1,740 test digits (98.0%), as recorded when it was built and worked out again by the app's tests. That is new handwriting of the same coarse 8 by 8 kind; on drawings unlike its training digits it can do worse.
see: lab
checked-by-code: DigitNetwork_ShippedAccuracyOnTheTestDigits

? Max pooling looks at one 2 by 2 block of a feature map holding 0, 0.4, 1.2 and 0.3. What does it keep?
- 0.475, the average of the four numbers
- 1.9, the sum of all four of the numbers
* 1.2, the largest of the four numbers
why: Max pooling keeps only the largest number in each 2 by 2 block, so an 8 by 8 map becomes 4 by 4. Averaging is another way to shrink a map, but it is not what this network does.
see: explanation

? Why is your drawing made into a coarse 8 by 8 picture before the network reads it?
* It learned only from 8 by 8 digits that were made that way
- Each drawing is added to its training digits as it is read
- A full-size drawing is too large for the computer to hold
why: The network has only ever seen 8 by 8 digits made by counting ink in blocks of a larger picture, and its layers are sized for 8 by 8 input. A drawing made the same way looks like what it learned from. It does not learn from your drawings.
see: explanation

## Formula

Max pooling, for row i and column j of a pooled map:

$$
p_{i,j} = \max(m_{2i,\,2j}, \; m_{2i,\,2j+1}, \; m_{2i+1,\,2j}, \; m_{2i+1,\,2j+1})
$$

Softmax, for the probability of digit d:

$$
P(d) = \frac{e^{z_d}}{\sum_{k=0}^{9} e^{z_k}}
$$

- m with subscript i, j: a feature map's number in row i and column j, counting from 0, after numbers below zero were set to 0.
- p with subscript i, j: the pooled map's number, the largest of the 2 by 2 block below it, so the pooled map is half as wide and half as tall.
- z with subscript d: the network's score for digit d, from its last layer; P(d) is that digit's probability, and the ten add up to 1.
