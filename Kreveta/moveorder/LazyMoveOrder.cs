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
    internal static int const1  = 119;
    internal static int const2  = 896;
    internal static int const3  = 103;
    internal static int const4  = 55;
    internal static int const5  = -96;
    internal static int const6  = -209;
    internal static int const7  = 277;
    internal static int const8  = 155;
    internal static int const9  = 82;
    internal static int const10 = 3011;
    internal static int const11 = 1542;
    internal static int const12 = 54;
    internal static int const13 = 15;
    internal static int const14 = 18;
    internal static int const15 = 284;
    internal static int const16 = 201;
    internal static int const17 = 84;
    
    internal static void AssignScores(in Board board, int depth, Move previous, ReadOnlySpan<Move> moves, Span<int> scores, int count) {
        Color col         = board.Color;
        bool  isEarlyGame = board.GamePhase() > const1;
        
        var captKillers = Killers.GetCluster(depth, captures: true);
        var killers     = Killers.GetCluster(depth, captures: false);
        var counterMove = CounterMoveHistory.Get(col, previous);
        
        for (int i = 0; i < count; i++) {
            Move move = moves[i];

            bool isCapture = move.Capture != PType.NONE;
            bool isKiller  = isCapture ? captKillers.Contains(move) : killers.Contains(move);
            bool isCounter = counterMove == move;

            // quiet moves
            if (!isCapture) {
                PType movedPiece = move.Piece;
                PType promPiece  = move.Promotion;

                int killer  = isKiller ? const2 : 0;
                int counter = isCounter ? const3 : 0;
                int qhist   = QuietHistory.GetRep(col, move) * const17 / 100;
                int cont    = previous != default ? ContinuationHistory.GetScore(previous, move) * const4 / 100 : 0;
                int queen   = movedPiece == PType.QUEEN && isEarlyGame                           ? const5 : 0;
                int king    = movedPiece == PType.KING && promPiece != PType.KING && isEarlyGame ? const6 : 0;

                int prom = promPiece switch {
                    PType.QUEEN => const7,
                    PType.ROOK  => const8,
                    PType.KING  => const9,
                    _           => 0
                };

                scores[i] += killer + counter + qhist + queen + king + prom + cont;
            }

            // captures
            else {
                int killer  = captKillers.Contains(move) ? const10 : const11;
                int counter = isCounter ? const12 : 0;
                int see     = SEE.GetCaptureScore(in board, col, move);
                int cont    = previous != default ? ContinuationHistory.GetScore(previous, move) * const13 / 100 : 0;
                //int recapt  = move.End == previous.End ? const14 : 0;

                int prom = move.Promotion switch {
                    PType.QUEEN => const15,
                    PType.ROOK  => const16,
                    _           => 0
                };

                scores[i] += killer + counter + cont + prom + see;
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
        
        score            = bestScore;
        Move bestMove    = moves[bestIndex];
        moves[bestIndex] = default;
        
        return bestMove;
    }
}