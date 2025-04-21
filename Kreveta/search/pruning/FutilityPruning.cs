//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.search.moveorder;
using System.Diagnostics.CodeAnalysis;

namespace Kreveta.search.pruning;

// FUTILITY PRUNING:
// we try to discard moves near the leaves which have no potential of raising alpha.
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

    // if not improving we make the margin smaller
    private const int ImprovingPenalty = -10;

    // try futility pruning
    internal static bool TryPrune([NotNull] in Board board, int depth, Color col, short staticEval, Window window) {

        int pawnCorrection = PawnCorrectionHistory.GetCorrection(board) * (col == Color.WHITE ? -2 : 2);

        // as taken from chessprogrammingwiki:
        // "If at depth 1 the margin does not exceed the value of a minor piece, at
        // depth 2 it should be more like the value of a rook."
        //
        // however, a lower margin increases search speed and thus our futility margin stays low
        int margin = (FutilityMarginBase + pawnCorrection + FutilityMarginMultiplier * depth) * (col == Color.WHITE ? 1 : -1);

        // if we failed low (fell under alpha). this means we already know of a better
        // alternative somewhere else in the search tree, and we can prune this branch.
        staticEval += (short)margin;
        return col == Color.WHITE
            ? (staticEval <= window.Alpha)
            : (staticEval >= window.Beta);
    }
}