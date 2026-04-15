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
    
    internal static void AssignScores(in Board board, bool rootNode, int depth, Move previous, ReadOnlySpan<Move> moves, Span<int> scores, int count) {
        Color color     = board.SideToMove;
        int   earlyGame = Math.Max(0, board.GamePhase() - 110);
        
        // find killers and a potential countermove
        var captKillers = Killers.GetCluster(depth, captures: true);
        var killers     = Killers.GetCluster(depth, captures: false);
        var counterMove = depth <= 2 ? CounterMoveHistory.Get(color, previous) : default;
        
        for (int i = 0; i < count; i++) {
            Move move = moves[i];
            
            // some stuff for evaluating more easily
            PType promPiece = move.Promotion;
            bool  isCapture = move.Capture != PType.NONE || promPiece == PType.PAWN;
            bool  isKiller  = isCapture ? captKillers.Contains(move) : killers.Contains(move);
            bool  isCounter = counterMove == move;

            if (!isCapture) {
                PType movedPiece = move.Piece;

                // killers and counters obviously get a higher score,
                // as they have previously proved to be effective
                int killer  = isKiller  ? 835 : 0;
                int counter = isCounter ? 103 : 0;
                
                // then quiet and continuation history is applied
                int qhist = QuietHistory.GetRep(move);
                int cont  = previous != default ? ContinuationHistory.GetScore(previous, move) : 0;
                int se    = StaticEvalDiffHistory.Get(move);
                
                // punish queen and king moves in the opening or early
                // middlegame, of course except for castling
                int queen = (movedPiece == PType.QUEEN                           ? -83  : 0) * earlyGame / 40;
                int king  = (movedPiece == PType.KING && promPiece != PType.KING ? -211 : 0) * earlyGame / 40;

                // promotions and castling get placed higher
                int prom = promPiece switch {
                    PType.QUEEN => 279,
                    PType.ROOK  => 154,
                    PType.KING  => 78,
                    _           => 0
                };

                scores[i] = (!rootNode ? killer + counter + queen + king + prom : 0)
                            + (95 * qhist + 95 * cont + 74 * se) / 256;
            }

            else {
                // killers and counters are the same as in quiets, but
                // higher scores are applied to push captures above quiets
                int killer = isKiller ? 3102 : 1648;
                
                // Static Exchange Evaluation (SEE) has the most effect on ordering captures, as it
                // is the most reliable. en passant has always an SEE of zero, which means the eval
                // can be skipped (despite it being unintentional and wrong behaviour)
                int see = promPiece == PType.PAWN ? 0 : SEE.GetCaptureScore(in board, color, move);
                
                // then we have some history heuristics. it is often said that conthist doesn't do well
                // with captures, but here it does. pieceto history stores data from quiets only, and thus
                // learns, which squares should be occupied, which can then enhance capture ordering
                int cont  = previous != default ? ContinuationHistory.GetScore(previous, move) : 0;
                int chist = CaptureHistory.GetRep(move);
                int pt    = PieceToHistory.GetRep(color, move);
                
                // once again promotions get placed higher
                int prom = promPiece switch {
                    PType.QUEEN => 285,
                    PType.ROOK  => 199,
                    _           => 0
                };

                scores[i] = (!rootNode ? killer + see + prom : 0)
                            + (165 * cont + 9 * chist + 29 * pt) / 1024;
            }
        }
    }

    // this selects the next best move from the scored list; moves that have been
    // already played are defaulted, and default is returned once no moves remain
    internal static Move NextMove(Span<Move> moves, Span<int> scores, int moveCount, out int score) {
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

        // there aren't any moves left
        if (bestIndex == -1) 
            return default;

        Move bestMove    = moves[bestIndex];
        moves[bestIndex] = default;
        
        return bestMove;
    }
}