//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.movegen;

using System.Runtime.InteropServices;
// ReSharper disable InconsistentNaming

namespace Kreveta.search.transpositions;

internal static partial class TranspositionTable {
    // this entry is stored for every position
    [StructLayout(LayoutKind.Explicit, Size = EntrySize)]
    private record struct Entry {

        // we store the board hash, because different hashes can
        // result in the same table index due to its size.
        [field: FieldOffset(0)]
        internal ulong Hash; // 8 bytes

        // the best move found in this position - used for move ordering
        [field: FieldOffset(8)]
        internal Move BestMove; // 4 bytes

        // the score of the position
        [field: FieldOffset(8 + 4)]
        internal short Score; // 2 bytes

        // the depth at which the search was performed
        // => higher depth means a more truthful score
        [field: FieldOffset(8 + 4 + 2)]
        internal sbyte Depth; // 1 byte

        // exact/lowerbound/upperbound score
        [field: FieldOffset(8 + 4 + 2 + 1)]
        internal ScoreFlags Flags; // 1 byte
    }

    // size of a single hash entry
    private const int EntrySize = 16;
}