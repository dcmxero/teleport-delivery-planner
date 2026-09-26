# Teleport Delivery Planner

Chooses which waiting parcels travel on one circuit of 120 delivery vans, and which van
each goes into, so that the yield carried is as high as possible.

```
dotnet run -c Release --project src/TeleportDelivery.Cli
```

## The assignment

Parcels for AlzaBoxes leave a regional warehouse in small vans that drive a circuit twice a
day. The warehouse has 120 vans, each taking 7 m³ and 5.5 t. A parcel has a weight, a
volume and a yield in crowns. Except on Tuesdays and Thursdays there are more parcels than
room, so every circuit should carry the highest possible yield. The planning window is
short and a run holds hundreds of thousands of parcels.

The brief asks for C# code and a short text on how the problem was understood, how it was
solved and what was assumed. It grades the approach and reasoning, readability, speed and
profit, and says the solution need not be optimal, only fast and profitable enough.

## How I understood it

- **It is a two-dimensional multiple knapsack.** 120 identical bins, two capacities,
  indivisible items. That is NP-hard, and hundreds of thousands of items in a short window
  rule out exact solvers. The aim is a near-linear heuristic whose quality is *measured*.
- **Either limit can be the one that runs out.** Duvets fill a van long before its payload;
  car batteries reach 5.5 t with room to spare. Which one binds depends on the day's goods.
- **The weekday is not an input.** Tuesdays and Thursdays only mean less demand, so the
  planner reads the demand: when everything fits, there is nothing to choose.
- **Vans are interchangeable.** The brief gives parcels no destination, so there is no
  routing, and the fleet can be treated as one pooled capacity for estimates and bounds.
- **Yield is the margin the retailer keeps**, not the price: a games console sells for
  twelve thousand crowns and earns a few hundred.

## Solution

1. **Skip the choice when there is none.** If the whole pool fits into the vans, it is
   loaded without choosing.
2. **Price weight against volume.** Parcels are ranked by yield per unit of priced
   capacity. Thirteen price splits are estimated on the day's parcels, the best few get a
   full plan in parallel, and the most profitable plan wins. The fixed-price plan is always
   among them, so the result is never worse than fixed prices.
3. **Load the fleet evenly.** Each parcel goes to the van left least full on its tighter
   limit, so neither capacity is stranded.
4. **Rank without sorting.** A counting sort on the bits of each score orders 300 000
   parcels in about a millisecond, against 14 ms for `Array.Sort`.

Quality is reported against a ceiling from Lagrangian relaxation that no plan can exceed:
99% of it means at least 99% of the best possible plan.

## Problems and questions along the way

- **Should weight and volume always cost the same?** That was the first version, kept as
  `baseline`. It loses under 1% on six of the ten days, but up to 10.5% when the goods are
  lopsided. On `MixedExtremes` (duvets and car batteries) it fills the payload with
  batteries and leaves 43% of the room empty. Working the price out from the day's parcels
  fills both limits and reaches 99.95% of the ceiling instead of 90.44%.
- **If everything fits in total, does it fit in the vans?** Not always: two vans that take
  10 kg each cannot carry three 6 kg parcels. So on quiet days the planner still loads van
  by van, and if a parcel is left over it plans the day in full instead.
- **How do we know a plan is good without knowing the best one?** The planner computes an
  upper limit that no plan can beat and reports profit as a share of it. The tests check
  that limit against the true best plan, found by trying every combination, on 120 small
  cases.
- **Can the smarter planner ever earn less than the simple one?** It could, by a hair: in
  one of 50 test runs it earned 0.0085% less, because it sorts parcels slightly less
  precisely to save time. Now it always builds the simple planner's plan as well and keeps
  the better one. That costs 10 to 18 ms and 13 MB per plan of 300 000 parcels.
- **Where does test data come from?** The brief gives none. Parcels are generated from a
  catalogue of about 7 000 made-up but realistic articles in 30 categories, from books and
  laptops to duvets and car batteries. An early catalogue with 200 articles in every
  category gave one misleading result. `plan --input` runs the same code on a real export.
- **Is it worth improving the plan afterwards?** A pass that swapped loaded parcels for
  better ones left behind was built and measured. It helped on one test day only, by up to
  0.6 percentage points, so it was removed to keep the code simple.
