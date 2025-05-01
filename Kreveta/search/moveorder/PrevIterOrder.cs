//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using System;
using Kreveta.movegen;

namespace Kreveta.search.moveorder;

// at earlier iterations of the search, we order the
// root moves based on their scores from the previous
// iteration. when using this for all iterations, we
// actually slow the search down, because not obvious
// critical moves take longer to find, but this allows
// us to speed up the early iteration, which does in
// fact gain us quite some strength
internal static class PrevIterOrder {

    // the maximum iteration depth to allow retrieving for moveorder
    internal const int MaxRetrieveDepth = 6;
    
    // we store the moves along with their scores
    private static (Move, short)[] _moves = [];
    private static int             _cur;

    // this tells us another iteration is going to pass us moves
    internal static void PrepareForNextIter(int moveCount) {
        _moves = new (Move, short)[moveCount];
        _cur   = 0;
    }

    // store the move and its score
    internal static void AddMove(Move move, short score)
        => _moves[_cur++] = (move, score);

    // this gets called from moveorder if the depth fits our criteria
    internal static Move[] GetOrderedMoves() {
        
        // once again we have this retarded sorting algorithm.
        // we only sort the moves when retrieving them to ensure
        // no time is wasted on sorting
        bool sortsMade = true;
        while (sortsMade) {

            // once we haven't switched any moves, break the loop
            sortsMade = false;

            for (int i = 1; i < _moves.Length; i++) {
                if (_moves[i].Item2 > _moves[i - 1].Item2) {
                    (_moves[i], _moves[i - 1]) = (_moves[i - 1], _moves[i]);
                    sortsMade = true;
                }
            }
        }
        
        Move[] sorted = new Move[_moves.Length];
        for (int i = 0; i < _moves.Length; i++)
            sorted[i] = _moves[i].Item1;

        return sorted;
    }
}