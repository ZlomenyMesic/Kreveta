/*
 *  Stockshrimp chess engine 1.0
 *  developed by ZlomenyMesic
 */

using Stockshrimp_1.evaluation;
using Stockshrimp_1.movegen;
using Stockshrimp_1.search.movesort;

namespace Stockshrimp_1.search.pruning;

// LATE MOVE REDUCTIONS:
// moves other than the pv node are expected to fail low (not raise alpha),
// so we first search them with null window around alpha. if it does not fail
// low as expected, we do a full re-search
internal static class LMR {

    // once again we set a minimum ply and depth
    internal const int MIN_PLY = 4;
    internal const int MIN_DEPTH = 0;

    // minimum nodes expanded before lmr
    // (we obviously don't want to reduce the pv)
    internal const int MIN_EXP_NODES = 3;

    // when a move's history rep falls below this threshold,
    // we use a larger R (we assume the move isn't that good
    // and save some time by not searching it that deeply)
    internal const int HH_THRESHOLD = -1320;

    // depth reduce normally and with bad history rep
    internal const int R = 3;
    internal const int HH_R = 4;

    // again a couple conditions
    internal static bool CanPruneOrReduce(int ply, int depth, int exp_nodes, bool interesting) {
        return !interesting
            && ply >= MIN_PLY
            && depth >= MIN_DEPTH
            && exp_nodes >= MIN_EXP_NODES;
    }

    internal static (bool Prune, bool Reduce) TryPrune(Board child, Move move, int ply, int depth, int col, Window window) {

        // depth reduce is larger with bad history
        int R = History.GetRep(child, move) < HH_THRESHOLD ? HH_R : LMR.R;

        // null window around alpha
        Window nullw_alpha = window.GetLowerBound(col);

        // once again a reduced depth search
        short score = PVSearch.ProbeTT(child, ply + 1, depth - R - 1, nullw_alpha).Score;

        if (Eval.IsMateScore(score)) return (false, false);

        // we failed low, we prune this branch. it is not good enough
        bool prune = window.FailsLow(score, col);

        //return (prune, false);

        // REDUCTIONS PART:
        // size of the window
        int window_size = Math.Abs(window.beta - window.alpha);

        // one tenth of the window is the margin
        int reduce_margin = Math.Min(66, window_size / 10) * (col == 0 ? 1 : -1) / 5;
        if (reduce_margin == 0 || (depth != 4)) return (prune, false);

        // we didn't fail low, but if the history rep is bad, we try to fail low
        // again with a small margin and if we succeed, we only reduce the search depth
        // (we don't prune this time, we only reduce)
        bool reduce = R == HH_R && window.FailsLow((short)(score - reduce_margin), col);

        return (prune, reduce);
    }
}
