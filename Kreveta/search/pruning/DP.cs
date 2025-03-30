//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using System.Runtime.CompilerServices;

namespace Kreveta.search.pruning;

// DELTA PRUNING:
//
internal static class DP {
    private const int MIN_PLY = 4;
    private const int MARGIN_BASE = 81;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool CanPrune(bool only_captures, int ply, int cur_depth) {
        return PruningOptions.ALLOW_DELTA_PRUNING 
            && only_captures 
            && ply >= cur_depth + MIN_PLY;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool TryPrune(int ply, int cur_max_qs_depth, int col, Window window, short stand_pat, int captured) {
        int delta_margin = (cur_max_qs_depth - ply) * MARGIN_BASE * (col == 0 ? 1 : -1);

        return window.FailsLow((short)(stand_pat + captured + delta_margin), col);
    }
}
