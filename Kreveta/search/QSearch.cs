//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.evaluation;
using Kreveta.movegen;
using Kreveta.search.moveorder;
using Kreveta.search.pruning;
using System.Diagnostics.CodeAnalysis;

namespace Kreveta.search;

internal static class QSearch {

    // maximum depth allowed in the quiescence search itself
    internal const int QSDepth = 12;

    // maximum depth total - qsearch and regular search combined
    // changes each iteration depending on pvsearch depth
    internal static int CurQSDepth;

    // same idea as ProbeTT, but used in qsearch
    internal static short QProbeTT([NotNull] Board board, int ply, Window window) {

        int depth = QSDepth - ply - PVSearch.CurDepth;

        // did we find the position and score?
        if (ply >= PVSearch.CurDepth + 3 && TT.TryGetScore(board, depth, ply, window, out short ttScore))
            return ttScore;

        // if the position is not yet stored, we continue the qsearch and then store it
        short score = Search(board, ply, window);
        TT.Store(board, (sbyte)depth, ply, window, score, default);
        return score;
    }

    // QUIESCENCE SEARCH:
    // instead of immediately returning the static eval of leaf nodes in the main
    // search tree, we return a qsearch eval. qsearch is essentially just an extension
    // to the main search, but only expands captures or checks. this prevents falsely
    // evaluating positions where we can for instance lose a queen in the next move
    internal static short Search([NotNull] Board board, int ply, Window window, bool onlyCaptures = false) {

        if (PVSearch.Abort)
            return 0;

        PVSearch.CurNodes++;

        // this stores the highest achieved search depth in this iteration
        if (ply > PVSearch.AchievedDepth)
            PVSearch.AchievedDepth = ply;

        // we reached the end, we return the static eval
        if (ply >= CurQSDepth)
            return Eval.StaticEval(board);

        Color col = board.color;

        // is the side to move in check?
        //
        // TODO - if we are only generating captures from a certain point,
        //        do we still need to be checking whether we are checked?
        //
        bool inCheck = Movegen.IsKingInCheck(board, col);

        short standPat = Eval.StaticEval(board);

        // can not use stand pat when in check
        if (!inCheck) {

            // stand pat is nothing more than a static eval

            // if the stand pat fails high, we can return it
            // if not, we use it as a lower bound (alpha)
            if (window.TryCutoff(standPat, col))
                return col == Color.WHITE
                    ? window.Alpha
                    : window.Beta;
        }

        onlyCaptures = !inCheck || onlyCaptures;

        // if we aren't in check we only generate captures
        Move[] moves = [.. Movegen.GetLegalMoves(board, onlyCaptures)];

        if (moves.Length == 0) {

            // if we aren't checked, it means there just aren't
            // any more captures and we can return the stand pat
            // (we also might be in stalemate - FIX THIS)
            if (onlyCaptures) {
                return standPat;
            }

            return inCheck
                ? Score.GetMateScore(col, ply)
                : (short)0;
        }

        // we aren't checked => sort the generated captures
        if (onlyCaptures) {

            // sort the captures by MVV-LVA
            // (most valuable victim - least valuable aggressor)
            moves = MVV_LVA.OrderCaptures(moves);
        }

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

                if (DeltaPruning.TryPrune(ply, CurQSDepth, col, window, standPat, captured)) {
                    continue;
                }
            }

            // full search
            short score = Search(child, ply + 1, window, onlyCaptures);

            if (window.TryCutoff(score, col)) {
                if (ply <= PVSearch.CurDepth + 2)
                    TT.Store(board, -1, ply, window, score, moves[i]);

                break;
            }
        }

        return col == Color.WHITE
            ? window.Alpha
            : window.Beta;
    }
}