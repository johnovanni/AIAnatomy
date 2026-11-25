---
id: expert-system
section: family
order: 2
title: Rules: an expert system
lab: ExpertSystemLab
sources: shortliffe1976, textbook
idea: An expert system chains if-then rules that people wrote, so you can read why it answered, but it fails where the rules run out.
part: rules
---
An **expert system** does one narrow expert job by following if-then rules that people wrote down, usually with the help of human experts. This lab's system names an animal. Its rules say things like "if has hair then mammal" and "if mammal and eats meat then carnivore". Its **facts** start as your yes or no answers to its questions, such as whether the animal has feathers.

It reaches an answer by **forward chaining**. It goes through the rules in order. When every condition of a rule holds, the rule fires and its conclusion becomes a new fact. It goes round the rules again and again until nothing new follows. A conclusion that no other rule builds on, here an animal's name, is an answer. A condition with "not", such as "not flies", holds when you did not answer yes to it.

The great strength is that **you can read why**. The trace lists each rule that fired, in order, so every step from your answers to the answer can be checked. Nothing here was learned: a person wrote every rule.

The great weakness is that the rules know only what someone thought to write. Describe the bat: the rules work out that it is a mammal, then have nothing more to say, because nobody wrote a rule for a flying mammal. This is called **brittleness**. Patching it with a new rule can break a case that used to work, as Try this shows.

Systems like this were built in the 1970s and 1980s for narrow jobs, and some were useful in daily work. MYCIN, described by Shortliffe (1976), used rules written with doctors' help to suggest antibiotics for serious blood infections. It was a research system; it worked backwards from possible conclusions rather than forwards, and weighed each rule by how certain it was, but its knowledge too was written by people. Outside the narrow field their rules covered, such systems had nothing to go on.

*Sources: Shortliffe (1976) for MYCIN; the rest is standard textbook material.*

## Try this
1. Read the trace for the tiger: rule 1 makes it a mammal, rule 5 a carnivore and rule 10 a tiger.
2. Under Describe, press bat. No animal fits: the rules call it a mammal and stop there.
3. Press Add rule, type "if flies then bat" in the new box and press Enter. The bat now gets an answer.
4. Under Describe, press albatross. The patch has broken it: the rules now give two answers. Change the new rule to "if mammal and flies then bat" and try both animals again.

## Check yourself
? With the default rules, what does the system answer for the bat (has hair, gives milk, flies)?
- Bat
* No animal fits these answers
- Bird, because it flies
why: Rule 1 makes it a mammal, but no rule goes on from a mammal that flies to any animal, so nothing more follows. The bird rule needs it to lay eggs too. Describe the bat and read the trace.
see: lab
checked-by-code: ExpertSystem_BatGetsNoAnswerFromTheDefaultRules

? What can you do with an expert system's answer that is much harder with a learned model's?
* Read the rules that fired, step by step, to see why it gave that answer
- Trust it on cases that its rules never mention
- Watch it improve its own rules from examples
why: The trace lists every rule that fired, in order, and a person wrote each one. A learned model's behaviour is spread over many numbers that nobody wrote. But the rules know nothing beyond what was written, and they never change by themselves.
see: explanation

? In forward chaining, when does a rule fire?
* When all its conditions hold and its conclusion is not known yet
- When at least one of its conditions is a known fact
- When its conclusion is the answer the system is hoping for
why: Each rule that fires adds its conclusion as a new fact, and the system goes round the rules until nothing new follows. For the tiger, rule 5 fires only once rule 1 has made "mammal" a known fact.
see: explanation
