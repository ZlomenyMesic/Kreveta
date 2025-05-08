//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.consts;
using Kreveta.movegen;

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Kreveta.evaluation;

// ReSharper disable InconsistentNaming

namespace Kreveta.search.moveorder;

// a very simple move-ordering heuristic only used for captures. the name
// stands for "Most Valuable Victim - Least Valuable Aggressor". the idea
// is that capturing a more valuable piece with a less valuable piece is
// usually a good move, while the opposite is often bad, e.g. capturing
// a queen with a pawn is usually prosperous, while capturing a pawn with
// a queen would most often be a terrible sacrifice. it is important to
// note that since we search all moves (not just the "obvious" ones), this 
// technique is actually much more useful than it seems.
internal static class MVV_LVA {

    // the piece values used don't have to be perfect, but
    // they should help determine, which captures are good,
    // e.g. trading a bishop and a knight for a rook and
    // a pawn usually isn't good and trading a minor piece
    // for three pawns also isn't a very good idea. the king
    // is given a lot of points to avoid bugs, although
    // i think there shouldn't be any

    // takes a list of captures, sorts it by MVV-LVA and returns it.
    internal static Move[] OrderCaptures(Move[] capts) {

        // if there's only a single available capture,
        // don't bother wasting time on this thing
        if (capts.Length <= 1) {
            return [ ..capts];
        }

        // add each capture and its score into a list
        var scores = new (Move, int)[capts.Length];
        int cur = 0;

        for (int i = 0; i < capts.Length; i++) {
            scores[cur++] = (capts[i], GetCaptureScore(capts[i]));
        }

        // here we once again have a very naive and primitive
        // sorting algorithm, but it shouldn't slow anything
        // down due to the usual low amount of captures
        bool sortsMade = true;
        while (sortsMade) {

            // once we haven't switched any moves, break the loop
            sortsMade = false;

            for (int i = 1; i < scores.Length; i++) {

                // if the current item's MVV-LVA score is higher
                // than the previous one's, switch their places
                if (scores[i].Item2 > scores[i - 1].Item2) {
                    (scores[i], scores[i - 1]) = (scores[i - 1], scores[i]);
                    sortsMade = true;
                }
            }
        }

        //add the sorted captures to the final list
        //Span<Move> sorted = stackalloc Move[scores.Length];
        Move[] sorted = new Move[scores.Length];

        for (int i = 0; i < scores.Length; i++) {
            sorted[i] = scores[i].Item1;
        }

        return sorted;
    }

    // this method calculates the score for a single move
    // positive score = likely a better move
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetCaptureScore(Move capt) {

        // piece moved and piece captured (aggressor and victim)
        int aggressor = EvalTables.PieceValues[(byte)capt.Piece];
        int victim    = EvalTables.PieceValues[(byte)capt.Capture];

        // weird case for en passant - the move doesn't end
        // on the actual victim, so the capture is marked as
        // negative one despite the captured pawn
        if (victim == -1) {

            // en passant always captures a pawn
            victim = EvalTables.PieceValues[(byte)PType.PAWN];
        }

        // calculate the difference - positive diff means the move is
        // likely to be good, negative one would probably be a bad trade
        return victim - aggressor;
    } 
}
