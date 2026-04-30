//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using System.Runtime.InteropServices;

namespace Kreveta.polyglot;

internal static partial class Polyglot {

    // yup this is truly one of the sizes in the world
    private const int EntrySize = 16;

    // each of the polyglot entries contains the key (zobrist
    // hash of the position), the best move from that position,
    // and the move's weight (how good it is).
    [StructLayout(LayoutKind.Explicit, Size = EntrySize - 4)]
    private record struct PolyglotEntry {
        [FieldOffset(0)]  internal ulong  Key;
        [FieldOffset(8)]  internal ushort Move;
        [FieldOffset(10)] internal float  Weight;
    }
}