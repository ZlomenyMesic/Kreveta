//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.evaluation;
using Kreveta.movegen;
using Kreveta.search.moveorder;

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
    internal const int MinPly   = 4;
    internal const int MinDepth = 0;

    // minimum nodes expanded before lmr
    // (we obviously don't want to reduce the pv)
    internal const int MinExpNodes = 3;

    // the actual depth reduce within late move reductions
    internal const int R = 2;

    // when a move's history rep falls below this threshold,
    // we use a larger R (we assume the move isn't that good
    // and save some time by not searching it that deeply)
    private const int HistReductionThreshold = -1220;

    // depth reduce normally and with bad history rep. this
    // reduce is used internally to evaluate positions.
    private const int InternalR     = 3;
    private const int InternalHistR = 4;

    private const int MaxReduceMargin   = 66;
    private const int WindowSizeDivisor = 9;
    private const int MarginDivisor     = 6;

    private const int ReductionDepth = 4;

    // should we prune or reduce?
    internal static (bool Prune, bool Reduce) TryPrune(in Board board, in Board child, Move move, int ply, int depth, Color col, int expNodes, Window window) {

        // depth reduce is larger with bad history
        int R = History.GetRep(board, move) < HistReductionThreshold ? InternalHistR : InternalR;
        //Console.WriteLine(History.GetRep(board, move));

        // null window around alpha
        Window nullAlphaWindow = col == Color.WHITE 
            ? new(window.Alpha, (short)(window.Alpha + 1)) 
            : new((short)(window.Beta - 1), window.Beta);

        // once again a reduced depth search
        short score = PVSearch.ProbeTT(child, ply + 1, depth - R - 1, nullAlphaWindow, default).Score;

        // continuing without this causes weird behaviour. the engine somehow
        // rates regular positions as mate in X. keep this. it's important.
        if (Eval.IsMateScore(score)) 
            return (false, false);

        // we failed low, we prune this branch. it is not good enough
        if (col == Color.WHITE
            ? (score <= window.Alpha)
            : (score >= window.Beta) 
            && PruningOptions.AllowNullMovePruning)
            return (true, false);

        if (!PruningOptions.AllowLateMoveReductions) 
            return (false, false);

        // REDUCTIONS PART:
        // size of the window
        int windowSize = Math.Abs(window.Beta - window.Alpha);

        // one tenth of the window is the margin
        short margin = (short)(Math.Min(MaxReduceMargin, windowSize / WindowSizeDivisor) * (col == Color.WHITE ? 1 : -1) / MarginDivisor + expNodes);

        if (margin == 0 || (depth != ReductionDepth)) 
            return (false, false);

        // we didn't fail low, but if the history rep is bad, we try to fail low
        // again with a small margin and if we succeed, we only reduce the search depth
        // (we don't prune this time, we only reduce)
        score -= margin;
        bool shouldReduce = R == InternalHistR 
            && col == Color.WHITE
                ? (score <= window.Alpha)
                : (score >= window.Beta);

        return (false, shouldReduce);
    }

    [Obsolete("No need for a method.", true)]
    internal static int GetReduce(int ply, int depth, int expNodes) {
        return 2;
        //return Math.Min(ReductionDepth - 1, Math.Max(2, 
        //    (int)(Math.Log2(expNodes) + depth / 4f)));
    }
}
