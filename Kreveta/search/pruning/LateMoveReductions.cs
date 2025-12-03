//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.consts;
using Kreveta.evaluation;
using Kreveta.movegen;
using Kreveta.moveorder.historyheuristics;

using System;

// ReSharper disable InconsistentNaming

namespace Kreveta.search.pruning;

// moves other than the pv node are expected to fail low (not raise alpha),
// so we first search them with null window around alpha at a reduced depth.
// if it does not fail low as expected, we try to fail low once again but with
// a margin (this only applies to moves with bad history rep). if we fail low
// with a margin, we only reduce the search depth. if we don't fail low at all
// despite the margin, we do a full re-search
internal static class LateMoveReductions {

    // once again we set a minimum ply and depth
    internal const int MinPly   = 4;
    
    // minimum nodes expanded before lmr
    // (we obviously don't want to reduce the pv)
    internal const int MinExpNodes = 3;

    // the actual depth reduce within late move reductions
    internal const int R = 2;

    // when a move's history rep falls below this threshold,
    // we use a larger R (we assume the move isn't that good
    // and save some time by not searching it that deeply)
    private const short HistReductionThreshold = -715;

    // depth reduce normally and with bad history rep. this
    // reduce is used internally to evaluate positions.
    private const int InternalR        = 3;
    private const int InternalBadHistR = 4;

    private const int MarginBase        = 0;
    private const int MaxReduceMargin   = 66;
    private const int WindowSizeDivisor = 9;
    private const int MarginDivisor     = 6;
    private const int ImprovingMargin   = 12;
    private const int SearchedMovesMult = 93;

    // minimum see score for a deeper search (R--)
    private const int MinSEEDeeper    = 100;
    
    private const int LMReductionDepth = 4;

    // should we prune or reduce?
    internal static (bool ShouldPrune, bool ShouldReduce) TryPrune(in Board board, ref Board child, Move move, SearchState ss, Color col, byte searchedMoves, bool improving, int see) {

        // depth reduce is larger with bad quiet history
        // ReSharper disable once LocalVariableHidesMember
        int R = move.Capture == PType.NONE && QuietHistory.GetRep(board, move) < HistReductionThreshold
            ? InternalBadHistR
            : InternalR;

        if ( improving || see >= MinSEEDeeper) R--;
        if (!improving && see <  0)            R++;

        // null window around alpha
        var nullAlphaWindow = col == Color.WHITE 
            ? new Window(ss.Window.Alpha, (short)(ss.Window.Alpha + 1)) 
            : new Window((short)(ss.Window.Beta - 1), ss.Window.Beta);

        // once again a reduced depth search
        int score = PVSearch.ProbeTT(ref child, 
            new SearchState((sbyte)(ss.Ply + 1), (sbyte)(ss.Depth - R - 1), nullAlphaWindow, default, false)).Score;

        // continuing without this causes weird behaviour. the engine somehow
        // rates regular positions as mate in X. keep this. it's important.
        if (Score.IsMateScore(score)) 
            return (false, false);

        // we failed low, we prune this branch. it is not good enough
        if (col == Color.WHITE
            ? score <= ss.Window.Alpha
            : score >= ss.Window.Beta
            && PruningOptions.AllowLateMovePruning)

            return (true, false);

        if (!PruningOptions.AllowLateMoveReductions || ss.Depth != LMReductionDepth) 
            return (false, false);

        // REDUCTIONS PART:
        // size of the window
        int windowSize = Math.Abs(ss.Window.Beta - ss.Window.Alpha);

        // a fraction of the window is the margin
        int margin = MarginBase
            + Math.Min(MaxReduceMargin, windowSize / WindowSizeDivisor) / MarginDivisor
            
            // be more aggressive with later moves
            + searchedMoves * SearchedMovesMult / 100
                          
            // be less aggressive when improving
            + (improving ? -ImprovingMargin : 0);
        
        // make the margin relative to color
        margin *= col == Color.WHITE ? 1 : -1;

        if (margin == 0) 
            return (false, false);

        // we didn't fail low, but if the history rep is bad, we try to fail low
        // again with a small margin and if we succeed, we only reduce the search depth
        // (we don't prune this time, we only reduce)
        score -= margin;
        bool shouldReduce = R == InternalBadHistR 
            && col == Color.WHITE
                ? score <= ss.Window.Alpha
                : score >= ss.Window.Beta;

        return (false, shouldReduce);
    }
}
