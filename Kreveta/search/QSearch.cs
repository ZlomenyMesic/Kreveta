//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.consts;
using Kreveta.evaluation;
using Kreveta.movegen;
using Kreveta.moveorder;
using Kreveta.moveorder.history.corrections;
using Kreveta.search.transpositions;

using System;

// ReSharper disable InconsistentNaming

namespace Kreveta.search;

internal static class QSearch {

    // QUIESCENCE SEARCH:
    // instead of immediately returning the static eval of leaf nodes in the main
    // search tree, we return a qsearch eval. qsearch is essentially just an extension
    // to the main search, but only expands captures or checks. this prevents falsely
    // evaluating positions where we can for instance lose a queen in the next move
    internal static short Search(ref Board board, int ply, Window window, int curQSDepth) {
        // exit the search if we should abort
        if (PVSearch.Abort && PVSearch.CurIterDepth > 1)
            return 0;

        // increment the node counter
        PVSControl.TotalNodes++; PVSearch.CurNodes++;

        // this stores the highest achieved search depth in this
        // iteration. if we surpassed it, store it as the new one
        if (ply > PVSearch.AchievedDepth)
            PVSearch.AchievedDepth = ply;

        // we reached the maximum allowed depth, return the static eval
        if (ply >= curQSDepth)
            return board.StaticEval;

        // stand pat is just a fancy word for static eval
        Color col      = board.SideToMove;
        bool  inCheck  = board.IsCheck;
        short standPat = board.StaticEval;

        // don't try to cutoff when in check
        if (!inCheck) {
            short corr = Corrections.Get(in board);
            
            // if the stand pat fails high, we can return it.
            // if not, we at least try to use it as the lower bound
            if (window.TryCutoff((short)(standPat + corr), col))
                return col == Color.WHITE
                    ? window.Alpha
                    : window.Beta;
        }

        // if we aren't in check we only generate captures
        Span<Move> moves = stackalloc Move[Consts.MoveBufferSize];
        int count = Movegen.GetLegalMoves(ref board, moves, !inCheck);

        // no moves have been generated
        if (count == 0) {

            // if we aren't checked, it means there simply aren't any more
            // captures in the position. however, we might be stalemated,
            // but such cases shouldn't hurt the evaluation
            return !inCheck 
                ? standPat
                : Score.CreateMateScore(col, ply);
        }

        // we aren't checked => sort the generated captures
        int[]         seeScores = [];
        if (!inCheck) moves     = SEE.OrderCaptures(in board, moves[..count], out count, out seeScores, true);

        // loop the generated moves
        for (int i = 0; i < count; ++i) {
            
            // DELTA PRUNING:
            // very similar to futility pruning but makes use of the value of the currently
            // captured piece (or SEE score to be exact), which is added to the stand pat with
            // a margin, and if the eval still doesn't raise alpha, we prune this branch
            if (!inCheck && ply >= PVSearch.CurIterDepth + 4) {
                int captured = seeScores[i];

                // the delta base is multiplied by depth, but the depth must be calculated
                // in a bit more difficult way (maximum qsearch depth - current ply)
                int delta = (curQSDepth - ply) * 77 + captured;

                // did we fail low?
                if (col == Color.WHITE
                        ? standPat + delta <= window.Alpha
                        : standPat - delta >= window.Beta) {
                    
                    PVSControl.TotalNodes++; PVSearch.CurNodes++;
                    continue;
                }
            }
            
            Board child = board.Clone();
            child.PlayMove(moves[i], true);

            // full search
            short score = Search(ref child, ply + 1, window, curQSDepth);

            // try to get a beta cutoff
            if (window.TryCutoff(score, col)) {
                
                // we want to store this in the tt not to retrieve the score,
                // but to retrieve the best move for better move ordering
                if (ply <= PVSearch.CurIterDepth + 2)
                    TT.Store(board.Hash, -1, ply, window, score, moves[i]);

                // exit the loop
                break;
            }
        }

        // return the bound score
        return col == Color.WHITE
            ? window.Alpha
            : window.Beta;
    }
}