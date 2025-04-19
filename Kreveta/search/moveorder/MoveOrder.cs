//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.movegen;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace Kreveta.search.moveorder;

internal static class MoveOrder {

    // to achieve the best results from PVS, good move ordering
    // is essential. searching the better moves first creates much
    // more space for pruning. we, of course, cannot know which
    // moves are the best unless we do the search, but we can at
    // least make a rough guess.

    // don't use "in" keyword!!! it becomes much slower
    internal static Move[] GetSortedMoves([NotNull][ReadOnly(true)] Board board, int depth, Move previous) {

        // we have to check the legality of found moves in case of some bugs
        // errors may occur anywhere in TT, Killers and History
        Move[] legal = [ ..Movegen.GetLegalMoves(board)];

        Move[] sorted = new Move[legal.Length];
        int cur = 0;

        // the first move is, obviously, the best move saved in the
        // transposition table. there also might not be any
        if (TT.GetBestMove(board, out Move bestMove) && legal.Contains(bestMove)) {
            sorted[cur++] = bestMove;
        }

        // after that go all captures, sorted by MVV-LVA, which
        // stands for Most Valuable Victim - Lest Valuable Aggressor.
        // see the actual MVV_LVA class for more information
        List<Move> capts = [];

        for (int i = 0; i < legal.Length; i++) {

            // only add captures
            if (!sorted.Contains(legal[i]) 
                && legal[i].Capture != PType.NONE)

                capts.Add(legal[i]);
        }

        // get the captures ordered and add them to the list
        Move[] mvvlva = MVV_LVA.OrderCaptures([ ..capts]);
        //List<Move> badCaptures = [];

        for (int i = 0; i < mvvlva.Length; i++) {
            sorted[cur++] = mvvlva[i];
            //if (PVSearch.CurDepth - depth < 3) sorted[cur++] = mvvlva[i];

            //else if (MVV_LVA.GetCaptureScore(mvvlva[i]) >= -350)
            //    sorted[cur++] = mvvlva[i];

            //else {
            //    //Console.WriteLine($"{mvvlva[i].Piece} {mvvlva[i].Capture}");
            //    badCaptures.Add(mvvlva[i]);
            //}
        }

        ulong empty = board.Empty;

        // next go killers, which are quiet moves, that caused
        // a beta cutoff somewhere in the past in or in a different
        // position. we only save a few per depth, though
        Move[] killers = Killers.Get(depth);
        for (int i = 0; i < killers.Length; i++) {

            // since killer moves are stored independently of
            // the position, we have to check a couple thing
            if (legal.Contains(killers[i])                           // illegal
                && !sorted.Contains(killers[i])                      // already added
                && ((empty & Consts.SqMask[killers[i].End]) != 0)) { // quiet

                sorted[cur++] = killers[i];
            }
        }

        //if (depth < 2) {
        //    Move counter = CounterMoveHistory.Get(board.color, previous);
        //    if (counter != default && legal.Contains(counter) && !sorted.Contains(counter)) {
        //        sorted[cur++] = counter;
        //    }
        //}

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
        OrderQuiets(quiets);

        // and add them to the final list
        for (int i = 0; i < quiets.Count; i++)
            sorted[cur++] = quiets[i].Item1;

        //for (int i = 0; i < badCaptures.Count; i++) {
        //    if (!sorted.Contains(badCaptures[i]))
        //        sorted[cur++] = badCaptures[i];
        //}

        return sorted;
    }

    // this is just a wrapper for a sorting loop, didn't
    // want to nest and create a mess in the ordering function
    internal static void OrderQuiets(List<(Move, int)> quiets) {

        // very primitive sorting algorithm for quiet moves,
        // sorts by their history value
        bool sortsMade = true;
        while (sortsMade) {
            sortsMade = false;

            for (int i = 1; i < quiets.Count; i++) {
                if (quiets[i].Item2 > quiets[i - 1].Item2) {

                    // switch places
                    (quiets[i], quiets[i - 1]) = (quiets[i - 1], quiets[i]);
                    sortsMade = true;
                }
            }
        }
    }
}
