//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.movegen;
using System.Diagnostics.CodeAnalysis;

namespace Kreveta.search.moveorder;

internal static class CounterMoveHistory {
    private static readonly Move[,,] CounterMoves = new Move[2, 64, 64];

    internal static void Clear() {
        Array.Clear(CounterMoves);
    }

    internal static void Add(Color col, Move previous, Move counter) {
        int start = previous.Start;
        int end   = previous.End;

        CounterMoves[(byte)col, start, end] = counter;
    }

    internal static Move Get(Color col, Move previous) {
        int start = previous.Start;
        int end   = previous.End;

        return CounterMoves[(byte)col, start, end];
    }
}