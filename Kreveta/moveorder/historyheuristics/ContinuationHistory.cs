//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

#pragma warning disable CA5394

using Kreveta.movegen;

using System;
using System.Runtime.CompilerServices;

namespace Kreveta.moveorder.historyheuristics;

internal static class ContinuationHistory {
    internal static int MaxRetrieveDepth = 4;

    // only save moves at higher depths to be precise
    internal static int MinStoreDepth    = 4;

    internal static int Seed      = 66;
    private const   int TableSize = 1;// 524_288;

    private static readonly uint[] Hashes        = new uint[4 * 64 + 2 * 6];
    private static readonly Move[] Continuations = new Move[TableSize];

    internal static unsafe void Init() {
        var rnd = new Random(Seed);
        Span<byte> bytes = stackalloc byte[4];
        
        for (int i = 0; i < 256; i++) {
            rnd.NextBytes(bytes);
            Hashes[i] = BitConverter.ToUInt32(bytes);
        }
    }

    // clear the table
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Clear() {
        Array.Clear(Continuations, 0, Continuations.Length);
    }

    // store a new continuation - same as with counters, there
    // is no priority measure, old continuations get overwritten
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Add(Move twoPlyBack, Move onePlyBack, Move best) {
        uint hash = HashMoves(twoPlyBack, onePlyBack);

        // as already mentioned, we always overwrite old continuations
        Continuations[hash] = best;
    }

    // try to retrieve the continuation
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Move Get(Move twoPlyBack, Move onePlyBack) {
        uint hash = HashMoves(twoPlyBack, onePlyBack);

        // if the continuation isn't present, this returns default
        return Continuations[hash];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint HashMoves(Move m1, Move m2) {
        uint hash = Hashes[m1.Start];
        hash     ^= Hashes[m2.Start];
        hash     ^= Hashes[m1.End];
        hash     ^= Hashes[m2.End];

        hash ^= Hashes[256 + (int)m1.Piece];
        hash ^= Hashes[262 + (int)m2.Piece];
        
        return hash & TableSize - 1;
    }
}

#pragma warning restore CA5394