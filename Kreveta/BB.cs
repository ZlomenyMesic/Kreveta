//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
// ReSharper disable InconsistentNaming

namespace Kreveta;

internal static class BB {

    [ReadOnly(true)]
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private static readonly int[] DeBruijnTable = [
        0,  47, 1, 56,  48, 27, 2,  60,
        57, 49, 41, 37, 28, 16, 3,  61,
        54, 58, 35, 52, 50, 42, 21, 44,
        38, 32, 29, 23, 17, 11, 4,  62,
        46, 55, 26, 59, 40, 36, 15, 53,
        34, 51, 20, 43, 31, 22, 10, 45,
        25, 39, 14, 33, 19, 30, 9,  24,
        13, 18, 8,  12, 7,  6,  5,  63 
    ];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsBitSet(ulong bb, int index) {
        return (bb & ((ulong)1 << index)) != 0;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int LS1B(ulong bb) {
        if (bb == 0) return -1;
        return DeBruijnTable[((bb ^ (bb - 1)) * 0x03f79d71b4cb0a89) >> 58];
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int LS1BReset(ref ulong bb) {
        int index = LS1B(bb);
        bb &= bb - 1;
        return index;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int Popcount(ulong bb) {
        return (int)ulong.PopCount(bb);
    }
}
