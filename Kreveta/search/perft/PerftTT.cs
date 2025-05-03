//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

// ReSharper disable InconsistentNaming

namespace Kreveta.search.perft;

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

        internal readonly ulong Nodes {
            get  => (_flags & 0xFFFFFFFFFFFFFF00) >> 8;
            init => _flags = (value << 8) | (_flags & 0x00000000000000FF);
        }

        internal readonly byte Depth {
            get  => (byte)(_flags & 0x00000000000000FF);
            init => _flags = value | (_flags & 0xFFFFFFFFFFFFFF00);
        }
    }

    private static Entry* Table;

    // perftt.clear is called prior to every perft test,
    // so we don't have to initialize the table inline
    internal static void Clear() {
        Table = (Entry*)NativeMemory.AlignedAlloc(
            byteCount: TableSize * EntrySize,
            alignment: EntrySize);
    }

    // generate an index in the tt for a specific board hash
    // key collisions can (and will) occur, so we later also check the correctness of this index
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int HashIndex(ulong hash) {
        uint hash32 = (uint)hash ^ (uint)(hash >> 32);
        return (int)(hash32 & (TableSize - 1));
    }
    
    internal static void Store([In, ReadOnly(true)] in Board board, byte depth, ulong nodes) {
        ulong hash  = Zobrist.GetHash(board);
        int   index = HashIndex(hash);

        Entry entry = new() {
            Hash  = hash,
            Nodes = nodes,
            Depth = depth,
        };

        // store the new entry or overwrite the old one
        Table[index] = entry;
    }
    
    internal static bool TryGetNodes([In, ReadOnly(true)] in Board board, byte depth, out ulong nodes) {
        ulong hash  = Zobrist.GetHash(board);
        int   index = HashIndex(hash);

        nodes = Table[index].Nodes;
        return Table[index].Hash == hash && Table[index].Depth == depth;
    }
}
