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
    internal const int MinPly   = 4;
    internal const int MinDepth = 0;

    // minimum nodes expanded before lmr
    // (we obviously don't want to reduce the pv)
    internal const int MinExpNodes = 3;

    // when a move's history rep falls below this threshold,
    // we use a larger R (we assume the move isn't that good
    // and save some time by not searching it that deeply)
    private const int HistReductionThreshold = -1320;

    // depth reduce normally and with bad history rep
    private const int R     = 3;
    private const int HistR = 4;

    private const int MaxReduceMargin   = 66;
    private const int WindowSizeDivisor = 10;
    private const int MarginDivisor     = 5;

    private const int ReductionDepth = 4;

    // should we prune or reduce?
    internal static (bool Prune, bool Reduce) TryPrune(Board board, Board child, Move move, int ply, int depth, Color col, int expNodes, Window window) {

        // depth reduce is larger with bad history
        int R = History.GetRep(board, move) < HistReductionThreshold ? HistR : LMR.R;
        //Console.WriteLine(History.GetRep(board, move));

        // null window around alpha
        Window nullAlphaWindow = col == Color.WHITE 
            ? new(window.alpha, (short)(window.alpha + 1)) 
            : new((short)(window.beta - 1), window.beta);

        // once again a reduced depth search
        short score = PVSearch.ProbeTT(child, ply + 1, depth - R - 1, nullAlphaWindow).Score;

        // continuing without this causes weird behaviour. the engine somehow
        // rates regular positions as mate in X. keep this. it's important.
        if (Eval.IsMateScore(score)) 
            return (false, false);

        // we failed low, we prune this branch. it is not good enough
        if (col == Color.WHITE
            ? (score <= window.alpha)
            : (score >= window.beta) 
            && PruningOptions.AllowNullMovePruning) 

            return (true, false);

        if (!PruningOptions.AllowLateMoveReductions) 
            return (false, false);

        // REDUCTIONS PART:
        // size of the window
        int window_size = Math.Abs(window.beta - window.alpha);

        // one tenth of the window is the margin
        short margin = (short)(Math.Min(MaxReduceMargin, window_size / WindowSizeDivisor) * (col == Color.WHITE ? 1 : -1) / MarginDivisor + expNodes);
        
        if (margin == 0 || (depth != ReductionDepth)) 
            return (false, false);

        // we didn't fail low, but if the history rep is bad, we try to fail low
        // again with a small margin and if we succeed, we only reduce the search depth
        // (we don't prune this time, we only reduce)
        score -= margin;
        bool reduce = R == HistR 
            && col == Color.WHITE
                ? (score <= window.alpha)
                : (score >= window.beta);

        return (false, reduce);
    }

    internal static int GetReduce(int ply, int depth, int expNodes) {
        return 2;
        //return Math.Min(ReductionDepth - 1, Math.Max(2, 
        //    (int)(Math.Log2(expNodes) + depth / 4f)));
    }
}
