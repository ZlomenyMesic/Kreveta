//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.search.transpositions;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
// ReSharper disable InconsistentNaming

namespace Kreveta.perft;

// perft searches in chess engines usually don't use transposition tables to
// ensure the number of nodes displayed is truly correct (hash collisions may
// occur). however, in some cases we just want a rough idea about the number
// of nodes, and we don't demand an exact number, so it's useful to have this
// option available for fast computations

// just to prove my point, from the starting position, up until depth 7, the
// number of nodes found with perfttt is absolutely precise, and at depth 7
// the calculation is three times faster than without perftt
internal static unsafe partial class PerftTT {

    // size of a single entry in bytes
    private const int EntrySize = 16;

    // MUST be a power of 2 in order to allow & instead of modulo indexing
    private static int TableSize;
    
    // perftt.clear is called prior to every perft test,
    // so we don't have to initialize the table inline
    private static Entry* Table;

    // aligned reallocation doesn't work, so we free the
    // memory and then allocate it once again
    internal static void Clear() {
        if (Table is not null) {
            NativeMemory.AlignedFree(Table);
            Table = null;
        }
    }

    internal static void Init(int depth) {
        Clear();
        
        // these table sizes i have best experience with
        TableSize = depth switch {
            < 6 => 1_048_576,
            6   => 8_388_608,
            > 6 => 16_777_216
        };
        
        Table = (Entry*)NativeMemory.AlignedAlloc(
            byteCount: (nuint)TableSize * EntrySize,
            alignment: EntrySize);
    }

    // generate an index in the tt for a specific board hash
    // (identical principle as in the actual tt)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int HashIndex(ulong hash) {
        uint hash32 = (uint)hash ^ (uint)(hash >> 32);
        return (int)(hash32 & TableSize - 1);
    }

    // store a position along with the depth and number
    // of nodes. we don't care what has been stored prior
    // to this, we just overwrite everything
    internal static void Store(in Board board, byte depth, ulong nodes) {
        ulong hash = ZobristHash.Hash(in board);
        int index = HashIndex(hash);

        // store the new entry or overwrite the old one
        *(Table + index) = new Entry {
            Hash  = hash,
            Depth = depth,
            Nodes = nodes
        };
    }

    // try to find the same position at the SAME DEPTH (very important)
    internal static bool TryGetNodes(in Board board, byte depth, out ulong nodes) {
        ulong hash = ZobristHash.Hash(in board);
        int index = HashIndex(hash);

        nodes = (*(Table + index)).Nodes;
        return (*(Table + index)).Hash == hash && (*(Table + index)).Depth == depth;
    }
}
