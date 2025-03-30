//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.evaluation;
using Kreveta.movegen;
using Kreveta.search.moveorder;
using System.Runtime.CompilerServices;

namespace Kreveta.search.pruning;

// LATE MOVE PRUNING/REDUCTIONS:
// moves other than the pv node are expected to fail low (not raise alpha),
// so we first search them with null window around alpha at a reduced depth.
// if it does not fail low as expected, we try to fail low once again but with
// a margin (this only applies to moves with bad history rep). if we fail low
// with a margin, we only reduce the search depth. if we don't fail low at all
// despite the margin, we do a full re-search
internal static class LMR {

    // once again we set a minimum ply and depth
    private const int MIN_PLY = 4;
    private const int MIN_DEPTH = 0;

    // minimum nodes expanded before lmr
    // (we obviously don't want to reduce the pv)
    private const int MIN_EXP_NODES = 3;

    // when a move's history rep falls below this threshold,
    // we use a larger R (we assume the move isn't that good
    // and save some time by not searching it that deeply)
    private const int HH_THRESHOLD = -1320;

    // depth reduce normally and with bad history rep
    private const int R = 3;
    private const int HH_R = 4;

    private const int MAX_REDUCE_MARGIN = 66;
    private const int WINDOW_SIZE_DIVISOR = 10;
    private const int MARGIN_DIVISOR = 5;

    private const int REDUCTION_DEPTH = 4;

    // again a couple conditions
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool CanPruneOrReduce(int ply, int depth, int exp_nodes, bool interesting) {
        return (PruningOptions.ALLOW_LATE_MOVE_PRUNING || PruningOptions.ALLOW_LATE_MOVE_REDUCTIONS)
            && !interesting
            && ply >= MIN_PLY
            && depth >= MIN_DEPTH
            && exp_nodes >= MIN_EXP_NODES;
    }

    // should we prune or reduce?
    internal static (bool Prune, bool Reduce) TryPrune(Board child, Move move, int ply, int depth, int col, int exp_nodes, Window window) {

        // depth reduce is larger with bad history
        int R = History.GetRep(child, move) < HH_THRESHOLD ? HH_R : LMR.R;

        // null window around alpha
        Window nullw_alpha = window.GetLowerBound(col);

        // once again a reduced depth search
        short score = PVSearch.ProbeTT(child, ply + 1, depth - R - 1, nullw_alpha).Score;

        // continuing without this causes weird behaviour. the engine somehow
        // rates regular positions as mate in X. keep this. it's important.
        if (Eval.IsMateScore(score)) return (false, false);

        // we failed low, we prune this branch. it is not good enough
        if (window.FailsLow(score, col) && PruningOptions.ALLOW_NULL_MOVE_PRUNING) 
            return (true, false);

        if (!PruningOptions.ALLOW_LATE_MOVE_REDUCTIONS) return (false, false);

        // REDUCTIONS PART:
        // size of the window
        int window_size = Math.Abs(window.beta - window.alpha);

        // one tenth of the window is the margin
        int reduce_margin = Math.Min(MAX_REDUCE_MARGIN, window_size / WINDOW_SIZE_DIVISOR) * (col == 0 ? 1 : -1) / MARGIN_DIVISOR + exp_nodes;
        if (reduce_margin == 0 || (depth != REDUCTION_DEPTH)) return (false, false);

        // we didn't fail low, but if the history rep is bad, we try to fail low
        // again with a small margin and if we succeed, we only reduce the search depth
        // (we don't prune this time, we only reduce)
        bool reduce = R == HH_R && window.FailsLow((short)(score - reduce_margin), col);

        return (false, reduce);
    }

    internal static int GetReduce(int ply, int depth, int exp_nodes) {
        return Math.Min(REDUCTION_DEPTH - 1, Math.Max(2, 
            (int)(Math.Log2(exp_nodes) + depth / 4f)));
    }
}
