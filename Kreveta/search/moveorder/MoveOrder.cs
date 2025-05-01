//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.consts;
using Kreveta.movegen;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace Kreveta.search.moveorder;

internal static class MoveOrder {

    // to achieve the best results from PVS, good move ordering
    // is essential. searching the better moves first creates much
    // more space for pruning. we, of course, cannot know which
    // moves are the best unless we do the search, but we can at
    // least make a rough guess.

    // don't use "in" keyword!!! it becomes much slower
    internal static unsafe Move[] GetSortedMoves([ReadOnly(true)] Board board, int depth, Move previous) {

        // we have to check the legality of found moves in case of some bugs
        // errors may occur anywhere in TT, Killers and History

        Span<Move> legal  = Movegen.GetLegalMoves(board);
        Span<Move> sorted = stackalloc Move[legal.Length];

        int cur = 0;

        // the first move is, obviously, the best move saved in the
        // transposition table. there also might not be any
        if (TT.TryGetBestMove(board, out Move ttMove) && legal.Contains(ttMove))
            sorted[cur++] = ttMove;

        // after that go all captures, sorted by MVV-LVA, which
        // stands for Most Valuable Victim - Lest Valuable Aggressor.
        // see the actual MVV_LVA class for more information
        List<Move> capts = [];

        for (int i = 0; i < legal.Length; i++) {

            // only add captures
            if (!sorted.Contains(legal[i])
                && legal[i].Capture != PType.NONE) {

                capts.Add(legal[i]);
            }
        }

        // get the captures ordered and add them to the list
        Move[] mvvlva = MVV_LVA.OrderCaptures([ ..capts]);

        for (int i = 0; i < mvvlva.Length; i++) {
            sorted[cur++] = mvvlva[i];
        }

        ulong empty = board.Empty;

        // next go killers, which are quiet moves, that caused
        // a beta cutoff somewhere in the past in or in a different
        // position. we only save a few per depth, though
        Span<Move> killers = Killers.GetCluster(depth);
        for (int i = 0; i < killers.Length; i++) {

            // since killer moves are stored independently of
            // the position, we have to check a couple thing
            if (legal.Contains(killers[i])                       // illegal
                && !sorted.Contains(killers[i])                  // already added
                && ((empty & (1UL << killers[i].End)) != 0UL)) { // quiet

                sorted[cur++] = killers[i];
            }
        }

        if (depth < CounterMoveHistory.MaxRetrieveDepth) {
            Move counter = CounterMoveHistory.Get(board.Color, previous);
            if (counter != default && legal.Contains(counter) && !sorted.Contains(counter)) {
                sorted[cur++] = counter;
            }
        }

        // last and probably least are the remaining quiet moves,
        // which are sorted by their history values. see History
        List<(Move, int)> quiets = [];

        for (int i = 0; i < legal.Length; i++) {
            if (sorted.Contains(legal[i]))
                continue;

            // if the move has no history, this is
            // set to zero, which is also fine
            quiets.Add((legal[i], QuietHistory.GetRep(board, legal[i])));
        }

        // sort them
        OrderQuiets([ ..quiets]);

        // and add them to the final list
        for (int i = 0; i < quiets.Count; i++)
            sorted[cur++] = quiets[i].Item1;

        Move[] result = [..sorted];
        return result;
    }

    // this is just a wrapper for a sorting loop, didn't
    // want to nest and create a mess in the ordering function
    private static void OrderQuiets((Move, int)[] quiets) {

        // very primitive sorting algorithm for quiet moves,
        // sorts by their history value
        bool sortsMade = true;
        while (sortsMade) {
            sortsMade = false;

            for (int i = 1; i < quiets.Length; i++) {
                if (quiets[i].Item2 > quiets[i - 1].Item2) {

                    // switch places
                    (quiets[i], quiets[i - 1]) = (quiets[i - 1], quiets[i]);
                    sortsMade = true;
                }
            }
        }
    }
}
