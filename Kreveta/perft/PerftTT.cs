//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.search;

using System.ComponentModel;
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
internal static unsafe class PerftTT {

    // size of a single entry in bytes
    private const int EntrySize = 16;
    
    // MUST be a power of 2 in order to allow & instead of modulo indexing
    private const int TableSize = 1_048_576;

    [StructLayout(LayoutKind.Explicit, Size = EntrySize)]
    private record struct Entry {
        // 8 bytes
        [field: FieldOffset(0)]
        internal ulong Hash;

        // 8 bytes
        [field: FieldOffset(sizeof(ulong))]
        private ulong _flags;

        // both the depth and node count are stored as
        // a single int64, or else the entry size would
        // become a number, which isn't a power of 2.
        internal readonly ulong Nodes {
            get  => (_flags & 0xFFFFFFFFFFFFFF00) >> 8;
            init => _flags = (value << 8) | (_flags & 0x00000000000000FF);
        }

        // storing the depth here is even more important
        // than in the actual tt, because the number of
        // nodes is hugely dependent on it
        internal readonly byte Depth {
            get  => (byte)(_flags & 0x00000000000000FF);
            init => _flags = value | (_flags & 0xFFFFFFFFFFFFFF00);
        }
    }

    // perftt.clear is called prior to every perft test,
    // so we don't have to initialize the table inline
    private static Entry* Table;
    
    // aligned reallocation doesn't work, so we free the
    // memory and then allocate it once again
    internal static void Clear() {
        NativeMemory.AlignedFree(Table);

        Table = (Entry*)NativeMemory.AlignedAlloc(
            byteCount: TableSize * EntrySize,
            alignment: EntrySize);
    }

    // generate an index in the tt for a specific board hash
    // (identical principle as in the actual tt)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int HashIndex(ulong hash) {
        uint hash32 = (uint)hash ^ (uint)(hash >> 32);
        return (int)(hash32 & (TableSize - 1));
    }
    
    // store a position along with the depth and number
    // of nodes. we don't care what has been stored prior
    // to this, we just overwrite everything
    internal static void Store([In, ReadOnly(true)] in Board board, byte depth, ulong nodes) {
        ulong hash  = Zobrist.GetHash(board);
        int   index = HashIndex(hash);

        // store the new entry or overwrite the old one
        Table[index] = new Entry {
            Hash  = hash,
            Depth = depth,
            Nodes = nodes
        };
    }
    
    // try to find the same position at the SAME DEPTH (very important)
    internal static bool TryGetNodes([In, ReadOnly(true)] in Board board, byte depth, out ulong nodes) {
        ulong hash  = Zobrist.GetHash(board);
        int   index = HashIndex(hash);

        nodes = Table[index].Nodes;
        return Table[index].Hash == hash && Table[index].Depth == depth;
    }
}
