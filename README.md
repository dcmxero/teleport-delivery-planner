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

- **A fixed price for weight and volume is wrong on half the days.** It was the first
  version, and it is kept as `baseline`. On `MixedExtremes`, where both limits bind, it
  reaches 90.44% of the ceiling; deriving the price from the day reaches 99.95%.
- **Fitting in total is not fitting in vans.** Two 10 kg vans cannot take three 6 kg
  parcels. The shortcut for quiet days therefore still loads the vans, and falls back to
  the full search when something is left over.
- **How good is good, without the optimum?** Hence the ceiling. It is checked against the
  exact optimum, found by brute force, on 120 small instances in the tests.
- **Could the adaptive planner earn less than the baseline?** A review found that it could:
  in one of 50 test runs, by 0.0085%, because it ranks by buckets and the baseline sorts
  exactly. The fixed-price plan is now built as one of the finalists, which makes the
  guarantee hold by construction, at a measured 10 to 18 ms and 13 MB per plan of 300 000.
- **Where does test data come from?** The brief supplies none. Parcels are drawn from a
  catalogue of about 7 000 real-shaped articles in 30 categories, not from distributions
  that could be tuned in the planner's favour. An earlier catalogue with a flat 200
  articles per category made one measurement misleading. `plan --input` runs the same code
  on a real export.
- **Is a refinement pass worth it?** One that swapped parcels across the accept/reject line
  was built. It gained up to 0.6 points on one synthetic day and nothing elsewhere, so it
  was removed to keep the planner simple.
- **How many cores?** Memory, not cores, is the limit: each worker needs its own ranking
  arrays, about 9 MB at a million parcels, so estimates run four at a time.
- **What if the time runs out?** The budget limits the work started. On an idle machine it
  never ran out at the default 250 ms, on two cores as on 32. On a heavily loaded one it
  can at a million parcels, and the plan then depends on how far the search got, though
  never below the fixed-price plan.
- **Open questions for Alza:** what "yield" really is; whether a parcel with a poor yield
  may wait indefinitely (a waiting bonus added to its yield would fix that without touching
  the planner); how long the window really is; whether destinations should ever matter.

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
