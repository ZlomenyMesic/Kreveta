/*
 *  Stockshrimp chess engine 1.0
 *  developed by ZlomenyMesic
 */

using Stockshrimp_1.movegen;

namespace Stockshrimp_1.search.movesort;

internal static class MoveSort {
    internal static List<Move> GetSortedMoves(Board b, int depth) {

        List<Move> sorted = [];

        // we have to check the legality of found moves in case of some error??
        // errors may occur anywhere in TT, Killers and History
        List<Move> legal = Movegen.GetLegalMoves(b);

        // check for known best move in this position
        if (TT.GetBestMove(b, out Move bestMove) && legal.Contains(bestMove)) {
            sorted.Add(bestMove);
        }

        // captures sorted by MVV-LVA
        List<Move> capts = [];
        for (int i = 0; i < legal.Count; i++) {

            // add every capture in legal moves
            if (!sorted.Contains(legal[i]) && legal[i].Capture() != 6)
                capts.Add(legal[i]);
        }

        List<Move> mvvlva = capts.Count > 1 ? MVV_LVA.SortCaptures(capts) : capts;
        for (int j = 0; j < mvvlva.Count; j++) {
            sorted.Add(mvvlva[j]);
        }

        // killers - quiet moves that caused a beta cutoff
        Move[] found_killers = Killers.Get(depth);
        for (int i = 0; i < found_killers.Length; i++) {

            if (legal.Contains(found_killers[i])                    // illegal
                && !sorted.Contains(found_killers[i])               // already added
                && b.PieceAt(found_killers[i].End()).Item2 == 6) {  // not quiet

                sorted.Add(found_killers[i]);
            }
        }

        // remaining quiet moves with their history value
        List<(Move, float)> quiets = [];

        for (int i = 0; i < legal.Count; i++) {
            if (sorted.Contains(legal[i]))
                continue;

            // if no history, 0 is set
            quiets.Add((legal[i], History.GetRep(b, legal[i])));
        }

        // very primitive sorting algorithm for the quiet moves,
        // sorts by their history value
        bool sortsMade = true;
        while (sortsMade) {
            sortsMade = false;

            for (int i = 1; i < quiets.Count; i++) {
                if (quiets[i].Item2 < quiets[i - 1].Item2) {

                    // switch places
                    (quiets[i], quiets[i - 1]) = (quiets[i - 1], quiets[i]);
                    sortsMade = true;
                }
            }
        }

        // add the quiet moves
        foreach ((Move q, _) in quiets)
            sorted.Add(q);

        //
        // TODO - FIX THIS
        //
        //if (sorted.Count > legal.Count) Console.WriteLine("fail");

        return sorted;
    }
}
