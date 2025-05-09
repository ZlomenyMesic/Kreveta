//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using System.Runtime.CompilerServices;

// ReSharper disable InconsistentNaming

namespace Kreveta;

internal static class BB {

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsBitSet(ulong bb, int index) {
        return (bb & ((ulong)1 << index)) != 0UL;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static byte LS1B(ulong bb)
        => (byte)ulong.TrailingZeroCount(bb);


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static byte LS1BReset(ref ulong bb) {
        byte index = (byte)ulong.TrailingZeroCount(bb);
        bb &= bb - 1;
        return index;
    }
}
