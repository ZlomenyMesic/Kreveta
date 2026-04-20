//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using System.Runtime.CompilerServices;

// ReSharper disable InconsistentNaming

namespace Kreveta;

// some bit operations
internal static class BB {

    // Least Significant 1 Bit (also called bit scan forward
    // or trailing zero count). returns the index of the first
    // 1-bit in the ulong (the 1-bit with the lowest index)
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
    
    // used to round user input hash size to the nearest power of two
    // to allow bitwise masking instead of modulo in TT entry indexing
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int RoundToNearestPow2(int n, int max) {
        if (n <= 1) return 1;

        // closest smaller and higher power of two
        int lower = 1 << 31 - int.LeadingZeroCount(n);
        int upper = lower << 1;

        // pick closest
        int nearest = n - lower <= upper - n 
            ? lower : upper;

        return nearest > max
            ? max : nearest;

    }
}
