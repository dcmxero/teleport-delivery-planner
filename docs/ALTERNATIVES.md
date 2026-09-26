# Alternatives, extensions and things left out

The shipped approach is in the [README](../README.md). This is what else was considered,
what was rejected and why, and where the solution would go next.

## Contents

- [Other ways to solve the same problem](#other-ways-to-solve-the-same-problem)
- [Loading rules compared](#loading-rules-compared)
- [Refinement: built, measured and removed](#refinement-built-measured-and-removed)
- [Parcels that never get delivered](#parcels-that-never-get-delivered)
- [Why parallelism is in scope and routing is not](#why-parallelism-is-in-scope-and-routing-is-not)
- [If routing were in scope](#if-routing-were-in-scope)
- [Other extensions](#other-extensions)
- [What the literature says](#what-the-literature-says)

## Other ways to solve the same problem

### Exact optimisation, rejected

A mixed-integer or constraint model with one binary per parcel-van pair:

| parcels | vans | binaries |
|---|---|---|
| 100 000 | 120 | 12 000 000 |
| 300 000 | 120 | 36 000 000 |
| 1 200 000 | 120 | 144 000 000 |

Beyond the size, the model is pathologically symmetric: the 120 vans are identical, so
every solution has 120! equivalent relabellings and a solver spends its time proving that
swapping two identical vans changes nothing. Solve times would also be unpredictable,
which matters more than their being long — a planner that usually takes two seconds and
occasionally takes four minutes cannot be put in a dispatch window.

### Dynamic programming, rejected

Pseudo-polynomial in capacity. The capacities here are 660 000 000 grams and 840 000 000
cubic centimetres, and any discretisation coarse enough to be tractable is too coarse to
be correct.

### Branch and bound for multiple knapsack, rejected

Martello and Toth's MTM and its successors solve this problem exactly and well — at a few
hundred items. Four orders of magnitude short.

### Metaheuristics over the whole pool, rejected

Genetic algorithms, simulated annealing, tabu search. Each candidate evaluation is linear
in the parcels, so within a window that fits a hundred passes over the data there is room
for perhaps a hundred generations on a population of one. A well-priced greedy reaches
99% or better of a provable ceiling on every demand mix here over twenty seeds, in a few
hundred milliseconds. There is nothing for a metaheuristic to
find, and its runtime would be far less predictable.

The one place this style of search does have something to offer is a narrow local pass
around the accept/reject boundary — see the next section.

### Column generation and branch-and-price, rejected

A natural fit for the set-partitioning view of the problem, and the pricing subproblem —
find the most attractive single van load — is itself a knapsack over the whole pool. Many
iterations of that, inside a branch-and-bound tree. Elegant and completely out of budget.

### Heuristic plus exact solver on a reduced core, not implemented

The most interesting rejected option. Run the fast planner, then extract the few thousand
parcels around the accept/reject boundary and hand *that* to a solver with a hard time
limit:

```
1 200 000 parcels
      ↓  fast planner
plan + the parcels it nearly took
      ↓  extract ~2 000 boundary parcels and the capacity they compete for
small exact model
      ↓  CP-SAT or MIP, one second ceiling
improved plan
```

This is how the approach would most likely be pushed further in production, because it
keeps the predictable runtime and spends the extra time only where the decision is
genuinely close. It is left out here because the measured headroom does not justify it:
every one of the ten demand mixes is already within four tenths of a per cent of a
ceiling nothing can exceed. Complexity should follow evidence, and the evidence says the boundary is not where
the money is — except on one pool.

## Loading rules compared

Which van a parcel goes into matters only where both capacities run out together. On
those days, rules that fill vans one at a time leave each van full on one limit and open on
the other, and that capacity is out of every later parcel's reach. Measured over five seeds
at unbounded budgets, against the balanced rule the planner uses:

| rule | `MixedExtremes` | `DenseGoods` | `LowMargin` | the other seven |
|---|---:|---:|---:|---:|
| balanced (kept) | 99.90% | 99.66% | 99.99% | about 99.99% |
| first-fit | 88.52% | 91.71% | 98.08% | about 99.99% |
| best-fit, on the tighter limit | 88.52% | 91.71% | 98.08% | about 99.99% |
| best-fit, on total room left | 88.52% | 91.71% | 98.08% | about 99.99% |

The three alternatives agree to the heller because, loading in price order, the vans fill
in index order: the fullest van that still fits and the first van that fits are the same
van. On `MixedExtremes` they leave 43% of the fleet's room unused. They are faster -
first-fit loads 300 000 parcels in about 20 ms against 74 ms - which does not make up for
losing up to twelve points of the ceiling. A different loading order - choosing the parcels first and packing them
largest first - was not tried.

## Refinement: built, measured and removed

`MixedExtremes` — duvets and car batteries in the same warehouse — is the one mix where
both limits bind at once. On prices alone it lands between 99.01% and 99.95% of the
ceiling over seeds 1 to 20, against 99.59% or better everywhere else.

**A one-for-one exchange across the accept/reject line was built.** It offered each of
the most profitable rejected parcels the cheapest loaded parcel whose removal would make
room for it, time-boxed, and every accepted swap raised the total. Over five days of
`MixedExtremes` it took the pool to between 99.81% and 99.95%, with up to 381 swaps; on
one day it found nothing, and on every other demand mix it found nothing on any day.

**It was removed.** It was around 450 lines with its tests, it needed a gate deciding
when to run, and it paid off on one synthetic mix by at most six tenths of a point. The
assignment asks for a solution that is fast and profitable, not maximally optimal, and a
planner without it is shorter to read and has one moving part fewer.

**Two further moves were not built.** Exchanging one parcel for two, or relocating parcels
between vans, might recover more on that one mix. Neither was measured.

**The test data misled twice on the way.** The pass was first rejected because earlier
synthetic data showed no headroom anywhere; that data could not express this case. Later
the catalogue was corrected from a flat two hundred articles per category to realistic
counts, and the pass's measured value fell sharply, because the flat count had been
drawing longer tails than such a category really has. The lesson is about test data at
least as much as about the algorithm, and it is why `plan --input` exists.

## Parcels that never get delivered

Maximising the yield of a single circuit is what the assignment asks for, and it is not
the same as running the operation well.

A parcel with a permanently poor yield per unit of room loses every time it is considered.
Not because anything is wrong with it, but because something better always arrives. Pure
yield maximisation has no mechanism to stop this: there is no term in the objective that
grows as a parcel waits, so nothing ever makes it competitive.

**How much this matters depends on facts the brief does not give.** It does not say
whether parcels left behind roll over to the next circuit, when new parcels arrive, or
whether there are days with enough slack to clear a backlog. If quiet days absorb what
busy days leave, the tail waits days rather than forever. If they do not, it waits
forever. Both are consistent with the brief.

**The fix needs no change to the planner**, which is a point in favour of how the
objective is separated from the search. Yield is just a number the planner maximises, so
an effective yield can be computed before calling it:

```
effectiveYield = yield + waitingBonus(daysWaiting)
```

Whether the bonus is linear, steps at an SLA threshold, or is driven by a customer
promise, is a business decision. The planner does not need to know why the number changed.

This is an extension beyond the assignment, which asks only about one circuit, so it is
described rather than built.

## Why parallelism is in scope and routing is not

Both are things the brief never names, and only one of them was built. The line between
them is worth stating, because it is the same line that decides every other "should this
be added" question here.

**The brief asks for speed and leaves the means open.** It gives a short computation
window — *"pro výpočet rozřazení balíčků do dodávek k dispozici poměrně krátké časové
okno. Program tedy nemůže výpočet provádět příliš dlouho"* — and it lists speed among
what is assessed: *"hodnotí se především přístup k řešení problému a způsob uvažování,
čitelnost kódu, rychlost řešení a profitabilita zvoleného postupu"*.

So parallelism needs no separate justification. It is not an extension of the problem; it
is one of the ways of meeting a requirement the brief states and grades. Routing is the
other case entirely: it would add a requirement the brief does not have, and every number
it produced would be a statement about invented geography.

**Where it is used.** The shortlisted price candidates are independent, so they are built
at the same time, and trying several sets of prices costs 1.3x to 1.5x what the
fixed-price plan costs - building that plan as a finalist included - rather than a multiple
of it per candidate. Estimating the candidates is parallel too. Loading the fleet
is effectively the whole cost of a plan, so that is where the concurrency is.

**Where it is deliberately capped, and why that is not timidity.** Estimating runs four
candidates at a time rather than one per core. The limit is memory, not cores: each worker
needs its own copy of the ranking's working arrays, about ten megabytes at a million
parcels, so giving all thirteen candidates a thread would spend a hundred and thirty
megabytes to save time that loading the fleet dwarfs. Four take most of the saving at a
third of the cost, and the figure was chosen by measurement rather than by
`Environment.ProcessorCount`.

That is the answer to "why not use every core": the scarce resource in this program is not
cores, it is memory proportional to the size of the pool, and a dispatch machine running
other work would rather have the megabytes back.

**What it assumes.** Spare cores. On a two-core machine the adaptive planner would cost
close to what the candidates cost serially, and the time budget rather than the core count
would decide how many were tried. At the default budget that did not happen: over 3 000
plans on two cores as on 32, the search was never cut short. If it were, the planner would
still build the fixed-price plan beside whatever it had estimated and keep the better, so a
smaller machine can lose the gain but not fall below fixed prices.

## If routing were in scope

The brief gives a parcel a weight, a volume and a yield, and nothing else — no
destination, no coordinates, no time. Routing is not ignored by choice; there is no data
from which to compute it. The title of the assignment, *delivery by teleport*, says so
directly.

It is worth being explicit about how much of this design rests on that, because the answer
is: nearly all of it.

**Vans being interchangeable is the load-bearing assumption.** It is why 120 vans can be
treated as one pooled capacity for the selection, why it does not matter which van a
parcel goes into, and why the problem splits into choosing and then loading. All of that
comes from there being no geography.

With destinations, the assumption falls first and takes the architecture with it:

- vans stop being interchangeable, because each serves a different set of lockers
- parcels going to the same locker want to travel together, so they stop being independent
- the objective becomes yield minus the cost of the distance driven, which needs a
  crowns-per-kilometre figure that would have to be invented
- a time limit per circuit appears, and it binds on stops rather than on capacity

The problem becomes a prize-collecting or team-orienteering vehicle routing problem,
which is a substantially harder class. Hundreds of thousands of parcels in a short window
would need a different approach entirely — cluster first by locker, then route, then fill.

None of that was implemented, because implementing it would mean inventing the lockers,
their positions, the distances between them and the cost of driving them. Every number
that came out would be a statement about invented geography.

## Other extensions

**Anything beyond one circuit.** Two variations were considered and neither is modelled.

Planning both daily circuits *together* would be a different objective: the brief asks for
the highest yield at each circuit, not across the day. It is also mechanically easy — the
fleet is a parameter, so 240 slots is a one-line change — which makes it tempting and
wrong.

Planning a second circuit over what the first left behind was built at one point and then
removed. It rests on two things the brief does not say — that the second circuit draws on
the remainder, and that nothing arrives between the two — so measuring it adds surface
without adding evidence about the problem that was actually set.

The thought behind it is worth a sentence. What a circuit leaves is the set of parcels
that lost the competition for space, so its composition *may* differ from the pool it came
from, and prices derived afresh can follow that where fixed ones cannot. Neither half of
that is a guarantee: the remainder can equally come out identical in shape - three parcels
alike, one carried, two left - and adapting to a change is not the same as earning more
for it.

Worth being exact about the comparison, too. The fixed-price planner does not carry over
prices chosen for the original pool; its prices come from the fleet's capacities alone and
never vary with what is waiting. The difference is that one adapts to composition and the
other does not consider it.

## What the literature says

The problem has a name. Cohen, Kulik and Shachnai study the **uniform two-dimensional
vector multiple knapsack** problem — items with a two-dimensional weight vector and a
positive profit, *m* identical bins of unit capacity in each dimension, choose a subset to
maximise profit. That is this problem exactly, with the capacities normalised.

Their result is a `1 − ln2/2 − ε ≈ 0.653 − ε` approximation, improving a previous
`1 − 1/e − ε ≈ 0.632`.[^1]

Two things are worth taking from that.

The best published worst-case guarantee for this problem is around 65% of optimum, and it
is obtained through a configuration LP with randomised rounding — an algorithm well outside
any dispatch window, and outside the complexity budget of a solution that has to be read
and maintained.

And a guarantee is a statement about the worst instance, not about the instances a
warehouse actually sees. The approach here offers no worst-case guarantee at all and
measures 99% or better of a provable ceiling on all ten realistic demand mixes over
twenty seeds,
because it exploits structure the general theory is not allowed to assume: parcels small
relative to a van, only 120 bins, and a hard runtime budget.

The honest summary is not that this beats the literature. It is that the two are answering
different questions.

[^1]: Tomer Cohen, Ariel Kulik, Hadas Shachnai, *Improved Approximation for Two-dimensional
Vector Multiple Knapsack*. ISAAC 2023 (LIPIcs vol. 283, art. 20); extended version in
*Computational Geometry: Theory and Applications*, 2025. [arXiv:2307.02137](https://arxiv.org/abs/2307.02137)
