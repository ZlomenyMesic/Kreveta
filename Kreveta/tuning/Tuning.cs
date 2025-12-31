//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

#pragma warning disable CA1031
#pragma warning disable CA1305

using Kreveta.evaluation;

using System;

namespace Kreveta.tuning;

internal static class Tuning {
    
    internal static void TuneParams(ReadOnlySpan<string> tokens) {
        Eval.SideToMoveBonus += int.Parse(tokens[1]);
        Eval.InCheckMalus += int.Parse(tokens[2]);
        
        Eval.DoubledPawnMalus += int.Parse(tokens[3]);
        Eval.IsolatedPawnMalus += int.Parse(tokens[4]);
        Eval.PassedPawnBonus += int.Parse(tokens[5]);
        Eval.BlockedPawnMalus += int.Parse(tokens[6]);
        
        for (int i = 0; i < 384; i++)
            EvalTables.Middlegame[i] += short.Parse(tokens[i + 1 + 6]);
        
        for (int i = 0; i < 384; i++)
            EvalTables.Endgame[i] += short.Parse(tokens[i + 1 + 384 + 6]);
    }
}

#pragma warning restore CA1031
#pragma warning restore CA1305