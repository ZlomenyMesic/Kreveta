//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using System.Runtime.CompilerServices;

namespace Kreveta.search.pruning;

// DELTA PRUNING:
//
internal static class DP {
    internal const int MinPly          = 4;
    private  const int DeltaMarginBase = 81;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool TryPrune(int ply, int curQSDepth, Color col, Window window, short standPat, int captured) {
        int delta = (curQSDepth - ply) * DeltaMarginBase 
            * (col == Color.WHITE ? 1 : -1);

        standPat += (short)captured;
        standPat += (short)delta;

        return col == Color.WHITE
            ? (standPat <= window.Alpha)
            : (standPat >= window.Beta);
    }
}
