//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.consts;
using Kreveta.movegen;
using Kreveta.moveorder.history;
using Kreveta.tuning;

using System;

namespace Kreveta.moveorder;

internal static class LazyMoveOrder {
    // pre-ordering moves has major performance drawbacks when beta cutoffs happen
    // at early moves, as ordering the rest was useless. to combat this issue, lazy
    // move ordering is used, where first all moves are assigned different scores,
    // and only during the move expansion is each next move selected. this fails when
    // a cutoff happens late or doesn't happen at all, but in most cases it's helpful
    internal static void AssignScores(in Board board, int depth, Move previous, ReadOnlySpan<Move> moves, Span<int> scores, int count) {
        Color col         = board.Color;
        bool  isEarlyGame = board.GamePhase() > 119;
        
        // find killers and a potential countermove
        var captKillers = Killers.GetCluster(depth, captures: true);
        var killers     = Killers.GetCluster(depth, captures: false);
        var counterMove = CounterMoveHistory.Get(col, previous);
        
        for (int i = 0; i < count; i++) {
            Move move = moves[i];

            PType promPiece = move.Promotion;
            bool  isCapture = move.Capture != PType.NONE || promPiece == PType.PAWN;
            bool  isKiller  = isCapture ? captKillers.Contains(move) : killers.Contains(move);
            bool  isCounter = counterMove == move;

            if (!isCapture) {
                PType movedPiece = move.Piece;

                // killers and counters obviously get a higher score,
                // as they have previously proved to be effective
                int killer  = isKiller  ? 896 : 0;
                int counter = isCounter ? 105 : 0;
                
                // then quiet and continuation history is applied
                int qhist = QuietHistory.GetRep(move) * 35 / 100;
                int cont  = previous != default ? ContinuationHistory.GetScore(previous, move) * 38 / 100 : 0;
                
                // punish queen and king moves in the opening or early
                // middlegame, of course except for castling
                int queen = movedPiece == PType.QUEEN && isEarlyGame                           ? -97  : 0;
                int king  = movedPiece == PType.KING && promPiece != PType.KING && isEarlyGame ? -209 : 0;

                // promotions and castling get placed higher
                int prom = promPiece switch {
                    PType.QUEEN => 277,
                    PType.ROOK  => 156,
                    PType.KING  => 82,
                    _           => 0
                };

                scores[i] += killer + counter + qhist + queen + king + prom + cont;
            }

            else {
                // killers and counters are the same as in quiets, but
                // higher scores are applied to push captures above quiets
                int killer  = isKiller ? 2978 : 1532;
                
                // static exchange evaluation and continuation history. it is
                // often said that conthist doesn't work well with captures,
                // but here it seems like it does. i've also tried combining
                // SEE with additional MVV-LVA, but that didn't work at all
                int see  = SEE.GetCaptureScore(in board, col, move);
                int cont = previous != default ? ContinuationHistory.GetScore(previous, move) * 16 / 100 : 0;

                // once again promotions get placed higher
                int prom = promPiece switch {
                    PType.QUEEN => 284,
                    PType.ROOK  => 200,
                    _           => 0
                };

                scores[i] += killer + cont + prom + see;
            }
        }
    }

    // this selects the next best move from the scored list; moves that have been
    // already played are defaulted, and default is returned once no moves remain
    internal static Move NextMove(Span<Move> moves, Span<int> scores, int moveCount) {
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

        // there aren't any moves left
        if (bestIndex == -1) 
            return default;
        
        Move bestMove    = moves[bestIndex];
        moves[bestIndex] = default;
        
        return bestMove;
    }
}