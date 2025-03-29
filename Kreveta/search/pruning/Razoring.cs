﻿/*
 * |============================|
 * |                            |
 * |    Kreveta chess engine    |
 * | engineered by ZlomenyMesic |
 * | -------------------------- |
 * |      started 4-3-2025      |
 * | -------------------------- |
 * |                            |
 * | read README for additional |
 * | information about the code |
 * |    and usage that isn't    |
 * |  included in the comments  |
 * |                            |
 * |============================|
 */

using System.Runtime.CompilerServices;

namespace Kreveta.search.pruning;

internal static class Razoring {
    private const int MIN_PLY = 3;
    private const int DEPTH = 4;

    private const int QS_PLY = 2;
    private const int MARGIN_BASE = 185;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool CanReduce(int ply, int depth, bool in_check) {
        return PruningOptions.ALLOW_RAZORING 
            && !in_check 
            && ply >= MIN_PLY 
            && depth == DEPTH;
    }

    internal static bool TryReduce(Board b, int depth, int col, Window window) {
        short q_eval = PVSearch.QSearch(b, QS_PLY, window.GetLowerBound(col));

        int margin = MARGIN_BASE * depth * (col == 0 ? 1 : -1);

        return window.FailsLow((short)(q_eval + margin), col);
    }
}
