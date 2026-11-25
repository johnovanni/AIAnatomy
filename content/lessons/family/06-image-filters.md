---
id: image-filters
section: family
order: 6
title: Image networks: filters
lab: FiltersLab
sources: lecun1998, alpaydin1998, textbook
idea: An image network slides small filters over a picture, using the same few numbers at every place to look for one pattern.
part: vision
---
An image network starts by sliding small **filters** over the picture. The lab's filter is a 3 by 3 window of nine numbers. Placed on one pixel, it multiplies that pixel and its eight neighbours by its nine numbers and adds up the results. That sum is one number of the **feature map**. Then the window moves to the next pixel, and the same nine numbers are used again, at every pixel. Where the window hangs over the edge, the missing pixels count as 0, so the map is the same size as the picture.

The lab's digits are small: 8 by 8 pixels, from the handwritten digits of Alpaydin and Kaynak (1998), written by 43 people. Each pixel was made by counting the ink in a 4 by 4 block of a 32 by 32 picture, so the digits are coarse, and a filter sees blocks of ink rather than fine strokes.

A filter is built to find one pattern. The vertical-edge filter takes the column to the left of a pixel away from the column to its right, so it gives large numbers where the picture changes from empty to inked across it, and 0 where it is even. Because the same numbers are used everywhere, it finds its pattern wherever that is in the picture.

A transformer also reuses its weights at every position. What is new here is the **local window**: each number of the map depends only on one pixel and its neighbours, not on the whole picture.

Nobody wrote the shipped network's 16 first-layer filters: training set their numbers, as it set the transformer's weights. They are messier than the hand-made ones. LeCun and colleagues (1998) described networks built from layers of learned filters like these for reading handwritten digits.

*Sources: LeCun et al. (1998) for networks of learned filters that read handwritten digits; Alpaydin and Kaynak (1998) for the digits (the UCI optical digits). Sliding a filter over a picture is standard textbook material.*

## Try this
1. With the vertical-edge filter, look at the feature map: red where the ink starts as you go right, blue where it ends, and near-white where the digit is even.
2. Choose Learned 14 and read its numbers: the top row is all above zero and two of the bottom row are below, a rough horizontal edge turned upside down. Most learned filters are harder to name.
3. Draw a digit on the pad, then try Blur and Sharpen on it. Point at squares of the map to read their exact numbers.
4. Type your own nine numbers into the filter, from −10 to 10. Put 1 in the middle and 0 everywhere else: the map is then the same as the picture.

## Check yourself
? In the lab, one 3 by 3 filter makes the whole feature map of an 8 by 8 digit. At how many positions are its 9 numbers used?
- Only once, as each of the 9 numbers belongs to one pixel
- At 36 positions, where the whole window fits on the picture
* At all 64 positions, with missing pixels at the edge as 0
why: The window is centred on every one of the 64 pixels in turn, and the same 9 numbers are used each time. At the edge, the pixels it hangs over count as 0 (padding), so the map is 8 by 8 like the picture. Without that padding the window would fit at only 6 by 6 = 36 positions.
see: lab
checked-by-code: Filters_OneFilterIsUsedAt64Positions

? A picture is empty on its left half and fully inked on its right half. Where does the vertical-edge filter give its largest numbers?
* Along the line where the empty half meets the inked half
- All over the inked half, where there is the most ink
- At the four corners, where the window hangs over the edge
why: The filter takes the left column of its window away from the right column. Inside either half those columns are equal, so the map is 0; at the two columns where the halves meet it is 1 in each of 3 rows, so 3 (2 in the top and bottom rows, where one row of the window is off the picture). The right-hand edge gives −3, because the 0s beyond the picture look like a change from inked to empty.
see: explanation

? A transformer also reuses its weights at every position. What is new in an image network's filters?
- Their nine numbers are learned from many examples
* Each output looks only at a small local window
- The same numbers are used at more than one position
why: A transformer learns its weights too and uses them at every position. What a 3 by 3 filter adds is the small window: each number of its map depends only on one pixel and its eight neighbours.
see: explanation

## Formula

The feature map's number at row i and column j:

$$
m_{i,j} = \sum_{u=-1}^{1} \sum_{v=-1}^{1} w_{u,v} \, x_{i+u,\,j+v}
$$

- x with subscript i, j: the picture's pixel in row i and column j, from 0 (no ink) to 1 (all ink); a pixel off the edge of the picture counts as 0.
- w with subscript u, v: the filter's nine numbers; u is −1 for its top row, 0 for its middle row and 1 for its bottom row, and v is −1, 0 or 1 from left to right.
- m with subscript i, j: the feature map's number there. The same nine w are used for every i and j.
- In the network, each filter also adds its own number, the bias, and then numbers below zero are set to 0. The lab's map leaves both out.
