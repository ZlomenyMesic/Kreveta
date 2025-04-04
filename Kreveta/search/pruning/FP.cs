//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using System.Runtime.CompilerServices;

namespace Kreveta.search.pruning;

// FUTILITY PRUNING:
// we try to discard moves near the leaves which have no potential of raising alpha.
// futility margin represents the largest possible score gain through a single move.
// if we add this margin to the static eval of the position and still don't raise
// alpha, we can prune this branch. we assume there probably isn't a phenomenal move
// that could save this position
internal static class FP {

    // minimum ply and maximum depth to allow futility pruning
    private const int MIN_PLY = 3;
    private const int MAX_DEPTH = 5;

    // magical constant - DON'T MODIFY
    // higher margin => fewer reductions
    private const int FUTILITY_MARGIN_BASE = 88;

    // if not improving we make the margin smaller
    private const int IMPROVING_PENALTY = -10;

    // we have to meet certain conditions to allow futility pruning
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool CanPrune(int ply, int depth, bool interesting) {
        return PruningOptions.ALLOW_FUTILITY_PRUNING
            && ply >= MIN_PLY
            && depth <= MAX_DEPTH
            && !interesting;
    }

    // try futility pruning
    internal static bool TryPrune(int depth, Color col, short s_eval, Window window) {
        // as taken from chessprogrammingwiki:
        // "If at depth 1 the margin does not exceed the value of a minor piece, at
        // depth 2 it should be more like the value of a rook."
        //
        // however, a lower margin increases search speed and thus our futility margin stays low
        int margin = FUTILITY_MARGIN_BASE * (depth + 1) * (col == Color.WHITE ? 1 : -1);

        // if we failed low (fell under alpha). this means we already know of a better
        // alternative somewhere else in the search tree, and we can prune this branch.
        return window.FailsLow((short)(s_eval + margin), col);
    }
}