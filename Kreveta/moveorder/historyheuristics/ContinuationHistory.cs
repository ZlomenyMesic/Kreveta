//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.movegen;

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Kreveta.moveorder.historyheuristics;

[SuppressMessage("Security", "CA5394:Do not use insecure randomness")]
internal static class ContinuationHistory {
    internal const int MaxRetrieveDepth = 6;

    // only save moves at higher depths to be precise
    internal const int MinStoreDepth    = 5;

    private static readonly ushort[] Hashes        = new ushort[4 * 64];
    private static readonly Move[]   Continuations = new Move[ushort.MaxValue + 1];

    internal static unsafe void Init() {
        var rnd = new Random();
        Span<byte> bytes = stackalloc byte[2];
        
        for (int i = 0; i < 256; i++) {
            rnd.NextBytes(bytes);
            Hashes[i] = BitConverter.ToUInt16(bytes);
        }
    }

    // clear the table
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Clear() {
        Array.Clear(Continuations, 0, Continuations.Length);
    }

    // store a new counter - we don't give higher priority to counters
    // found at higher depths (might change this later), so when there's
    // a new counter, we always overwrite the old one
    internal static void Add(Move twoPlyBack, Move onePlyBack, Move best) {
        ushort hash = HashMoves(twoPlyBack, onePlyBack);

        // as already mentioned, we always overwrite old counters
        Continuations[hash] = best;
    }

    // try to retrieve a counter using the previously played move,
    // and the color that is currently on turn
    internal static Move Get(Move twoPlyBack, Move onePlyBack, Move best) {
        ushort hash = HashMoves(twoPlyBack, onePlyBack);

        // if the counter isn't present, this simply returns the "default"
        return Continuations[hash];
    }

    private static ushort HashMoves(Move m1, Move m2) {
        ushort hash = Hashes[m1.Start];
        hash       ^= Hashes[m2.Start];
        hash       ^= Hashes[m1.End];
        hash       ^= Hashes[m2.End];
        return hash;
    }
}