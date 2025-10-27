//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.movegen;

using System.Runtime.InteropServices;
// ReSharper disable InconsistentNaming

namespace Kreveta.search.transpositions;

internal static partial class TT {
    // this entry is stored for every position
    [StructLayout(LayoutKind.Explicit, Size = EntrySize)]
    private record struct Entry {

        // we store the board hash, because different hashes can
        // result in the same table index due to its size.
        // (8 bytes)
        [field: FieldOffset(0)]
        internal ulong Hash;

        // the best move found in this position - used for move ordering
        // (4 bytes)
        [field: FieldOffset(8)]
        internal Move BestMove;

        // the score of the position
        // (2 bytes)
        [field: FieldOffset(8 + 4)]
        internal short Score;

        // the depth at which the search was performed
        // => higher depth means a more truthful score
        // (1 byte)
        [field: FieldOffset(8 + 4 + 2)]
        internal sbyte Depth;

        // (1 byte)
        [field: FieldOffset(8 + 4 + 2 + 1)]
        internal SpecialFlags Flags;
    }

    // size of a single hash entry
    private const int EntrySize = 16;
}