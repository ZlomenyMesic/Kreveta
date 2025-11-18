//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

#pragma warning disable CA1031
#pragma warning disable CA1305

using System;

namespace Kreveta.tuning;

internal static class Tuning {
    internal static void TuneParams(ReadOnlySpan<string> tokens) {
        /*try {
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
            DeltaPruning.DeltaDepthMultiplier         += int.Parse(tokens[12]); // step 0-15
            PawnCorrectionHistory.CorrScale           += short.Parse(tokens[13]); // step 0-20
            QuietHistory.RelHHScale                   += short.Parse(tokens[14]);
            QuietHistory.ShiftSubtract                += short.Parse(tokens[15]);
            
            QuietHistory.ShiftLimit                   += short.Parse(tokens[16]); // step 0-15
            DeltaPruning.DeltaMarginBase              += int.Parse(tokens[17]); // step 40
            DeltaPruning.CapturedMultiplier           += int.Parse(tokens[18]); // step 30
            LateMoveReductions.MarginBase             += sbyte.Parse(tokens[19]); // step 15
            LateMoveReductions.SearchedMovesMult      += sbyte.Parse(tokens[20]); // step 50
            
            NullMovePruning.PieceDivisor              += int.Parse(tokens[21]);
        } 
        catch (Exception e) 
            when (UCI.LogException("Tuning params failed", e)) 
        { }*/
    }

    internal static void ShiftParams() {
        /*FutilityPruning.MarginBase                += 0;
        FutilityPruning.DepthMultiplier           += 0;
        FutilityPruning.ImprovingMargin           += 0;
        FutilityPruning.NotImprovingMargin        += 0;
        NullMovePruning.MinAddRedDepth            += 0;

        NullMovePruning.AddDepthDivisor           += 0;
        LateMoveReductions.HistReductionThreshold += 0;
        LateMoveReductions.MaxReduceMargin        += 0;
        LateMoveReductions.WindowSizeDivisor      += 0;
        LateMoveReductions.MarginDivisor          += 0;

        LateMoveReductions.ImprovingMargin        += 0;
        DeltaPruning.DeltaDepthMultiplier         += 0;
        PawnCorrectionHistory.CorrScale           += 0;
        QuietHistory.RelHHScale                   += 0;
        QuietHistory.ShiftSubtract                += 0;

        QuietHistory.ShiftLimit                   += 0;
        Eval.SideToMoveBonus                      += 0;
        Eval.DoubledPawnPenalty                   += 0;
        Eval.IsolatedPawnPenalty                  += 0;
        Eval.IsolaniAddPenalty                    += 0;

        Eval.ConnectedPassedPawnBonus             += 0;
        Eval.BlockedPawnPenalty                   += 0;
        Eval.BishopPairBonus                      += 0;
        Eval.OpenFileRookBonus                    += 0;
        Eval.SemiOpenFileRookBonus                += 0;

        DeltaPruning.DeltaMarginBase              += 0;
        DeltaPruning.CapturedMultiplier           += 0;
        LateMoveReductions.MarginBase             += 0;
        LateMoveReductions.SearchedMovesMult      += 0;
        NullMovePruning.PieceDivisor              += 0;*/
    }
}

#pragma warning restore CA1031
#pragma warning restore CA1305