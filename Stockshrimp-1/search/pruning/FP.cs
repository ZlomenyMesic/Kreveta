/*
 *  Stockshrimp chess engine 1.0
 *  developed by ZlomenyMesic
 */

using Stockshrimp_1.evaluation;

namespace Stockshrimp_1.search.pruning;

// FUTILITY PRUNING
// we try to discard moves near the leaves which have no potential of raising alpha.
// futility margin represents the largest possible score gain through a single move.
// if we add this margin to the static eval of the position and still don't raise
// alpha, we can prune this branch. we assume there probably isn't a phenomenal move
// that could save this position
internal static class FP {

    // minimum ply and maximum depth to allow futility pruning
    internal const int MIN_PLY = 3;
    internal const int MAX_DEPTH = 5;

    // magical constant - DON'T MODIFY
    // higher margin => fewer reductions
    internal const int FUTILITY_MARGIN_BASE = 88;

    // if not improving we make the margin smaller
    internal const int IMPROVING_PENALTY = -10;

    // returns the margin which could potentialy raise alpha when added to the score
    internal static int GetMargin(int depth, int col, bool improving) {
        int margin = FUTILITY_MARGIN_BASE * (depth + 1);
        //+ (improving ? 0 : IMPROVING_PENALTY);

        return margin * (col == 0 ? 1 : -1);
    }

    // we have to meet certain conditions to allow futility pruning
    internal static bool CanPrune(int ply, int depth, bool interesting) {
        return ply >= MIN_PLY
            && depth <= MAX_DEPTH
            && !interesting;
    }

    // try futility pruning
    internal static bool TryPrune(int depth, int col, short s_eval, Window window) {
        // as taken from chessprogrammingwiki:
        // "If at depth 1 the margin does not exceed the value of a minor piece, at
        // depth 2 it should be more like the value of a rook."
        //
        // however, a lower margin increases search speed and thus our futility margin stays low
        int margin = GetMargin(depth, col, true);

        // if we failed low (fell under alpha). this means we already know of a better
        // alternative somewhere else in the search tree, and we can prune this branch.
        return window.FailsLow((short)(s_eval + margin), col);
    }
}