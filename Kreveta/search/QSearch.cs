//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.consts;
using Kreveta.evaluation;
using Kreveta.movegen;
using Kreveta.search.moveorder;
using Kreveta.search.pruning;

using System;

// ReSharper disable InconsistentNaming

namespace Kreveta.search;

internal static class QSearch {

    // maximum depth allowed in the quiescence search itself
    internal const int QSDepth = 12;

    // maximum depth total - qsearch and regular search combined
    // changes each iteration depending on pvsearch depth
    internal static int CurQSDepth;

    // same idea as ProbeTT, but used in qsearch
    // internal static short QProbeTT(Board board, int ply, Window window) {
    //
    //     int depth = QSDepth - ply - PVSearch.CurDepth;
    //
    //     // did we find the position and score?
    //     if (ply >= PVSearch.CurDepth + 3 && TT.TryGetScore(board, depth, ply, window, out short ttScore))
    //         return ttScore;
    //
    //     // if the position is not yet stored, we continue the qsearch and then store it
    //     short score = Search(board, ply, window);
    //     TT.Store(board, (sbyte)depth, ply, window, score, default);
    //     return score;
    // }

    // QUIESCENCE SEARCH:
    // instead of immediately returning the static eval of leaf nodes in the main
    // search tree, we return a qsearch eval. qsearch is essentially just an extension
    // to the main search, but only expands captures or checks. this prevents falsely
    // evaluating positions where we can for instance lose a queen in the next move
    internal static short Search(Board board, int ply, Window window, bool onlyCaptures = false) {

        // exit the search if we should abort
        if (PVSearch.Abort)
            return 0;

        // increment the node counter
        PVSearch.CurNodes++;
        PVSControl.TotalNodes++;

        // this stores the highest achieved search depth in this
        // iteration. if we surpassed it, store it as the new one
        if (ply > PVSearch.AchievedDepth)
            PVSearch.AchievedDepth = ply;

        // we reached the maximum allowed depth, return the static eval
        if (ply >= CurQSDepth)
            return Eval.StaticEval(board);

        Color col = board.Color;

        // is the side to move in check?
        //
        // TODO - if we are only generating captures from a certain point,
        //        do we still need to be checking whether we are checked?
        //
        bool inCheck = Movegen.IsKingInCheck(board, col);

        // stand pat is just a fancy word for static eval
        short standPat = Eval.StaticEval(board);

        // don't try to cutoff when in check
        if (!inCheck) {
            
            // if the stand pat fails high, we can return it.
            // if not, we at least try to use it as the lower bound
            if (window.TryCutoff(standPat, col))
                return col == Color.WHITE
                    ? window.Alpha
                    : window.Beta;
        }

        // a bit complex idea - if we are checked, we generate all legal moves,
        // not just captures. but once we get out of check, we no longer want
        // to return to generating all legal moves, so we pass this as an argument
        // to the next search, and we only generate captures from a certain point.
        onlyCaptures = !inCheck || onlyCaptures;

        // if we aren't in check we only generate captures
        Span<Move> moves = Movegen.GetLegalMoves(board, onlyCaptures);

        // no moves have been generated
        if (moves.Length == 0) {

            // if we aren't checked, it means there just aren't
            // any more captures, and we can return the stand pat.
            //
            // as already mentioned, from a certain point we only
            // generate captures, so we don't bother checking for
            // checks right now, because we could encounter false
            // mate scores
            //
            // we might be in stalemate, though.
            // (there's nothing we can do...)
            if (onlyCaptures) {
                return standPat;
            }

            // otherwise return the mate score
            return inCheck
                ? Score.CreateMateScore(col, ply)
                : (short)0;
        }

        // we aren't checked => sort the generated captures
        if (onlyCaptures) {

            // sort the captures by MVV-LVA
            // (most valuable victim - least valuable aggressor)
            moves = MVV_LVA.OrderCaptures([..moves]);
        }

        // loop the generated moves
        for (int i = 0; i < moves.Length; ++i) {

            Board child = board.Clone();
            child.PlayMove(moves[i]);

            // value of the piece we just captured
            int captured = (inCheck ? 0 : EvalTables.PieceValues[(byte)moves[i].Capture])
                * (col == Color.WHITE ? 1 : -1);

            // delta pruning
            if (PruningOptions.AllowDeltaPruning
                && !inCheck
                && ply >= PVSearch.CurDepth + DeltaPruning.MinPly) {

                // very similar to futility pruning but makes use
                // of the value of the currently captured piece
                if (DeltaPruning.TryPrune(ply, CurQSDepth, col, window, standPat, captured)) {
                    continue;
                }
            }

            // full search
            short score = Search(child, ply + 1, window, onlyCaptures);

            // try to get a beta cutoff
            if (window.TryCutoff(score, col)) {
                
                // we want to store this in the tt not to retrieve the score,
                // but to retrieve the best move for move ordering
                if (ply <= PVSearch.CurDepth + 2)
                    TT.Store(board, -1, ply, window, score, moves[i]);

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