//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Kreveta.search.perft;

internal static class PerftTT {

    private const int EntrySize = 17;
    private const int TableSize = 1048576;

    [StructLayout(LayoutKind.Explicit, Size = EntrySize)]
    private record struct Entry {
        // 8 bytes
        [field: FieldOffset(0)]
        internal ulong Hash;

        // 8 bytes
        [field: FieldOffset(sizeof(ulong))]
        internal ulong Nodes;

        // 1 byte
        [field: FieldOffset(2 * sizeof(ulong))]
        internal sbyte Depth;
    }

    private static readonly Entry[] Table = new Entry[TableSize];

    internal static void Clear() {
        Array.Clear(Table, 0, TableSize);
    }

    // generate an index in the tt for a specific board hash
    // key collisions can (and will) occur, so we later also check the correctness of this index
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int HashIndex(ulong hash) {
        uint hash32 = (uint)hash ^ (uint)(hash >> 32);
        return (int)(hash32 % TableSize);
    }

    internal static void Store([NotNull, In, ReadOnly(true)] in Board board, int depth, ulong nodes) {
        ulong hash = Zobrist.GetHash(board);
        int i = HashIndex(hash);

        Entry entry = new() {
            Hash  = hash,
            Nodes = nodes,
            Depth = (sbyte)depth,
        };

        // store the new entry or overwrite the old one
        Table[i] = entry;
    }

    internal static bool TryGetNodes([NotNull, In, ReadOnly(true)] in Board board, int depth, out ulong nodes) {
        ulong hash = Zobrist.GetHash(board);
        int i = HashIndex(hash);

        nodes = Table[i].Nodes;
        return Table[i].Hash == hash && Table[i].Depth == depth;
    }
}
