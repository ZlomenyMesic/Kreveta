//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using System.Numerics;
using System.Runtime.InteropServices;

namespace Kreveta.movegen;

internal static unsafe class PextLookupTables {
    internal static readonly ulong* BishopTable        = (ulong*)NativeMemory.AlignedAlloc(5_248   * sizeof(ulong), 64);
    internal static readonly ulong* RookTable          = (ulong*)NativeMemory.AlignedAlloc(102_400 * sizeof(ulong), 64);
    internal static readonly ulong* BishopMask         = (ulong*)NativeMemory.AlignedAlloc(64      * sizeof(ulong), 64);
    internal static readonly ulong* RookMask           = (ulong*)NativeMemory.AlignedAlloc(64      * sizeof(ulong), 64);
    internal static readonly int*   BishopOffset       = (int*)  NativeMemory.AlignedAlloc(64      * sizeof(int),   64);
    internal static readonly int*   RookOffset         = (int*)  NativeMemory.AlignedAlloc(64      * sizeof(int),   64);
    private  static readonly int*   BishopRelevantBits = (int*)  NativeMemory.AlignedAlloc(64      * sizeof(int),   64);
    private  static readonly int*   RookRelevantBits   = (int*)  NativeMemory.AlignedAlloc(64      * sizeof(int),   64);

    internal static void Init() {
        for (int sq = 0; sq < 64; sq++) {
            BishopMask        [sq] = ComputeBishopMask(sq);
            RookMask          [sq] = ComputeRookMask(sq);
            BishopRelevantBits[sq] = BitOperations.PopCount(BishopMask[sq]);
            RookRelevantBits  [sq] = BitOperations.PopCount(RookMask[sq]);
        }

        ComputeOffsets(BishopRelevantBits, BishopOffset);
        ComputeOffsets(RookRelevantBits,   RookOffset);
        
        // these are not needed anymore
        NativeMemory.AlignedFree(BishopRelevantBits);
        NativeMemory.AlignedFree(RookRelevantBits);
        
        BuildTables(bishop: true);
        BuildTables(bishop: false);
    }
    
    private static void ComputeOffsets(int* relevantBits, int* offsets) {
        int total = 0;

        for (int sq = 0; sq < 64; sq++) {
            offsets[sq] = total;
            total += 1 << relevantBits[sq];
        }
    }

    private static ulong ComputeRookMask(int square) {
        int   rank = square / 8;
        int   file = square % 8;
        ulong mask = 0;

        // ranks, excluding edges
        for (int f = file + 1; f < 7; f++) mask |= 1UL << rank * 8 + f;
        for (int f = file - 1; f > 0; f--) mask |= 1UL << rank * 8 + f;

        // files, excluding edges
        for (int r = rank + 1; r < 7; r++) mask |= 1UL << r * 8 + file;
        for (int r = rank - 1; r > 0; r--) mask |= 1UL << r * 8 + file;

        return mask;
    }

    private static ulong ComputeBishopMask(int square) {
        int   rank = square / 8;
        int   file = square % 8;
        ulong mask = 0;

        // up-right
        for (int r = rank + 1, f = file + 1; r < 7 && f < 7; r++, f++)
            mask |= 1UL << r * 8 + f;

        // up-left
        for (int r = rank + 1, f = file - 1; r < 7 && f > 0; r++, f--)
            mask |= 1UL << r * 8 + f;

        // down-right
        for (int r = rank - 1, f = file + 1; r > 0 && f < 7; r--, f++)
            mask |= 1UL << r * 8 + f;

        // down-left
        for (int r = rank - 1, f = file - 1; r > 0 && f > 0; r--, f--)
            mask |= 1UL << r * 8 + f;

        return mask;
    }

    private static void BuildTables(bool bishop) {
        var maskArr = bishop ? BishopMask   : RookMask;
        var offsets = bishop ? BishopOffset : RookOffset;
        var table   = bishop ? BishopTable  : RookTable;

        for (int sq = 0; sq < 64; sq++) {
            ulong mask       = maskArr[sq];
            int   baseOffset = offsets[sq];

            ulong subset = 0;

            do {
                int index = (int)Pext.PEXT(subset, mask);
                table[baseOffset + index] = SlidingAttacks(sq, subset, bishop);

                subset = subset - mask & mask;
            }
            while (subset != 0);
        }
    }
    
    private static ulong SlidingAttacks(int sq, ulong blockers, bool bishop) {
        ulong attacks = 0;
        int   r       = sq / 8;
        int   f       = sq % 8;

        if (bishop) {
            Trace(ref attacks, blockers, r, f,  1,  1);
            Trace(ref attacks, blockers, r, f,  1, -1);
            Trace(ref attacks, blockers, r, f, -1,  1);
            Trace(ref attacks, blockers, r, f, -1, -1);
        } else {
            Trace(ref attacks, blockers, r, f,  1,  0);
            Trace(ref attacks, blockers, r, f, -1,  0);
            Trace(ref attacks, blockers, r, f,  0,  1);
            Trace(ref attacks, blockers, r, f,  0, -1);
        }

        return attacks;
    }

    private static void Trace(ref ulong attacks, ulong blockers, int r, int f, int dr, int df) {
        r += dr; f += df;

        while ((uint)r < 8 && (uint)f < 8) {
            ulong sq = 1UL << r * 8 + f;
            attacks |= sq;

            if ((blockers & sq) != 0)
                break;

            r += dr; f += df;
        }
    }
    
    internal static void Free() {
        NativeMemory.AlignedFree(BishopTable);
        NativeMemory.AlignedFree(RookTable);
        NativeMemory.AlignedFree(BishopMask);
        NativeMemory.AlignedFree(RookMask);
        NativeMemory.AlignedFree(BishopOffset);
        NativeMemory.AlignedFree(RookOffset);
    }
}