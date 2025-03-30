//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.movegen;
using System.Runtime.CompilerServices;

namespace Kreveta.search.moveorder;

internal static class MVV_LVA {

    // very simple values of pieces
    // the values don't have to be perfect, but they should
    // help determine which captures are good, e.g. trading 
    // a bishop and a knight for a rook and a pawn usually
    // isn't good and trading a minor piece for three pawns
    // also isn't a very good idea. the king is given a lot
    // of point to avoid some bugs, although i think there
    // shouldn't be any
    private static readonly int[] PIECE_VALUES = [100, 315, 330, 520, 930, 10000, -1];

    // takes a list of captures, sorts it from best to worst and
    // returns it. the technique is called MVV-LVA and stands for
    // Most Valuable Victim - Least Valuable Aggressor. this gives
    // us a rough guess of how good a capture is, e.g. capturing
    // a queen with a pawn is usually a really good move.
    internal static List<Move> OrderCaptures(List<Move> capts) {

        // if there's only a single available capture,
        // don't bother wasting time on this thing
        if (capts.Count <= 1) return capts;

        // add each capture and its score into a list
        List<(Move, float)> scores = [];
        for (int i = 0; i < capts.Count; i++) {
            scores.Add((capts[i], GetCaptureScore(capts[i])));
        }

        // here we once again have a very naive and primitive
        // sorting algorithm, but it shouldn't slow anything
        // down due to the usual low amount of captures
        bool sorts_made = true;
        while (sorts_made) {

            // once we haven't switched any moves, break the loop
            sorts_made = false;

            for (int i = 1; i < scores.Count; i++) {

                // if the current item's MVV-LVA score is higher
                // than the previous one's, switch their places
                if (scores[i].Item2 < scores[i - 1].Item2) {
                    (scores[i], scores[i - 1]) = (scores[i - 1], scores[i]);
                    sorts_made = true;
                }
            }
        }

        // add the sorted captures to the final list
        List<Move> sorted = [];
        foreach ((Move m, _) in scores)
            sorted.Add(m);

        return sorted;
    }

    // this method calculates the score for a single move
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int GetCaptureScore(Move capt) {

        // piece moved and piece captured (aggressor and victim)
        int aggressor = PIECE_VALUES[capt.Piece()];
        int victim    = PIECE_VALUES[capt.Capture()];

        // weird case for en passant - the move doesn't end
        // on the actual victim, so the capture is marked as
        // none even though we captured a pawn
        if (victim == -1 && aggressor == 1) {

            // en passant always captures a pawn
            victim = 1;
        }

        // calculate the difference - positive diff means the move is
        // likely to be good, negative one would probably be a bad trade
        return victim - aggressor;
    } 
}
