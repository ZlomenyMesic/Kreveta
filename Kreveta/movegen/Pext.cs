//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.consts;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.X86;

// ReSharper disable InconsistentNaming

namespace Kreveta.movegen;

internal static unsafe class Pext {

    // PEXT (Parallel Bits Extract) is a CPU instruction that maps bits
    // from a source bitboard based on a given mask to lower bit positions,
    // creating a dense index used for move lookups
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ulong PEXT(ulong val, ulong mask) {
        if (Consts.UseBMI2)
            return Bmi2.X64.ParallelBitExtract(val, mask);

        // this is a fallback implementation of PEXT
        ulong res = 0;
        for (ulong bb = 1; mask != 0; bb <<= 1) {
            if ((val & mask & (0UL - mask)) != 0)
                res |= bb;
            mask &= mask - 1;
        }

        return res;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ulong GetBishopTargets(int sq, ulong free, ulong occupied) {
        // mask of relevant squares - diag/antidiag, excluding edge squares
        ulong relevantMask = PextLookupTables.BishopMask[sq];
        
        // extract relevant occupancy bits into dense index using PEXT
        int index = (int)PEXT(occupied, relevantMask);
        
        return *(PextLookupTables.BishopTable 
                 + PextLookupTables.BishopOffset[sq] + index) & free;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ulong GetRookTargets(int sq, ulong free, ulong occupied) {
        // the same goes for the rook
        ulong relevantMask = PextLookupTables.RookMask[sq];
        int   index        = (int)PEXT(occupied, relevantMask);
        return *(PextLookupTables.RookTable + PextLookupTables.RookOffset[sq] + index) & free;
    }
}