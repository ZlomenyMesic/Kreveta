//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.movegen;

using System;
using System.Threading.Tasks;

// ReSharper disable InconsistentNaming

namespace Kreveta.moveorder.history;

// to help move ordering, we use a few different history heuristics.
// the idea of these is that we save moves that proved to be good
// or bad in the past, idenependently of the position. for instance,
// if we noticed that sacrificing our rook two moves ago was a terrible
// move, we can usually be assured that doing the same two pawn pushes
// later would be the same exact blunder.
internal static class QuietHistory {

    // this array stores history values of quiet moves - no captures
    private static readonly int[][] QuietScores = new int[64][];

    // idea taken from Chess Programming Wiki:
    // the main disadvantage of the history heuristic is that it tends to
    // bias towards moves, which appear more frequently. there might be
    // a move that turned out to be very good, but doesn't occur that
    // often and thus, doesn't get a very large score. for this reason, we
    // use the so-called relative history heuristic, which alongside with
    // the history scores also stores the number of times a move has been
    // visited (ButterflyScores). when retrieving the quiet score, it is
    // then divided by this number to get the average.
    private static readonly int[][] ButterflyBoard = new int[64][];

    internal static void Init() {
        for (int i = 0; i < 64; i++) {
            QuietScores[i]    = new int[64];
            ButterflyBoard[i] = new int[64];
        }
    }

    // before each new iterated depth, we "shrink" the stored values.
    // they are still quite relevant, but the newcoming values are
    // more important, so we want them to have a stronger effect
    internal static void Shrink() {
        Parallel.For(0, 64, i => {
            Parallel.For(0, 64, j => {
                
                // history reputation is straightforward
                QuietScores[i][j] /= 2;
                
                // this for an unexplainable reason works
                // out to be the best option available
                ButterflyBoard[i][j] = Math.Min(1, ButterflyBoard[i][j]);
            });
        });
    }

    // clear all history data
    internal static void Clear() {
        Parallel.For(0, 64, i => {
            Array.Clear(QuietScores[i]);
            Array.Clear(ButterflyBoard[i]);
        });
    }
    
    // modify the history reputation of a move. isMoveGood tells us how
    internal static void ChangeRep(Move move, int depth, bool isGood) {
        int start = move.Start;
        int end   = move.End;
        
        // how much should the move affect the reputation (moves at higher depths
        // are probably more reliable, so their impact should be stronger)
        QuietScores[start][end] += Math.Min(depth * depth - 5, 84)
                               
                               // we either add or subtract the shift, depending on
                               // whether the move was good or not
                               * (isGood ? 1 : -1);

        // add the move as visited, too
        ButterflyBoard[start][end]++;
    }

    // retrieve the reputation of a move
    internal static int GetRep(Move move) {
        int start = move.Start;
        int end   = move.End;

        // quiet score and butterfly score
        int q  = QuietScores[start][end];
        int bf = ButterflyBoard[start][end];

        // precaution to not divide by zero
        if (bf == 0) return 0;
        
        // and now, as already mentioned, we divide the quiet score
        // by the number of times the move has been visited to get
        // a more average score
        return 12 * q / bf;
    }
}