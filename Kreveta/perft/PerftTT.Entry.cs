//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using System.Runtime.InteropServices;
// ReSharper disable InconsistentNaming

namespace Kreveta.perft;

internal static partial class PerftTT {
    [StructLayout(LayoutKind.Explicit, Size = EntrySize)]
    private record struct Entry {
        // 8 bytes
        [field: FieldOffset(0)]
        internal ulong Hash;

        // 8 bytes
        [field: FieldOffset(sizeof(ulong))]
        private readonly ulong _flags;

        // both the depth and node count are stored as
        // a single uint64, or else the entry size would
        // become a number, which isn't a power of 2.
        internal readonly ulong Nodes {
            get => (_flags & 0xFFFFFFFFFFFFFF00) >> 8;
            init => _flags = value << 8 | _flags & 0x00000000000000FF;
        }

        // storing the depth here is even more important
        // than in the actual tt, because the number of
        // nodes is hugely dependent on it
        internal readonly byte Depth {
            get => (byte)(_flags & 0x00000000000000FF);
            init => _flags = value | _flags & 0xFFFFFFFFFFFFFF00;
        }
    }
}