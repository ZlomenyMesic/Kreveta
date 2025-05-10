//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.consts;
using Kreveta.moveorder;

using System.ComponentModel;
using System.Runtime.InteropServices;

// ReSharper disable InconsistentNaming

namespace Kreveta.search.pruning;

// we try to discard moves near the leaves, which have no potential of raising alpha.
// futility margin represents the largest possible score gain through a single move.
// if we add this margin to the static eval of the position and still don't raise
// alpha, we can prune this branch. we assume there probably isn't a phenomenal move
// that could save this position
internal static class FutilityPruning {

    // minimum ply and maximum depth to allow futility pruning
    internal const int MinPly   = 3;
    internal const int MaxDepth = 5;

    // magical constant - DON'T MODIFY
    // higher margin => fewer reductions
    private const int FutilityMarginBase       = 66;
    private const int FutilityMarginMultiplier = 102;

    // if improving we make the margin smaller
    private const int ImprovingMargin          = -35;
    private const int NotImprovingMargin       = 23; 

    // try futility pruning
    internal static bool TryPrune([In, ReadOnly(true)] in Board board, int depth, Color col, short staticEval, bool improving, Window window) {

        int pawnCorrection = PawnCorrectionHistory.GetCorrection(board) * (col == Color.WHITE ? -2 : 2);
        int _improving     = improving ? ImprovingMargin : NotImprovingMargin;

        // as taken from chessprogrammingwiki:
        // "If at depth 1 the margin does not exceed the value of a minor piece, at
        // depth 2 it should be more like the value of a rook."
        // we don't really follow this exactly, but our approach is kind of similar
        int margin = (FutilityMarginBase + pawnCorrection + _improving + FutilityMarginMultiplier * depth) * (col == Color.WHITE ? 1 : -1);

        // if we failed low (fell under alpha). this means we already know of a better
        // alternative somewhere else in the search tree, and we can prune this branch.
        staticEval += (short)margin;
        return col == Color.WHITE
            ? staticEval <= window.Alpha
            : staticEval >= window.Beta;
    }
}