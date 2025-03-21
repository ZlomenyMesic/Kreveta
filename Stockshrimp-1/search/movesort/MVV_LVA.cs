/*
 *  Stockshrimp chess engine 1.0
 *  developed by ZlomenyMesic
 */

using Stockshrimp_1.evaluation;
using Stockshrimp_1.movegen;
using System.Globalization;

namespace Stockshrimp_1.search.movesort;

internal static class MVV_LVA {

    // very simple evaluation of pieces
    // king is gived 10 points?? not sure what bad could happen but don't want to risk it
    private static readonly int[] PIECE_VALUES = [1, 3, 3, 5, 9, 10, -1];

    // takes a list of captures, sorts it and returns it
    internal static List<Move> SortCaptures(List<Move> capts) {
        if (capts.Count <= 1) return capts;

        List<(Move, float)> scores = [];

        for (int i = 0; i < capts.Count; i++) {

            // piece moved and piece captured
            int aggressor = PIECE_VALUES[capts[i].Piece()];
            int victim = PIECE_VALUES[capts[i].Capture()];

            // weird case for en passant - move doesn't end on capture square
            if (victim == -1 && aggressor == 1) {

                // en passant captures a pawn, so we let the engine know it
                victim = 1;
            }

            // calculate the difference - positive diff => likely a good move
            int diff = victim - aggressor;
            scores.Add((capts[i], diff));
        }

        // the same exact primitive sorting algorithm
        bool sortsMade = true;
        while (sortsMade) {
            sortsMade = false;

            for (int i = 1; i < scores.Count; i++) {
                if (scores[i].Item2 < scores[i - 1].Item2) {

                    // switch places
                    (scores[i], scores[i - 1]) = (scores[i - 1], scores[i]);
                    sortsMade = true;
                }
            }
        }

        // add the sorted captures to the final list
        List<Move> sorted = [];
        foreach ((Move m, _) in scores)
            sorted.Add(m);

        return sorted;
    }
}
