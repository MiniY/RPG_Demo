using System;
using System.Collections.Generic;

/// <summary>Integer, deterministic allocation after the caller proves compatible placement capacity.</summary>
public static class EcologyPopulationBudget
{
    public static bool TryComputeDemand(long legalArea, int densityNumerator, int densityDenominator,
        int minimumGroup, int constructiveCapacity, int upperLimit, out int demand, out string failureCode)
    {
        demand = 0;
        failureCode = "InvalidRecipe";
        if (legalArea < 0 || densityNumerator < 0 || densityDenominator < 1 || minimumGroup < 1 ||
            upperLimit < minimumGroup)
            return false;
        if (constructiveCapacity < minimumGroup)
        {
            failureCode = "InsufficientConstructiveCapacity";
            return false;
        }
        try
        {
            long raw = checked(legalArea * densityNumerator) / densityDenominator;
            demand = checked((int)Math.Min(Math.Min(constructiveCapacity, upperLimit),
                Math.Max(minimumGroup, raw)));
            failureCode = null;
            return true;
        }
        catch (OverflowException)
        {
            failureCode = "NumericRangeExceeded";
            return false;
        }
    }

    public static bool TryAllocate(int minimumGroup, int globalBudget, int[] regionIds,
        int[] capacities, int[] desiredTargets, ulong[] stableScores,
        out int[] assigned, out string failureCode)
    {
        assigned = null;
        failureCode = "InvalidRecipe";
        if (minimumGroup < 1 || globalBudget < 0 || regionIds == null || capacities == null ||
            desiredTargets == null || stableScores == null || capacities.Length != regionIds.Length ||
            desiredTargets.Length != regionIds.Length || stableScores.Length != regionIds.Length)
            return false;

        int count = regionIds.Length;
        var ids = new HashSet<int>();
        var demands = new int[count];
        long baseline = 0, totalNeed = 0;
        try
        {
            for (int i = 0; i < count; i++)
            {
                if (regionIds[i] <= 0 || !ids.Add(regionIds[i]) || desiredTargets[i] < minimumGroup)
                    return false;
                if (capacities[i] < minimumGroup)
                {
                    failureCode = "InsufficientConstructiveCapacity";
                    return false;
                }
                demands[i] = Math.Min(capacities[i], desiredTargets[i]);
                baseline = checked(baseline + minimumGroup);
                totalNeed = checked(totalNeed + demands[i] - minimumGroup);
            }
            if (globalBudget < baseline)
            {
                failureCode = "InsufficientGroupBudget";
                return false;
            }

            var result = new int[count];
            long remaining = Math.Min(globalBudget - baseline, totalNeed);
            var remainders = new long[count];
            long used = 0;
            for (int i = 0; i < count; i++)
            {
                long product = checked(remaining * (demands[i] - minimumGroup));
                int extra = totalNeed == 0 ? 0 : checked((int)(product / totalNeed));
                remainders[i] = totalNeed == 0 ? 0 : product % totalNeed;
                result[i] = checked(minimumGroup + extra);
                used = checked(used + extra);
            }

            var order = new int[count];
            for (int i = 0; i < count; i++) order[i] = i;
            Array.Sort(order, (a, b) =>
            {
                int remainder = remainders[b].CompareTo(remainders[a]);
                if (remainder != 0) return remainder;
                int score = stableScores[a].CompareTo(stableScores[b]);
                return score != 0 ? score : regionIds[a].CompareTo(regionIds[b]);
            });
            long left = remaining - used;
            foreach (int i in order)
            {
                if (left == 0) break;
                if (result[i] >= demands[i]) continue;
                result[i]++;
                left--;
            }
            if (left != 0) throw new InvalidOperationException("Remainder allocation invariant failed.");
            assigned = result;
            failureCode = null;
            return true;
        }
        catch (OverflowException)
        {
            failureCode = "NumericRangeExceeded";
            return false;
        }
    }
}
