# Bug Report: Moisture Farm output caps

**Game version:** 0.46 (full release, Mono)
**Affected class:** `MachineMoistureFarm`
**Severity:** High — the machine silently produces nothing once the performance bonus gets high enough

---

## Summary

The Moisture Farm has two independent problems:

1. **`GetOutputVolume` overflows 32-bit integer arithmetic** at high performance bonuses. Past a certain bonus the method returns `0` instead of a large volume, so the machine produces no water at all.
2. **`Fill` routes everything through a fixed 6,000 mL `water_jug`**, so the output can never exceed 6 L no matter what container the player puts in the machine.

Both are silent — no error, no log, the tooltip just shows a small or zero number.

---

## Issue 1 — 32-bit overflow in `GetOutputVolume`

### Current code

```csharp
public static int GetOutputVolume(GameItem gameItem)
{
    int currentPerformanceBonus = MachineryHelper.GetCurrentPerformanceBonus(gameItem);
    return Math.Max(850000 * (100 + currentPerformanceBonus) / 100, 0);
}
```

### Analysis

`850000 * (100 + bonus)` is evaluated entirely in 32-bit signed arithmetic.

| bonus | `850000 * (100 + bonus)` | Result |
|---|---|---|
| 2426 | 2,147,100,000 (fits in int32) | 21,471,000 µL = **21.47 L** |
| 2427 | 2,147,950,000 → **overflows int32** | wraps negative → `Math.Max(..., 0)` = **0** |

So a performance bonus of 2427% or more makes the farm produce exactly nothing. The threshold is always ~21.47 L of output, because the result is `int.MaxValue / 100` µL.

Note that `100 + bonus` itself can also overflow if `bonus` is close to `int.MaxValue`, so the addition should be widened too.

### Suggested fix

```csharp
public static int GetOutputVolume(GameItem gameItem)
{
    int bonus = MachineryHelper.GetCurrentPerformanceBonus(gameItem);
    long volume = 850000L * ((long)bonus + 100) / 100;
    return (int)Math.Clamp(volume, 0, int.MaxValue);
}
```

See the note at the end about `ApplySharedAir` if you clamp to `int.MaxValue`.

---

## Issue 2 — output capped by the fixed transfer vessel

### Current code

```csharp
public static void Fill(string quality, GameItem container, int volume)
{
    GameItem gameItem = ItemSpawner.Spawn("water_jug");   // capacity 6,000 mL

    if (quality.Equals("Basewater")) WaterHelper.AddBaseWater(gameItem, volume);
    // ... other qualities ...

    WaterHelper.TransferLiquid(gameItem, container);
    gameItem.Destroy();
}
```

`water_jug` is defined in `FoodItemDirectory.WaterJug()`:

```csharp
WaterHelper.InitLiquidContainerItem(gameItem, 6000, 15, /* ... */);
```

### Analysis

The farm spawns a fixed 6,000 mL jug, pours the produced volume into it, and only then transfers to the player's container.

`WaterHelper.AddWater` caps the amount at the receiving container's capacity:

```csharp
int capacity = waterContainer.GetTagReadonly("LIQUID_CONTAINER_CAPACITY").GetInt();
int total    = /* sum of all liquid parts */;
int free     = capacity - total;
int add      = Math.Min(addAmount, free);
if (add <= 0) return;
```

Because the jug only holds 6,000 mL, at most 6 L of the produced volume survives. The size of the container the player placed in the machine is irrelevant — it never sees more than 6 L.

### Suggested fix

Two options, in order of preference:

**A. Fill the target container directly**

```csharp
WaterHelper.AddBaseWater(container, volume);
// ... drop the jug entirely
```

Caveat: when `container` already holds liquid, `AddWater` computes `free = capacity - total` and the contaminant split is based on the remaining space, so the result differs slightly from "fill a clean jug, then pour". Worth verifying that's acceptable.

**B. Keep the intermediate vessel, but size it to the target**

Spawn the jug, then copy the target's capacity onto it before adding water:

```csharp
GameItem jug = ItemSpawner.Spawn("water_jug");
int capacity = container.GetTagReadonly("LIQUID_CONTAINER_CAPACITY").GetInt();
jug.ModifyTag("LIQUID_CONTAINER_CAPACITY", s => s.SetInt(capacity), skipValidate: true);
```

This keeps the existing "mix in a clean vessel, then transfer" semantics and only removes the artificial 6 L ceiling.

---

## Additional note — `ApplySharedAir` float rounding

```csharp
public static int ApplySharedAir(int volume, int runningFarms)
{
    int num = Math.Max(1, runningFarms);
    return Mathf.RoundToInt((float)volume / (1f + 0.25f * (float)(num - 1)));
}
```

`Mathf.RoundToInt` is implemented as `(int)Math.Round(f)`. With a single farm running the divisor is `1f`, so the result is just `(int)(float)volume`.

`(float)int.MaxValue` rounds up to 2^31, and casting that back to `int` wraps to `int.MinValue` — a **negative** volume, which `AddWater` then rejects via `if (add <= 0) return;`.

Clamping to `2147483520` (the largest int32 exactly representable as a float) instead of `int.MaxValue` avoids this. It only matters in the extreme case, but it means clamping to `int.MaxValue` alone is not a complete fix.

---

## Reproduction

**Issue 1**

1. Place a Moisture Farm and install enough modules to reach a **2427% or higher** performance bonus.
2. Put any liquid container in the container slot.
3. Let the machine run for one night.
4. **Expected:** water is produced.
   **Actual:** 0 mL; the tooltip shows `0 毫升`.

**Issue 2**

1. Reach a bonus around **1000%** (so the raw output is ~9.35 L, well past 6 L).
2. Put a container larger than 6 L into the machine.
3. **Expected:** the container receives the full output.
   **Actual:** it receives at most 6 L.

---

## Workaround

A community mod (`ProbablyWaterMachineFix`) patches both methods:

- `GetOutputVolume` — recomputes the same formula in 64-bit and clamps the result
- `ItemSpawner.Spawn` — copies the target container's `LIQUID_CONTAINER_CAPACITY` onto the spawned transfer vessel

It is a workaround only; the fixes above are the proper solution.
