//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.consts;
using Kreveta.movegen;
using Kreveta.moveorder.history;

using System;

namespace Kreveta.moveorder;

internal static class LazyMoveOrder {
    // pre-ordering moves has major performance drawbacks when beta cutoffs happen
    // at early moves, as ordering the rest was useless. to combat this issue, lazy
    // move ordering is used, where first all moves are assigned different scores,
    // and only during the move expansion is each next move selected. this fails when
    // a cutoff happens late or doesn't happen at all, but in most cases it's helpful
    internal static void AssignScores(in Board board, int depth, Move previous, Span<Move> moves, Span<int> scores, Span<int> seeScores, int count, int seePruneQuiet) {
        Color color     = board.SideToMove;
        int   earlyGame = Math.Max(0, board.GamePhase() - 51);
        
        // find killers and a potential countermove
        var ck = Killers.GetCluster(depth, captures: true);
        var qk = Killers.GetCluster(depth, captures: false);
        var c  = depth <= 2 ? CounterMoveHistory.Get(color, previous) : default;
        
        for (int i = 0; i < count; i++) {
            Move  move      = moves[i];
            PType promPiece = move.Promotion;
            bool  isCapture = move.Capture != PType.NONE || promPiece == PType.PAWN;
            
            // Static Exchange Evaluation (SEE) has the most effect on captures, as it is
            // quite reliable, but is used for quiets as well. all SEE scores are stored
            // in the span, so they can be later reused in search
            int see = SEE.GetMoveScore(in board, color, move);

            // prematurely discard quiet moves with bad SEE scores
            if (!isCapture && see < seePruneQuiet) {
                seeScores[i] = 0;
                moves    [i] = default;

                continue;
            }
            seeScores[i] = see;
            
            // some stuff for evaluating more easily
            bool  isCounter = c == move;
            bool  isKiller  = isCapture // we could use .Contains(), but this is faster
                ? ck[0] == move || ck[1] == move || ck[2] == move || ck[3] == move || ck[4] == move || ck[5] == move || ck[6] == move
                : qk[0] == move || qk[1] == move || qk[2] == move || qk[3] == move || qk[4] == move || qk[5] == move || qk[6] == move;
            
            // continuation history. the conthist values may easily reach tens
            // of thousands, so all values must be clamped accordingly
            int cont = previous != default ? ContinuationHistory.GetScore(previous, move) : 0;

            if (!isCapture) {
                PType movedPiece = move.Piece;

                // killers and counters obviously get a higher score,
                // as they have previously proved to be effective
                int killer  = isKiller  ? 835 : 0;
                int counter = isCounter ? 103 : 0;
                
                // then quiet and continuation history is applied
                int qhist = QuietHistory.GetRep(move);
                int se    = StaticEvalDiffHistory.Get(move);
                
                // punish queen and king moves in the opening or early
                // middlegame, of course except for castling
                int queen = (movedPiece == PType.QUEEN                           ? -83  : 0) * earlyGame / 19;
                int king  = (movedPiece == PType.KING && promPiece != PType.KING ? -211 : 0) * earlyGame / 19;

                // promotions and castling get placed higher
                int prom = promPiece switch {
                    PType.QUEEN => 279,
                    PType.ROOK  => 154,
                    PType.KING  => 78,
                    _           => 0
                };

                cont = Math.Min(cont, 1500);
                
                scores[i] = killer + counter + queen + king + prom
                          + (95 * qhist + 95 * cont + 74 * se + 16 * see) / 256;
            }

            else {
                // killers and counters are the same as in quiets, but
                // higher scores are applied to push captures above quiets
                int killer = isKiller ? 3102 : 1648;
                
                // then we have some history heuristics. it is often said that conthist doesn't do well
                // with captures, but here it does. pieceto history stores data from quiets only, and thus
                // learns, which squares should be occupied, which can then enhance capture ordering
                int chist = CaptureHistory.GetRep(move);
                int pt    = PieceToHistory.GetRep(color, move);
                
                // once again promotions get placed higher
                int prom = promPiece switch {
                    PType.QUEEN => 285,
                    PType.ROOK  => 199,
                    _           => 0
                };
                
                int totalHist = (165 * cont + 9 * chist + 29 * pt) / 1024;

                // clamp the total history effect not to hurt SEE ordering
                scores[i] = killer + see + prom
                          + Math.Clamp(totalHist, -50, 50);
            }
        }
    }

    // this selects the next best move from the scored list; moves that have been
    // already played are defaulted, and default is returned once no moves remain
    internal static Move NextMove(Span<Move> moves, Span<int> scores, ReadOnlySpan<int> seeScores, int moveCount, out int score, out int see) {
        int bestScore = int.MinValue;
        int bestIndex = -1;

        for (int i = 0; i < moveCount; i++) {
            if (moves[i] == default)
                continue;
            
            if (scores[i] > bestScore) {
                bestScore = scores[i];
                bestIndex = i;
            }
        }

        score = bestScore;
        see   = 0;

        // there aren't any moves left
        if (bestIndex == -1) 
            return default;

        see              = seeScores[bestIndex];
        Move bestMove    = moves[bestIndex];
        moves[bestIndex] = default;
        
        return bestMove;
    }
}