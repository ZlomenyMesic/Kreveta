//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using System;
using System.Runtime.CompilerServices;
using Kreveta.movegen;

namespace Kreveta.moveorder.history;

internal static class StaticEvalDiffHistory {
    private static readonly short[] Diffs = new short[64 * 64];
    private static readonly short[] Count = new short[64 * 64];

    internal static void Clear() {
        Array.Clear(Diffs, 0, Diffs.Length);
        Array.Clear(Count, 0, Count.Length);
    }

    internal static void Add(Move move, int evalDiff) {
        int index = move.Start * 64 + move.End;

        Diffs[index] += (short)evalDiff;
        Count[index]++;

        // make sure no overflow happens. since we're keeping the average
        // difference, dividing by two only hurts accuracy, nothing more
        if (Diffs[index] is >= 10_000 or <= -10_000) {
            Diffs[index] /= 2;
            Count[index] /= 2;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int Get(Move move) {
        int index = move.Start * 64 + move.End;
        int diff  = Diffs[index];
        int count = Count[index];
        
        return diff / (count != 0 ? count : 1);
    }
}