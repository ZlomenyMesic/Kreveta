//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

#pragma warning disable CA1031
#pragma warning disable CA1305

using Kreveta.search;
using Kreveta.search.pruning;
using Kreveta.uci;

using System;

namespace Kreveta.tuning;

internal static class Tuning {
    internal static void TuneParams(ReadOnlySpan<string> tokens) {
        /*try {
            FutilityPruning.MarginBase += int.Parse(tokens[1]);
            FutilityPruning.DepthMultiplier += int.Parse(tokens[2]);
            FutilityPruning.NotImprovingMargin += int.Parse(tokens[3]);
            FutilityPruning.SEEDivisor += int.Parse(tokens[4]);
            FutilityPruning.SEEClampDown += int.Parse(tokens[5]);
            
            FutilityPruning.SEEClampUp += int.Parse(tokens[6]);
            LateMoveReductions.MinSEEDeeper += int.Parse(tokens[7]);
            PVSearch.RazorConst1 += int.Parse(tokens[8]);
            PVSearch.RazorConst2 += int.Parse(tokens[9]);
            PVSearch.RazorConst3 += int.Parse(tokens[10]);
            
            DeltaPruning.DeltaDepthMultiplier += int.Parse(tokens[11]);
            DeltaPruning.CapturedMultiplier += int.Parse(tokens[12]);
        } 
        catch (Exception e) 
            when (UCI.LogException("Tuning params failed", e))
        { }*/
    }
}

#pragma warning restore CA1031
#pragma warning restore CA1305