- **Why not use every processor core?** Memory is the limit, not cores: each parallel task
  needs about 9 MB of its own at a million parcels, so the price estimates run four at a
  time.
- **What if planning runs out of time?** The time limit, 250 ms by default, stops new work
  from starting; work already started is finished. On an idle machine the limit was never
  reached, even on two cores. On a very busy machine it can be reached at a million
  parcels; the plan is then less good than usual, but never worse than the simple one.
- **Questions to ask Alza.** What exactly is "yield"? May a low-yield parcel be left behind
  day after day? Adding a bonus for every day it waits would fix that without changing the
  planner. How long is the planning window really? Will delivery addresses ever matter?

The evidence in detail: [RESULTS](docs/RESULTS.md). Methods considered and not used,
including exact solvers and metaheuristics: [ALTERNATIVES](docs/ALTERNATIVES.md).

## Results

Seed 1. `baseline` uses fixed prices, `adaptive` derives them from the day's parcels.

| day | parcels | baseline, % of ceiling | adaptive, % of ceiling | profit gained |
|---|---:|---:|---:|---:|
| QuietDay | 100 000 | 100.00% | 100.00% | 0.0% |
| OrdinaryDay | 300 000 | 99.68% | 100.00% | 0.3% |
| BusyDay | 600 000 | 99.21% | 100.00% | 0.8% |
| PeakSeason | 1 200 000 | 97.36% | 100.00% | 2.7% |
| BulkyGoods | 300 000 | 99.51% | 99.99% | 0.5% |
| DenseGoods | 300 000 | 96.67% | 99.63% | 3.1% |
| ElectronicsWeek | 1 000 000 | 99.25% | 100.00% | 0.8% |
| MixedExtremes | 300 000 | 90.44% | 99.95% | 10.5% |
| LowMargin | 300 000 | 98.28% | 99.99% | 1.7% |
| Clearance | 500 000 | 99.49% | 100.00% | 0.5% |

Over the ten days `adaptive` earns 15.24 million crowns more. Over seeds 1 to 20 its worst
result is 99.01% of the ceiling (`MixedExtremes`); every other day stays at 99.59% or
better. The console prints the full table with a legend in Slovak.

## Speed

BenchmarkDotNet, i9-14900HX, .NET 10, one ordinary day scaled up:

| parcels | time | allocated |
|---:|---:|---:|
| 100 000 | 45 ms | 25 MB |
| 300 000 | 116 ms | 58 MB |
| 600 000 | 194 ms | 118 MB |
| 1 200 000 | 377 ms | 209 MB |

Loading the vans is nearly all of the time. The adaptive planner costs 1.3x to 1.5x the
fixed-price one; on two cores the Christmas peak takes about 1.4 s instead of about 0.5 s.

## Assumptions

- Yield is positive, as the brief allows.
- Volumes add up; there is no 3D packing.
- One run plans one circuit from what is waiting; parcels left behind are returned.
- A parcel in the generated data holds one article; the planner only sees totals.
- Under a second per plan is my own target; the brief only says the window is short.

## Running it

Requires the .NET 10 SDK.

```
dotnet build
dotnet test
dotnet run -c Release --project src/TeleportDelivery.Cli
```

Without arguments the program offers a menu in Slovak and prints the command line for each
choice:

```
plan  [--mix <name|all>] [--count N] [--seed N] [--csv <path>]
plan  --input <path.csv>
```

Benchmarks, also available from the menu:

```
dotnet run -c Release --project benchmarks/TeleportDelivery.Benchmarks -- --filter "*"
```

## Layout

```
src/TeleportDelivery.Core/Domain           parcels, vans, fleet, plan
src/TeleportDelivery.Core/Planning         the planners, ranking and ceiling
src/TeleportDelivery.Core/DataGeneration   catalogue, demand mixes, generator
src/TeleportDelivery.Cli                   console runner
tests/TeleportDelivery.Tests               xUnit tests
benchmarks/TeleportDelivery.Benchmarks     BenchmarkDotNet suite
data/catalogue.csv                         the generated assortment, for reading
docs/                                      results and alternatives in detail
```
