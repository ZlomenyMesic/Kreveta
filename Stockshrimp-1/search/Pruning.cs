/*
 *  Stockshrimp chess engine 1.0
 *  developed by ZlomenyMesic
 */

using System.Runtime.CompilerServices;

namespace Stockshrimp_1.search;

// NULL MOVE PRUNING
internal static class NMP {

    // minimal depth and ply needed to allow nullmp
    internal const int MIN_DEPTH = 0;
    internal const int MIN_PLY = 2;

    // depth at which nmp goes straight into qsearch
    internal const int DROP_INTO_QS = 2;

    // depth reduce within nullmp
    internal const int R = 3;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int GetR(int ply) {
        if (ply <= 4) return R - 1;
        //if (ply >= 8) return R + 1;
        return R;
    }
}

// FUTILITY PRUNING
internal static class FP {

    // minimum ply and maximum depth to allow futility pruning
    internal const int MIN_PLY = 3;
    internal const int MAX_DEPTH = 5;

    // magical constant - DON'T MODIFY
    // higher margin => fewer reductions
    internal const int FUTILITY_MARGIN_BASE = 58;

    // if not improving we make the margin smaller
    internal const int IMPROVING_PENALTY = -10;

    // returns the margin which could potentialy raise alpha when added to the score
    internal static int GetMargin(int depth, int col, bool improving) {
        int margin = FUTILITY_MARGIN_BASE * depth 
            + (improving ? 0 : IMPROVING_PENALTY);

        return margin * (col == 0 ? 1 : -1);
    }
}

// LATE MOVE REDUCTIONS
internal static class LMR {

    // once again we set a minimum ply and depth
    internal const int MIN_PLY = 4;
    internal const int MIN_DEPTH = 0;

    // minimum nodes expanded before lmr
    // (we obviously don't want to reduce the pv)
    internal const int MIN_EXP_NODES = 3;

    // depth reduce
    internal const int R = 3;
}