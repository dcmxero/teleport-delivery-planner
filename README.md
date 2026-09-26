# Teleport Delivery Planner

Loading parcels into a fixed fleet of delivery vans so that the profit carried on a
single circuit is as high as possible.

A fleet of vans runs a circuit from a regional warehouse out to parcel lockers. On most
days there are more parcels waiting than the fleet can carry, so the planner decides
which ones travel and which van each goes into.

```
dotnet run -c Release --project src/TeleportDelivery.Cli -- plan
```

## In brief

The assignment asks for a short description; this section is it. The rest of this page is
the reference, and [RESULTS](docs/RESULTS.md) and [ALTERNATIVES](docs/ALTERNATIVES.md)
are the longer discussion the assignment says will happen at interview.

**The problem as I read it.** Choose which of hundreds of thousands of waiting parcels
travel on one circuit of 120 identical vans, and which van each goes into, so that the
yield carried is as high as possible. Each van holds 7 m³ and 5 500 kg; a parcel has a
weight, a volume and a yield, and cannot be split. Demand exceeds capacity on most days,
so the decision is what to leave behind. The planning window is short, so the answer has
to be produced in seconds rather than minutes.

This is the uniform two-dimensional vector multiple knapsack problem. It is NP-hard, so
the goal is not an optimum but a fast plan whose quality is *measured* rather than hoped
for.

**How I solve it.** Price the two capacities against each other, rank every parcel by
yield per unit of that price, and load in order. The whole question is what the prices
should be, because ranking by weight suits a day of car batteries and ranking by volume
suits a day of duvets, and a fixed answer is wrong on half the days. So the planner tries
a shortlist of price mixes on the actual pool, estimates each cheaply, builds the best few
properly and keeps the winner. Vans are filled by whichever of its two limits would end
up tighter, which keeps the fleet loading in step.

**How good it is.** Quality is reported against a ceiling from Lagrangian relaxation that
no plan can exceed, so "99.6% of the bound" means at most 0.4% was left on the table. On
seed 1 all ten demand mixes land at 99.6% or better; over seeds 1 to 20 the worst is
99.01% (`MixedExtremes`), with every other mix at 99.59% or better - provided the time
budget does not run out. That bounds how much any other method could add on these pools;
it is not a guarantee for an arbitrary input. Deriving prices from demand rather than
fixing them is worth up to 10.5% depending on the day, most where both limits bind, and
nothing on a day when everything fits. The 1 200 000-parcel Christmas peak is planned in
under 0.6 s.

