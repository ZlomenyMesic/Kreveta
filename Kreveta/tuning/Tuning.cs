//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

#pragma warning disable CA1031
#pragma warning disable CA1305

using Kreveta.evaluation;
using Kreveta.moveorder;
using Kreveta.search;

using System;

namespace Kreveta.tuning;

internal static class Tuning {
    internal static ulong TotalCutoffs     = 0UL;
    internal static ulong TotalCutoffScore = 0UL;
    
    internal static void TuneParams(ReadOnlySpan<string> tokens) {
        LazyMoveOrder.const1 = int.Parse(tokens[1]);
        LazyMoveOrder.const2 = int.Parse(tokens[2]);
        LazyMoveOrder.const3 = int.Parse(tokens[3]);
        LazyMoveOrder.const4 = int.Parse(tokens[4]);
        LazyMoveOrder.const5 = int.Parse(tokens[5]);
        LazyMoveOrder.const6 = int.Parse(tokens[6]);
        LazyMoveOrder.const7 = int.Parse(tokens[7]);
        LazyMoveOrder.const8 = int.Parse(tokens[8]);
        LazyMoveOrder.const9 = int.Parse(tokens[9]);
        LazyMoveOrder.const10 = int.Parse(tokens[10]);
        LazyMoveOrder.const11 = int.Parse(tokens[11]);
        LazyMoveOrder.const12 = int.Parse(tokens[12]);
        LazyMoveOrder.const13 = int.Parse(tokens[13]);
        LazyMoveOrder.const14 = int.Parse(tokens[14]);
        LazyMoveOrder.const15 = int.Parse(tokens[15]);
    }
}

#pragma warning restore CA1031
#pragma warning restore CA1305