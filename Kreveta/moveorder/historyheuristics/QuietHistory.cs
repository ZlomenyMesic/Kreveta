//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.consts;
using Kreveta.movegen;

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

// ReSharper disable InconsistentNaming

namespace Kreveta.moveorder.historyheuristics;

// to help move ordering, we use a few different history heuristics.
// the idea of these is that we save moves that proved to be good
// or bad in the past, idenependently of the position. for instance,
// if we noticed that sacrificing our rook two moves ago was a terrible
// move, we can usually be assured that doing the same two pawn pushes
// later would be the same exact blunder.
internal static class QuietHistory {

    // the moves are usually indexed [from, to] but after some testing,
    // indexing by [from, piece] yields much better results. it may just
    // be a failure in my implementation, though.

    // this array stores history values of quiet moves - no captures
    [ReadOnly(true)]
    private static readonly int[][] QuietScores = new int[64][];

    // idea taken from Chess Programming Wiki:
    // the main disadvantage of the history heuristic is that it tends to
    // bias towards moves, which appear more frequently. there might be
    // a move that turned out to be very good, but doesn't occur that
    // often and thus, doesn't get a very large score. for this reason,
    // use the so-called relative history heuristic, which alongside with
    // the history scores also stores the number of times a move has been
    // visited (ButterflyScores). when retrieving the quiet score, it is
    // then divided by this number to get the average.
    private static readonly int[][] ButterflyBoard = new int[64][];
    
    // there is still some scale, though
    internal static int RelHHScale = 12;

    internal static void Init() {
        for (int i = 0; i < 64; i++) {
            QuietScores[i]    = new int[12];
            ButterflyBoard[i] = new int[12];
        }
    }

    // before each new iterated depth, we "shrink" the stored values.
    // they are still quite relevant, but the new values coming are
    // more important, so we want them to have a stronger effect
    internal static void Shrink() {
        Parallel.For(0, 64, i => {
            Parallel.For(0, 12, j => {
                
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
    
    // i believe i stole these values from Stockfish, but
    // i am not sure. they do, however, work very great
    internal static int ShiftSubtract = 5;
    internal static int ShiftLimit    = 84;
    
    // modify the history reputation of a move. isMoveGood tells us how
    internal static void ChangeRep(in Board board, Move move, int depth, bool isMoveGood) {
        int i   = PieceIndex(board, move);
        int end = move.End;
        
        // how much should the move affect the reputation (moves at higher depths
        // are probably more reliable, so their impact should be stronger)
        QuietScores[end][i] += Math.Min(depth * depth - ShiftSubtract, ShiftLimit)
                               
                               // we either add or subtract the shift, depending on
                               // whether the move was good or not
                               * (isMoveGood ? 1 : -1);

        // add the move as visited, too
        ButterflyBoard[end][i]++;
    }

    // retrieve the reputation of a move
    internal static int GetRep(in Board board, Move move) {
        int i   = PieceIndex(board, move);
        int end = move.End;

        // quiet score and butterfly score
        int q  = QuietScores[end][i];
        int bf = ButterflyBoard[end][i];

        // precaution to not divide by zero
        if (bf == 0) return 0;
        
        // and now, as already mentioned, we divide the quiet score
        // by the number of times the move has been visited to get
        // a more average score
        return RelHHScale * q / bf;
    }

    // calculate the index of a piece in the boards
    // (we just add 6 for white pieces)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int PieceIndex(in Board board, Move move) {

        PType piece = move.Piece;
        
        // figure out the color of the piece based on whether it is present in the bitboard
        Color col = (board.Pieces[(byte)Color.WHITE * 6 + (byte)piece] ^ 1UL << move.Start) == 0UL
            ? Color.BLACK
            : Color.WHITE;

        return (byte)piece + (col == Color.WHITE ? 6 : 0);
    }
}