**What I assumed.** Yield is the margin the retailer keeps, not the selling price -
ranking by price would fill the vans with expensive goods that earn little. Parcels are
indivisible and volumes simply add, with no 3D packing. Vans are interchangeable, which is
what lets the fleet be treated as one pooled capacity, and which holds only because the
brief gives a parcel no destination. One planning run is one circuit. The full list is
under [Assumptions](#assumptions).

**What I did not do.** No routing: the brief gives no destinations, so every number it
produced would describe invented geography. Nothing beyond a single circuit, and no
ageing of parcels left behind - both are extensions the brief does not ask for, described
in [ALTERNATIVES](docs/ALTERNATIVES.md) along with the exact methods that were rejected
and why.

## Contents

- [In brief](#in-brief)
- [The problem](#the-problem)
- [How I read the assignment](#how-i-read-the-assignment)
- [Approach](#approach)
- [Results](#results)
- [Speed](#speed)
- [Test data](#test-data)
- [Assumptions](#assumptions)
- [Limits](#limits)
- [Running it](#running-it)

## The problem

| | |
|---|---|
| Vans per warehouse | 120, identical, two circuits a day |
| Capacity per van | 7 m³ and 5 500 kg |
| Parcel attributes | weight, volume, yield |
| Input size | hundreds of thousands of parcels per planning run |
| Objective | maximise yield carried on one circuit |
| Constraint | the planning window is short |

## How I read the assignment

- **The day of the week is not an input.** The brief's Tuesdays and Thursdays say some runs
  are less constrained than others. What decides whether there is a choice to make is the
  demand in front of the planner, so that is what it reads.
- **Each circuit is a separate problem.** The brief asks for the highest yield *at each
  circuit*, so a run plans one circuit from what is waiting now.
- **Yield is what the retailer keeps, not what the customer pays.** A games console sells
  for twelve thousand crowns and earns a few hundred; ranking on price would fill the vans
  with goods that earn little. The algorithm maximises whatever is in that field, so a
  different meaning changes the plans, not the code.

## Approach

1. **Is there a decision at all?** One pass totals the demand. If it fits, everything is
   loaded - but fitting in total is not fitting into 120 vans (two vans of ten kilograms
   cannot take three parcels of six), so the loading is still done, and only a real
   failure falls through to the full path.
2. **Price the two capacities.** Ranking by yield per kilogram suits a day of duvets and
   fails on bottled water; per litre is the reverse. A price on each resource covers both.
   The planner tries thirteen splits, estimates each against pooled capacity, and builds
   full plans for the best few plus the best from each end of the range.
3. **Load the fleet evenly.** Each parcel goes to the van that stays least full on its
   tighter limit, so all 120 fill in step and neither capacity is stranded. A parcel that
   fits nowhere does not end the walk; something smaller further down will use the room.
4. **Order without sorting.** A positive double's bit pattern rises with its value, so a
   counting sort ranks the pool in linear time, with no comparisons and no allocation.

## Results

Both planners over the ten demand mixes, seed 1, release build. `baseline` charges the
same for weight and volume; `adaptive` works the prices out from the demand. The program
speaks Slovak, so the table does too, numbers included; the notes printed under it
explain every column.

```
┌────────────────────┬──────────────────┬──────────┬───────────────────────────────┬─────────┬──────────┬───────────────┬───────────┬──────┐
│ typ dňa            │ dopyt / kapacita │ plánovač │ zásielky                      │ zisk    │ z maxima │ využitie      │ cena váhy │ ms   │
│                    │   váha  objem    │          │     spolu  naložené    ostalo │ mil. Kč │          │   váha  objem │           │      │
├────────────────────┼──────────────────┼──────────┼───────────────────────────────┼─────────┼──────────┼───────────────┼───────────┼──────┤
│ Pokojný deň        │   0,07   0,19    │ baseline │   100 000   100 000         0 │  14,22  │ 100,00%  │   7,4%  18,9% │         - │   40 │
│                    │                  │ adaptive │   100 000   100 000         0 │  14,22  │ 100,00%  │   7,4%  18,9% │      fast │   31 │
├────────────────────┼──────────────────┼──────────┼───────────────────────────────┼─────────┼──────────┼───────────────┼───────────┼──────┤
│ Bežný deň          │   1,01   4,78    │ baseline │   300 000   184 582   115 418 │  71,70  │  99,68%  │  27,3% 100,0% │         - │  103 │
│                    │                  │ adaptive │   300 000   188 617   111 383 │  71,93  │ 100,00%  │  29,8% 100,0% │      0,00 │  141 │
├────────────────────┼──────────────────┼──────────┼───────────────────────────────┼─────────┼──────────┼───────────────┼───────────┼──────┤
│ Rušný deň          │   2,03   9,56    │ baseline │   600 000   272 736   327 264 │ 113,08  │  99,21%  │  30,3% 100,0% │         - │  158 │
│                    │                  │ adaptive │   600 000   285 950   314 050 │ 113,98  │ 100,00%  │  35,6% 100,0% │      0,00 │  210 │
├────────────────────┼──────────────────┼──────────┼───────────────────────────────┼─────────┼──────────┼───────────────┼───────────┼──────┤
│ Vianočná špička    │   2,20  11,83    │ baseline │ 1 200 000   391 474   808 526 │ 130,61  │  97,36%  │  24,0% 100,0% │         - │  273 │
│                    │                  │ adaptive │ 1 200 000   419 957   780 043 │ 134,15  │ 100,00%  │  38,1% 100,0% │      0,00 │  377 │
├────────────────────┼──────────────────┼──────────┼───────────────────────────────┼─────────┼──────────┼───────────────┼───────────┼──────┤
│ Objemný tovar      │   1,47  16,09    │ baseline │   300 000    43 248   256 752 │  36,95  │  99,51%  │  13,3% 100,0% │         - │   51 │
│                    │                  │ adaptive │   300 000    42 994   257 006 │  37,13  │  99,99%  │  15,7% 100,0% │      0,00 │   65 │
├────────────────────┼──────────────────┼──────────┼───────────────────────────────┼─────────┼──────────┼───────────────┼───────────┼──────┤
│ Ťažký tovar        │   5,19   3,26    │ baseline │   300 000   117 261   182 739 │  56,20  │  96,67%  │ 100,0%  80,7% │         - │   62 │
│                    │                  │ adaptive │   300 000   122 649   177 351 │  57,92  │  99,63%  │ 100,0%  96,2% │      0,75 │   83 │
├────────────────────┼──────────────────┼──────────┼───────────────────────────────┼─────────┼──────────┼───────────────┼───────────┼──────┤
│ Týždeň elektroniky │   1,68   3,26    │ baseline │ 1 000 000   422 881   577 119 │ 402,19  │  99,25%  │  46,5% 100,0% │         - │  277 │
│                    │                  │ adaptive │ 1 000 000   466 146   533 854 │ 405,24  │ 100,00%  │  51,1% 100,0% │      0,00 │  505 │
├────────────────────┼──────────────────┼──────────┼───────────────────────────────┼─────────┼──────────┼───────────────┼───────────┼──────┤
│ Zmes extrémov      │   6,82   6,89    │ baseline │   300 000    57 520   242 480 │  47,64  │  90,44%  │ 100,0%  57,1% │         - │   56 │
│                    │                  │ adaptive │   300 000    66 356   233 644 │  52,64  │  99,95%  │ 100,0%  99,8% │      0,75 │   75 │
├────────────────────┼──────────────────┼──────────┼───────────────────────────────┼─────────┼──────────┼───────────────┼───────────┼──────┤
│ Nízke marže        │   1,21   4,32    │ baseline │   300 000   222 753    77 247 │  16,54  │  98,28%  │  58,1% 100,0% │         - │   90 │
│                    │                  │ adaptive │   300 000   237 168    62 832 │  16,83  │  99,99%  │  93,0% 100,0% │      0,00 │  155 │
├────────────────────┼──────────────────┼──────────┼───────────────────────────────┼─────────┼──────────┼───────────────┼───────────┼──────┤
│ Výpredaj           │   1,57   8,16    │ baseline │   500 000   199 970   300 030 │  67,90  │  99,49%  │  18,7% 100,0% │         - │  122 │
│                    │                  │ adaptive │   500 000   212 810   287 190 │  68,24  │ 100,00%  │  21,3% 100,0% │      0,00 │  192 │
└────────────────────┴──────────────────┴──────────┴───────────────────────────────┴─────────┴──────────┴───────────────┴───────────┴──────┘
```

The rows name each day in Slovak; this document uses the `--mix` name: `QuietDay`,
`OrdinaryDay`, `BusyDay`, `PeakSeason`, `BulkyGoods`, `DenseGoods` (Ťažký tovar),
`ElectronicsWeek`, `MixedExtremes` (Zmes extrémov), `LowMargin`, `Clearance`, in that order.

- **`adaptive` never earns less** - by construction, since the fixed-price plan is always
  one of its finalists - and totals 15.24 million crowns more over the ten days for about
  0.6 s of extra wall-clock time in all. The gain is up to 10.5% - nothing when everything fits, most where both
  limits bind at once: 10.5% on `MixedExtremes`, 3.1% on dense goods, 2.7% on the Christmas peak.
- **`cena váhy` (the weight price) lands where it should without being told:** zero where
  room runs out first, 0.75 where payload does.
- **`dopyt / kapacita` (demand over capacity) says whether there was a choice at all.** Reaching the ceiling on a day that
  fits in the vans is no achievement; `MixedExtremes` waits at 6.8 times both capacities.
- **The time budget does not bind at the defaults**, measured on two cores as well as all
  32. The baseline is kept as the instrument for asking the same question of real data.

Why keep both planners, what the ceiling proves and how far each claim can be trusted: [RESULTS](docs/RESULTS.md).

## Speed

BenchmarkDotNet, i9-14900HX, .NET 10, 300 000 parcels unless stated.

Where the time goes:

| stage | mean | allocated |
|---|---:|---:|
| ordering the pool, counting sort | 0.9 ms | 0 B |
| ordering the pool, `Array.Sort` | 13.9 ms | 3.6 MB |
| loading the fleet, balanced rule | 74.0 ms | 3.1 MB |

Loading is effectively the entire cost of a plan. The shortlisted candidates are
independent, so they are built in parallel, the fixed-price plan among them: in the
benchmark run behind these tables the adaptive planner cost 1.3x to 1.5x the fixed-price one on days with a
choice to make, and about 0.9x on a quiet day, where the search is skipped.

Scaling, same demand mix:

| parcels | time | allocated |
|---|---:|---:|
| 100,000 | 45 ms | 25 MB |
| 300,000 | 116 ms | 58 MB |
| 600,000 | 194 ms | 118 MB |
| 1,200,000 | 377 ms | 209 MB |

Close to linear. A short benchmark job, so read the timings as the shape rather than to the
millisecond; allocation figures are exact.

The parallel gains need cores to spare. Confined to two cores, the Christmas peak takes
about 1.4 seconds rather than about half of one.

## Test data

The brief supplies none, so it is generated - from a fixed catalogue of some seven
thousand real-shaped articles in thirty categories ([`data/catalogue.csv`](data/catalogue.csv)),
not from distributions that could be tuned until the planner looks good. A day is only a
parcel count and a mix of categories. None of it has to be believed: `plan --input
parcels.csv` runs the same planners and bound on a real export. How the catalogue was built
is in [RESULTS](docs/RESULTS.md#test-data).

## Assumptions

Stated because the brief is deliberately open, and these are choices rather than facts.

| | |
|---|---|
| **Yield** | The margin the retailer keeps, not the selling price. Positive, as the brief permits. |
| **Parcels are indivisible** | One parcel goes entirely into one van or not at all. This is what makes the problem NP-hard; with divisible parcels it would be a linear programme, solvable exactly. |
| **One parcel holds one article** | A simplification in the generator. The planner sees only a parcel's weight, volume and yield, so a basket would reach it as one set of totals like any other; modelling baskets would need guesses the brief gives no basis for. |
| **Volume is additive** | No 3D packing of shapes, as the brief states. |
| **Routing is out of scope** | The brief gives a parcel no destination, so travel time cannot be modelled and does not depend on which parcels are loaded. The whole approach rests on vans being interchangeable, which rests on there being no geography. |
| **No deadlines or priorities** | Yield is the only value a parcel carries. |
| **One planning run is one circuit** | Of 120 vans. The brief's "twice daily" means the planner runs again, not that both circuits are planned together. |
| **Parcels left behind** | Returned as a separate list. What happens to them next is an operational decision the brief does not describe. |
| **Arrival timing** | Not specified in the brief and not modelled. |
| **Oversized parcels** | Nothing in the catalogue exceeds a van. A parcel that did would simply be left behind; there is no separate handling for it. |
| **Sub-second target** | My own engineering goal. The brief says only that the window is short. |

## Limits

- **Over seeds 1 to 20, `DenseGoods` reaches 99.59% to 99.78% of the ceiling and
  `MixedExtremes` 99.01% to 99.95%**, the two lowest. A pass trading parcels across the accept/reject
  line recovered up to 0.6 points on `MixedExtremes` and nothing elsewhere; it was built,
  measured and removed to keep the planner simple, see [ALTERNATIVES](docs/ALTERNATIVES.md).
  Whether the remaining fractions of a per cent are real or the ceiling is merely loose is
  not established.
- **Pure yield maximisation starves the tail.** A parcel with a permanently poor yield per
  unit of room loses every time, not because it is bad but because something better always
  arrives. The fix needs no change to the planner - add a waiting bonus to the yield before
  calling it - but whether it is needed depends on operational facts the brief does not
  give. Discussed in [ALTERNATIVES](docs/ALTERNATIVES.md).
- **The catalogue is a considered guess**, not Alza's data.

## Running it

Requires the .NET 10 SDK; `global.json` keeps the build on .NET 10. The planner has no
dependencies; the menu uses Spectre.Console.

```
dotnet build
dotnet test
dotnet run -c Release --project src/TeleportDelivery.Cli
```

With no arguments it offers a menu, and every choice prints the command line that does the
same thing. A redirected console gets the usage listing instead. Every word the console
prints is in one class, `Texts` - a static class rather than resource files, for
simplicity, since there is one language.

```
plan      [--mix <name|all>] [--count N] [--seed N] [--csv <path>]
plan      --input <path.csv>              plan a real pool instead
```

The benchmarks run with the command below, or from the menu's `BENCHMARKY`, which starts
the same command as a separate process, always in Release. Reports land in
`BenchmarkDotNet.Artifacts/results`.

```
dotnet run -c Release --project benchmarks/TeleportDelivery.Benchmarks -- --filter "*"
```

## Layout

```
src/TeleportDelivery.Core/Domain           parcels, vans, fleet, plan
src/TeleportDelivery.Core/Planning         the planners, ranking and bound
src/TeleportDelivery.Core/DataGeneration   catalogue, demand mixes, generator
src/TeleportDelivery.Cli                   command line runner
tests/TeleportDelivery.Tests               xUnit tests
benchmarks/TeleportDelivery.Benchmarks     BenchmarkDotNet suite
data/catalogue.csv                         the fixed assortment
docs/RESULTS.md                            the evidence behind the results
docs/ALTERNATIVES.md                       what was considered and rejected
```
