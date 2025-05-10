//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using System.Runtime.CompilerServices;

// ReSharper disable InconsistentNaming

namespace Kreveta;

// some bit operations
internal static class BB {

    // only used for initializing lookup tables, checks
    // whether the bit at the specified index is 1
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsBitSet(ulong bb, int index) {
        return (bb & ((ulong)1 << index)) != 0UL;
    }

    // Least Significant 1 Bit (also called bit scan forward
    // or trailing zero count). returns the index of the first
    // 1-bit in a ulong (the 1-bit with the lowest index)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static byte LS1B(ulong bb)
        => (byte)ulong.TrailingZeroCount(bb);

    // same as above, but also resets this bit
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static byte LS1BReset(ref ulong bb) {
        byte index = (byte)ulong.TrailingZeroCount(bb);
        bb &= bb - 1;
        return index;
    }
}
