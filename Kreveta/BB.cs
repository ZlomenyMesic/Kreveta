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

    // [ReadOnly(true), DebuggerBrowsable(DebuggerBrowsableState.Never)]
    // private static readonly sbyte[] DeBruijnTable = [
    //     0,  47, 1, 56,  48, 27, 2,  60,
    //     57, 49, 41, 37, 28, 16, 3,  61,
    //     54, 58, 35, 52, 50, 42, 21, 44,
    //     38, 32, 29, 23, 17, 11, 4,  62,
    //     46, 55, 26, 59, 40, 36, 15, 53,
    //     34, 51, 20, 43, 31, 22, 10, 45,
    //     25, 39, 14, 33, 19, 30, 9,  24,
    //     13, 18, 8,  12, 7,  6,  5,  63 
    // ];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsBitSet(ulong bb, int index) {
        return (bb & ((ulong)1 << index)) != 0UL;
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static byte LS1B(ulong bb) {
        //if (bb == 0) return -1;
        //return DeBruijnTable[((bb ^ (bb - 1)) * 0x03F79D71B4CB0A89) >> 58];
        return (byte)ulong.TrailingZeroCount(bb);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static byte LS1BReset(ref ulong bb) {
        byte index = (byte)ulong.TrailingZeroCount(bb);
        bb &= bb - 1;
        return index;
    }
}
