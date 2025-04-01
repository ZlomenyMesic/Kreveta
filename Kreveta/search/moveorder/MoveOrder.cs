//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.movegen;
using System.Diagnostics;

namespace Kreveta.search.moveorder;

internal static class MoveOrder {

    // to achieve the best results from PVS, good move ordering
    // is essential. searching the better moves first creates much
    // more space for pruning. we, of course, cannot know which
    // moves are the best unless we do the search, but we can at
    // least make a rough guess.
    internal static Move[] GetSortedMoves(Board b, int depth) {

        // we have to check the legality of found moves in case of some bugs
        // errors may occur anywhere in TT, Killers and History
        List<Move> legal = Movegen.GetLegalMoves(b);

        Move[] sorted = new Move[legal.Count];
        int cur = 0;

        // the first move is, obviously, the best move saved in the
        // transposition table. there also might not be any
        if (TT.GetBestMove(b, out Move bestMove) && legal.Contains(bestMove)) {
            sorted[cur++] = bestMove;
        }

        // after that go all captures, sorted by MVV-LVA, which
        // stands for Most Valuable Victim - Lest Valuable Aggressor.
        // see the actual MVV_LVA class for more information
        List<Move> capts = [];
        for (int i = 0; i < legal.Count; i++) {

            // only add captures
            if (!sorted.Contains(legal[i]) && legal[i].Capture() != 6)
                capts.Add(legal[i]);
        }

        // get the captures ordered and add them to the list
        Move[] mvvlva = capts.Count > 1 ? MVV_LVA.OrderCaptures([ ..capts]) : [ ..capts];
        for (int j = 0; j < mvvlva.Length; j++) {
            sorted[cur++] = mvvlva[j];
        }

        // next go killers, which are quiet moves, that caused
        // a beta cutoff somewhere in the past in or in a different
        // position. we only save a few per depth, though
        Move[] found_killers = Killers.Get(depth);
        for (int i = 0; i < found_killers.Length; i++) {

            // since killer moves are stored independently of
            // the position, we have to check a couple thing
            if (legal.Contains(found_killers[i])                    // illegal
                && !sorted.Contains(found_killers[i])               // already added
                && b.PieceAt(found_killers[i].End()).Item2 == 6) {  // not quiet

                sorted[cur++] = found_killers[i];
            }
        }

        // last and probably least are the remaining quiet moves,
        // which are sorted by their history values. see History
        List<(Move, int)> quiets = [];

        for (int i = 0; i < legal.Count; i++) {
            if (sorted.Contains(legal[i]))
                continue;

            // if the move has no history, this is
            // set to zero, which is also fine
            quiets.Add((legal[i], History.GetRep(b, legal[i])));
        }

        // sort them
        OrderQuiets(quiets);

        // and add them to the final list
        for (int i = 0; i < quiets.Count; i++)
            sorted[cur++] = quiets[i].Item1;

        return sorted;
    }

    // this is just a wrapper for a sorting loop, didn't
    // want to nest and create a mess in the ordering function
    internal static void OrderQuiets(List<(Move, int)> quiets) {

        // very primitive sorting algorithm for quiet moves,
        // sorts by their history value
        bool sorts_made = true;
        while (sorts_made) {
            sorts_made = false;

            for (int i = 1; i < quiets.Count; i++) {
                if (quiets[i].Item2 < quiets[i - 1].Item2) {

                    // switch places
                    (quiets[i], quiets[i - 1]) = (quiets[i - 1], quiets[i]);
                    sorts_made = true;
                }
            }
        }
    }
}
