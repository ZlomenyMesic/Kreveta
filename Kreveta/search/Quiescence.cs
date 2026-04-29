//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.consts;
using Kreveta.evaluation;
using Kreveta.movegen;
using Kreveta.moveorder;
using Kreveta.moveorder.history;
using Kreveta.moveorder.history.corrections;

using System;

// ReSharper disable InconsistentNaming

namespace Kreveta.search;

internal static class Quiescence {

    // QUIESCENCE SEARCH:
    // instead of immediately returning the static eval of leaf nodes in the main
    // search tree, we return a qsearch eval. qsearch is essentially just an extension
    // to the main search, but only expands captures or checks. this prevents falsely
    // evaluating positions where there's for instancea hanging queen (horizon effect)
    internal static short Search(ref Board board, int ply, int alpha, int beta, int curQSDepth, int prevSq) {
        
        // exit the search if we should abort
        if (PVS.Abort && PVS.CurIterDepth > 1)
            return 0;

        // increment the node counter
        PVS.CurNodes++;

        // this stores the highest achieved search depth in this
        // iteration. if we surpassed it, store it as the new one
        if (ply > PVS.AchievedDepth)
            PVS.AchievedDepth = ply;

        // we reached the maximum allowed depth, return the static eval
        if (ply >= curQSDepth)
            return board.StaticEval;

        // try to retrieve a score and best move from the TT
        bool ttHit = TT.TryGetData(board.Hash, ply, out Move ttMove, out short ttScore, out var ttFlags, out int ttDepth);

        // cutoff if any tt score exists. we don't care about depth as long as depth
        // isn't -1, which would signify the entry comes from quiescence search as well
        if (ttHit && ttDepth >= 3 && (ttFlags.HasFlag(ScoreType.SCORE_EXACT)
                                   || ttFlags.HasFlag(ScoreType.LOWER_BOUND) && ttScore >= beta
                                   || ttFlags.HasFlag(ScoreType.UPPER_BOUND) && ttScore <= alpha)) {

            return ttScore;
        }
        
        // stand pat is just a fancy word for static eval
        bool  inCheck  = board.IsCheck;
        short standPat = board.StaticEval;
        
        // 1. STAND-PAT PRUNING
        // based on the null move observation, there is always at least one good move
        // in every position. since we're only searching captures, e.g. a subset of all
        // legal moves, we cannot return a bad score if we don't find a good move. so,
        // to combat that, we assume there is a good move, try to cut off using the
        // static evaluation and only then search. we cannot do this when in check, as
        // we would be searching all evasions, breaking the initial assumption
        if (!inCheck) {
            int corr = Corrections.Get(in board);
            
            // if the stand pat fails high, we can return it.
            // if not, we at least try to use it as the lower bound
            alpha = Math.Max(alpha, standPat + corr);
            
            if (alpha >= beta)
                return (short)alpha;
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
                : Score.CreateMateScore(ply);
        }

        // 2. SEE PRUNING
        // when not in check, only captures are generated. the capture ordering
        // function takes a threshold, below which all captures are directly skipped.
        int seeThreshold = (ply - PVS.CurIterDepth) / 8;

        // order the captures, and place the potential tt move at the front
        Span<int> seeScores = stackalloc int[count];
        if (!inCheck) count = SEE.OrderCaptures(in board, moves[..count], seeScores, seeThreshold, ttMove);

        // loop the generated moves
        for (int i = 0; i < count; ++i) {
            
            // 3. MOVECOUNT PRUNING
            // late captures are simply skipped, unless being a recapture
            if (!inCheck && i > 3 && ply >= PVS.CurIterDepth + 4 && moves[i].End != prevSq && !Score.IsMate(alpha)) {
                PVS.CurNodes++;
                continue;
            }
            
            // 4. DELTA PRUNING
            // very similar to futility pruning but makes use of the value of the currently
            // captured piece (or SEE score to be exact), which is added to the stand pat with
            // a margin, and if the eval still doesn't raise alpha, we prune this branch
            if (!inCheck && ply >= PVS.CurIterDepth + 4) {
                int captured    = EvalTables.PieceValues[(int)moves[i].Capture];
                int captureTerm = (2 * seeScores[i] + captured) / 3;

                // the delta base is multiplied by depth, but the depth must be calculated
                // in a bit more difficult way (maximum qsearch depth - current ply)
                int delta = (curQSDepth - ply) * 77 + captureTerm;

                // did we fail low?
                if (standPat + delta <= alpha) {
                    PVS.CurNodes++;
                    continue;
                }
            }
            
            Board child = board.Clone(ply + 1);
            child.PlayMove(moves[i], true);

            // full child search
            var score = (short)-Search(ref child, ply + 1, -beta, -alpha, curQSDepth, moves[i].End);
            
            // raise alpha if new score is higher than previous alpha
            alpha = Math.Max(alpha, score);

            // try to get a beta cutoff
            if (alpha >= beta) {
                
                // we want to store this in the tt not to retrieve the score,
                // but to retrieve the best move for better move ordering
                if (ply <= PVS.CurIterDepth + 2)
                    TT.Store(board.Hash, -1, ply, alpha, beta, score, moves[i]);
                
                CaptureHistory.ChangeRep(moves[i], weight: 1);

                // exit the loop
                break;
            }
        }

        // return the alpha bound score
        return (short)alpha;
    }
}