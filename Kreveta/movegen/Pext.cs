//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;

namespace Kreveta.movegen;

internal static class Pext {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ulong GetBishopTargets(int sq, ulong free, ulong occupied) {
        // mask of relevant squares - diag/antidiag, excluding edge squares
        ulong relevantMask = PextLookupTables.BishopMask[sq];
        
        // extract relevant occupancy bits into dense index using PEXT.
        // i have no idea how or why this works, but it indeed does :)
        int index = (int)Bmi2.X64.ParallelBitExtract(occupied, relevantMask);
        
        // start of the bishop attack table
        ref ulong tableStart = ref MemoryMarshal.GetArrayDataReference(PextLookupTables.FlatBishopTable);
        
        // offset + extracted index = exact attack bitboard from precomputed table.
        // this is directly ANDed with available squares to avoid own color captures
        return Unsafe.Add(ref tableStart, PextLookupTables.BishopOffset[sq] + index)
            & free;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ulong GetRookTargets(int sq, ulong free, ulong occupied) {
        // the same goes for the rook
        ulong     relevantMask = PextLookupTables.RookMask[sq];
        int       index        = (int)Bmi2.X64.ParallelBitExtract(occupied, relevantMask);
        ref ulong tableStart   = ref MemoryMarshal.GetArrayDataReference(PextLookupTables.FlatRookTable);
        return Unsafe.Add(ref tableStart, PextLookupTables.RookOffset[sq] + index) & free;
    }
}