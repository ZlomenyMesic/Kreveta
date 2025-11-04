//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

#pragma warning disable CA1031
#pragma warning disable CA1305

using Kreveta.evaluation;
using Kreveta.moveorder.historyheuristics;
using Kreveta.search.pruning;
using Kreveta.uci;

using System;

namespace Kreveta.tuning;

internal static class Tuning {
    internal static void TuneParams(ReadOnlySpan<string> tokens) {
        try {
            FutilityPruning.MarginBase                += int.Parse(tokens[1]); // step 0-15
            FutilityPruning.DepthMultiplier           += int.Parse(tokens[2]); // step 0-12
            FutilityPruning.ImprovingMargin           += int.Parse(tokens[3]);
            FutilityPruning.NotImprovingMargin        += int.Parse(tokens[4]);
            NullMovePruning.MinAddRedDepth            += int.Parse(tokens[5]);
            
            NullMovePruning.AddDepthDivisor           += int.Parse(tokens[6]);
            LateMoveReductions.HistReductionThreshold += short.Parse(tokens[7]); // step 0-100
            LateMoveReductions.MaxReduceMargin        += sbyte.Parse(tokens[8]); // step 0-10
            LateMoveReductions.WindowSizeDivisor      += sbyte.Parse(tokens[9]);
            LateMoveReductions.MarginDivisor          += sbyte.Parse(tokens[10]);
            
            LateMoveReductions.ImprovingMargin        += sbyte.Parse(tokens[11]);
            DeltaPruning.DeltaMarginBase              += int.Parse(tokens[12]); // step 0-15
            PawnCorrectionHistory.CorrScale           += short.Parse(tokens[13]); // step 0-20
            QuietHistory.RelHHScale                   += short.Parse(tokens[14]);
            QuietHistory.ShiftSubtract                += short.Parse(tokens[15]);
            
            QuietHistory.ShiftLimit                   += short.Parse(tokens[16]); // step 0-15
            Eval.SideToMoveBonus                      += sbyte.Parse(tokens[17]);
            Eval.DoubledPawnPenalty                   += sbyte.Parse(tokens[18]);
            Eval.IsolatedPawnPenalty                  += sbyte.Parse(tokens[19]);
            Eval.IsolaniAddPenalty                    += sbyte.Parse(tokens[20]);
            
            Eval.ConnectedPassedPawnBonus             += sbyte.Parse(tokens[21]);
            Eval.BlockedPawnPenalty                   += sbyte.Parse(tokens[22]);
            Eval.BishopPairBonus                      += sbyte.Parse(tokens[23]);
            Eval.OpenFileRookBonus                    += sbyte.Parse(tokens[24]);
            Eval.SemiOpenFileRookBonus                += sbyte.Parse(tokens[25]);
        } 
        catch (Exception e) 
            when (UCI.LogException("Tuning params failed", e)) 
        { }
    }
}

#pragma warning restore CA1031
#pragma warning restore CA1305