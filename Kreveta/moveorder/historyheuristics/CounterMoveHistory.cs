//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.consts;
using Kreveta.movegen;

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Kreveta.moveorder.historyheuristics;

// COUNTER MOVE HISTORY - a dynamic move-ordering heuristic, which makes use of the fact
// that many moves have a natural response irrespective to the position (e.g. certain
// trades can be waiting to happen through many plies, but the way they are performed
// stays the same). we have a board indexed by the color, starting and ending squares of
// a move, and we store the best response (counter) to this move. when ordering moves, we
// check if the counter is stored for the previous move, and we place it a little higher
internal static class CounterMoveHistory {

    // we cannot really use this at higher depths, since the results
    // can often be a bit misleading. only allowing retrieving at lower
    // depths actually increases the playing strength, though
    internal const int MaxRetrieveDepth = 2;

    // in order to store actually correct counters, we only save the
    // ones found at higher depths
    internal const int MinStoreDepth    = 4;

    // indexed [color, starting_square, ending_square]
    // !!! the color is of the side that is to play the counter, while
    // the starting and targets squares are of the other side's move !!!
    [ReadOnly(true), DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private static readonly Move[][][] CounterMoves = new Move[2][][];

    static CounterMoveHistory() => InitArrays();

    private static void InitArrays() {
        CounterMoves[(byte)Color.WHITE] = new Move[64][];
        CounterMoves[(byte)Color.BLACK] = new Move[64][];

        for (int i = 0; i < 64; i++) {
            CounterMoves[(byte)Color.WHITE][i] = new Move[64];
            CounterMoves[(byte)Color.BLACK][i] = new Move[64];
        }
    }

    // clear the table
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Clear() {
        for (int i = 0; i < 64; i++) {
            Array.Clear(CounterMoves[(byte)Color.WHITE][i]);
            Array.Clear(CounterMoves[(byte)Color.BLACK][i]);
        }
    }

    // store a new counter - we don't give higher priority to counters
    // found at higher depths (might change this later), so when there's
    // a new counter, we always overwrite the old one
    internal static void Add(Color col, Move previous, Move counter) {
        int start = previous.Start;
        int end   = previous.End;

        // as already mentioned, we always overwrite old counters
        CounterMoves[(byte)col][start][end] = counter;
    }

    // try to retrieve a counter using the previously played move
    // and the color that is currently on turn
    internal static Move Get(Color col, Move previous) {
        int start = previous.Start;
        int end   = previous.End;

        // if the counter isn't present, this simply returns the "default"
        return CounterMoves[(byte)col][start][end];
    }
}