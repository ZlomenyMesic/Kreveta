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
    internal static int const1 = 3563;
    internal static int const2 = -2033;
    internal static int const3 = 2012;
    internal static int const4 = 19;
    internal static int const5 = 58;
    internal static int const6 = 289;

    internal static int const7  = 2949;
    internal static int const8  = 112;
    internal static int const9  = 20;
    internal static int const10 = 274;
    internal static int const11 = 1402;

    internal static int const12 = 150;
    internal static int const13 = 100;
    internal static int const14 = 150;
    internal static int const15 = -1000;
    
    internal static void AssignScores(in Board board, int depth, Move previous, ReadOnlySpan<Move> moves, Span<int> scores, int count) {
        Color col = board.Color;
        
        var captKillers = Killers.GetCluster(depth, captures: true);
        var killers     = Killers.GetCluster(depth, captures: false);

        for (int i = 0; i < count; i++) {
            Move move = moves[i];
            
            // quiet moves
            if (move.Capture == PType.NONE) {
                int killer = killers.Contains(move) ? const1 : 0;
                int qhist  = Math.Clamp(QuietHistory.GetRep(col, move), const2, const3) * const4 / 100;
                int cont   = previous != default ? ContinuationHistory.GetScore(previous, move) * const5 / 100 : 0;
                
                int prom = move.Promotion switch {
                    PType.QUEEN => const6,
                    PType.ROOK  => const12,
                    PType.KING  => const13,
                    _           => 0
                };

                scores[i] += killer + qhist + cont + prom;
            }
            
            // captures
            else {
                int killer = captKillers.Contains(move) ? const7 : 0;
                int see    = SEE.GetCaptureScore(in board, col, move) * const8 / 100;
                int cont   = previous != default ? ContinuationHistory.GetScore(previous, move) * const9 / 100 : 0;
                
                int prom = move.Promotion switch {
                    PType.QUEEN => const10,
                    PType.ROOK  => const14,
                    _           => 0
                };

                // additional malus for negative SEE to push these captures lower
                if (see < 0) see += const15;

                scores[i] += const11 + killer + see + cont + prom;
            }
        }
    }

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

        // there aren't any moves left
        if (bestIndex == -1) {
            score = 0;
            return default;
        }
        
        score            = scores[bestIndex];
        Move bestMove    = moves[bestIndex];
        moves[bestIndex] = default;
        
        return bestMove;
    }
}