# Results in detail

The [README](../README.md) gives the result table and what it shows. This is the evidence
behind it: why both planners are reported, what the quality figure does and does not
prove, how far each claim can be trusted, and where the test data comes from. It is the longer discussion the
assignment says belongs to the interview.

## Contents

- [Why keep the fixed-price planner at all?](#why-keep-the-fixed-price-planner-at-all)
- [How good is "good"?](#how-good-is-good)
- [What kind of claim is each of these?](#what-kind-of-claim-is-each-of-these)
- [Test data](#test-data)

## Why keep the fixed-price planner at all?

It is faster on nine days in ten, so the question is fair: yield and speed point at
different planners. The brief does not weigh them equally, though. Yield is what is
maximised - *"chceme při každém okruhu dosáhnout co nejvyšší výnosnosti"* - while the
computation window is a constraint, *"program nemůže výpočet provádět příliš dlouho"*. A
constraint is met or not met; being further inside it earns nothing.

Over the ten demand mixes, adaptive pricing totals 15.24 million crowns more, for about
0.6 s of extra wall-clock time across all ten circuits - 602 ms in the run the README
shows, 549 to 691 ms in three others. It never earns less by construction: the fixed-price
plan is always built as one of its finalists, and the best plan wins. The
fleet is a fixed cost - 120 vans and 120 drivers leave the warehouse either way - so the
marginal cost of the better plan is those milliseconds.

**What happens when the clock runs short.** The adaptive planner is time-boxed: the budget
limits how much search is started, and it does not impose a hard deadline on the whole run,
because a plan already begun is finished. Measured on seed 1, five runs at each budget:

| budget for the price search | `MixedExtremes` | `DenseGoods` | `PeakSeason` |
|---|---:|---:|---:|
| fixed prices, no search | 90.44% | 96.67% | 97.36% |
| 1 ms | 90.44% | 96.67% | 97.36% |
| 5 ms | 99.95% | 96.67% to 99.63% | 97.36% |
| 20 ms | 99.95% | 99.63% | 100.00% |

At one millisecond it estimates nothing and returns the fixed-price plan. At twenty it is at
full quality everywhere. In between, the outcome depends on which candidates were
estimated before the clock ran out, and that varies from run to run: over 200 runs of
`MixedExtremes` at five milliseconds, 196 reached 99.95% and the other four 90.44% to
91.77%. None fell below the fixed-price plan, because that plan is built beside the
finalists however little time is left. Before it was, three of 200 such runs did, the
worst at 78.78%.

**Does the default budget ever starve it?** Measured, not assumed: 3 000 plans over the ten
demand mixes, five seeds and three repetitions each, once on all 32 logical processors and
once confined to two. At the default 250 ms - and at 50 and 20 ms - all thirteen price
candidates were estimated every time, on two cores and at 1.2 million parcels included.
The fall back to fixed prices appeared only at budgets of about five milliseconds and
below. A variant that estimates every candidate regardless of the clock was compared
against the planner on the same parcels: it was never worse, better only at those tiny
budgets, and cost 40 to 155 ms more there. At the default settings the two gave
identical plans, so it was not adopted. How far the whole run can exceed the budget shows
on two cores: the Christmas peak takes about 1.4 seconds under a 250 ms budget.

That is also what the `baseline` row in the [results table](../README.md#results) is for. It is not a rival on
offer; it is the instrument that lets the same question be answered on real data through
`plan --input`, rather than on the pools invented here - because on six of these ten
days the gain is under one per cent, and whether that holds for Alza's own traffic is not
something this repository can know.

## How good is "good"?

`of bound` compares against a ceiling nothing can exceed, built by Lagrangian relaxation:
both capacity limits move into the objective at a price, so exceeding them is charged for
rather than forbidden, and for **any** non-negative prices the result is a valid ceiling.
That property is what makes searching for good ones safe - there is no way to choose
badly enough to produce a bound that is too low, only one that is loose.

Worth stating carefully, because it is easy to get backwards. A plan carries no more than
the optimum and the optimum is no more than the ceiling, so a plan at 98% of the ceiling
is at **at least** 98% of the optimum. Closeness to the ceiling does prove closeness to
the best possible plan.

It is the other direction that fails. A plan well short of the ceiling has not necessarily
missed anything, because the ceiling may simply be loose - the relaxation allows things no
real plan can do. The figure is a floor under how good a plan is, not a measure of how
much was lost.

## What kind of claim is each of these?

Three different things get stated in this document and they carry very different weight.
Keeping them apart is the difference between a result that can be defended and one that
merely sounds confident.

**Proved.** These follow from the mathematics and do not depend on the data.

- The ceiling is valid for *any* non-negative prices, which is what makes searching for
  good ones safe: no choice of them can produce a ceiling below the optimum. On a hundred
  and twenty instances small enough to enumerate, this is checked rather than only argued -
  a test finds the true optimum by trying every assignment and confirms the ceiling stays
  above it. That is a check on those instances, not a proof for all of them.
- A plan at 98% of the ceiling is at **at least** 98% of the optimum, because a plan
  carries no more than the optimum and the optimum no more than the ceiling.
- Demand fitting the fleet's pooled capacity is necessary but not sufficient for it to fit
  into 120 individual vans. Two vans of ten kilograms cannot take three parcels of six.
- Ranking by profit density is exact only for divisible items under a single limit. Parcels
  are indivisible and vans have two limits, which is what makes the problem NP-hard and why
  no ranking is guaranteed optimal.

**Measured.** These are facts about this code on this data, reproducible from a seed.

- Every figure in the results table and the speed tables.
- That no plan has some vans stopped on weight and others on volume. Vans stopped on one
  limit with the other barely used are common and correct - every van on an ordinary day
  is full of room at under a third of its payload.
- That the gain on the dense pool holds between 3.02% and 3.19% across eight seeds, so it
  is not an artefact of one draw.

**Assumed.** These are choices. Each could have gone another way, and the plans would
differ if it had.

- That yield means margin rather than price.
- That each circuit is planned on its own, over whatever is waiting.
- That routing is out of scope, which the brief's data makes unavoidable but does not state.
- That the catalogue resembles a real assortment.
- That a sub-second run is the target.

## Test data

The assignment supplies no data, so it has to be generated - and generated data is easy to
bend until the algorithm looks good. Two things guard against that.

**Parcels are articles from a fixed catalogue**, not points from a distribution. Some
seven thousand of them across thirty categories, in
[`data/catalogue.csv`](../data/catalogue.csv), where they can be read.

How many articles a category holds varies by a factor of nearly sixty, and that is not
decoration. It is anchored on what one Czech retailer publishes for its own categories -
1,811 laptops against 30 games consoles - and it matters because an earlier version of
this catalogue gave every category the same 200. That flat count was not merely arbitrary:
drawing 200 samples from a log-normal produces longer tails than drawing 55, so it quietly
handed the hardest demand mix more extreme parcels than such an assortment would really
hold, and some of the difficulty that mix was prized for was manufactured by the data.
Categories that retailer does not sell are set by analogy, and those are estimates and
nothing more.

The whole assortment can be checked against a single figure: it holds a **blended margin of
11.8%**. A general retailer reports a gross margin in
the low teens, so a catalogue landing far outside that would be describing a different kind
of business. An everyday parcel comes to about 2,500 crowns of goods earning about 330 of
margin. Weight, volume and margin are consequences of what a thing is - a
pillow is bulky, light and cheap because it is a pillow - and whether a boxed kettle really
weighs about two and a half kilograms is something anybody can check, in a way that
"log-normal with a spread of 0.85" is not.

**A day of demand is only two things**: how many parcels, and what is in them. Which
capacity runs out first stops being a parameter and becomes a consequence of what people
ordered. The awkward cases have to be plausible as shopping baskets, which is much harder
to rig than a correlation coefficient.

```
dotnet run -c Release --project src/TeleportDelivery.Cli -- plan
```

One place the assortment was chosen to reach a case rather than to describe a shop: the
payload limit can only bind for goods heavier than 0.79 kg per litre, the ratio of the
fleet's own two limits, and ordinary consumer goods do not reach it. Gym weights, car
batteries, pet food and paint were added so that the weight constraint could bind at all.
Without them half the planner would never have been exercised.

**And none of it has to be believed.** Point the planner at a real export and the same
code, the same bound and the same reporting run against it unchanged:

```
dotnet run -c Release --project src/TeleportDelivery.Cli -- plan --input parcels.csv
```
