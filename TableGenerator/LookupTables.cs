//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using System.Numerics;

namespace TableGenerator;

internal static class LookupTables {
    internal static          ulong[] FlatBishopTable    = null!;
    internal static          ulong[] FlatRookTable      = null!;
    internal static readonly ulong[] BishopMask         = new ulong[64];
    internal static readonly ulong[] RookMask           = new ulong[64];
    internal static readonly int[]   BishopOffset       = new int[64];
    internal static readonly int[]   RookOffset         = new int[64];
    internal static readonly byte[]  BishopRelevantBits = new byte[64];
    internal static readonly byte[]  RookRelevantBits   = new byte[64];

    internal static void Init() {
        GenerateMasks();
        BuildBishopTables();
        BuildRookTables();
    }

    private static void GenerateMasks() {
        for (int sq = 0; sq < 64; sq++) {
            BishopMask[sq] = ComputeBishopRelevantMask(sq);
            RookMask[sq] = ComputeRookRelevantMask(sq);

            BishopRelevantBits[sq] = (byte)BitOperations.PopCount(BishopMask[sq]);
            RookRelevantBits[sq]   = (byte)BitOperations.PopCount(RookMask[sq]);
        }

        FlatBishopTable = AllocateFlatTable(BishopRelevantBits, BishopOffset);
        FlatRookTable   = AllocateFlatTable(RookRelevantBits, RookOffset);
    }

    
    // allocate flat contiguous memory for one slider table type
    private static ulong[] AllocateFlatTable(byte[] relevantBitsPerSquare, int[] squareOffsets) {
        int totalEntries = 0;

        for (int square = 0; square < 64; square++) {
            squareOffsets[square] = totalEntries;
            totalEntries += 1 << relevantBitsPerSquare[square];
        }

        return new ulong[totalEntries];
    }

    private static ulong ComputeRookRelevantMask(int square) {
        int rank = square / 8;
        int file = square % 8;
        ulong mask = 0;

        // ranks, excluding edges
        for (int f = file + 1; f < 7; f++) mask |= 1UL << (rank * 8 + f);
        for (int f = file - 1; f > 0;  f--) mask |= 1UL << (rank * 8 + f);

        // files, excluding edges
        for (int r = rank + 1; r < 7; r++) mask |= 1UL << (r * 8 + file);
        for (int r = rank - 1; r > 0;  r--) mask |= 1UL << (r * 8 + file);

        return mask;
    }

    private static ulong ComputeBishopRelevantMask(int square) {
        int rank = square / 8;
        int file = square % 8;
        ulong mask = 0;

        // up-right
        for (int r = rank + 1, f = file + 1; r < 7 && f < 7; r++, f++)
            mask |= 1UL << (r * 8 + f);

        // up-left
        for (int r = rank + 1, f = file - 1; r < 7 && f > 0; r++, f--)
            mask |= 1UL << (r * 8 + f);

        // down-right
        for (int r = rank - 1, f = file + 1; r > 0 && f < 7; r--, f++)
            mask |= 1UL << (r * 8 + f);

        // down-left
        for (int r = rank - 1, f = file - 1; r > 0 && f > 0; r--, f--)
            mask |= 1UL << (r * 8 + f);

        return mask;
    }

    private static void BuildBishopTables() {
        for (int sq = 0; sq < 64; sq++) {
            ulong mask = BishopMask[sq];
            int blockerCount = BishopRelevantBits[sq];
            int maxBlockerCombinations = 1 << blockerCount;
            int baseOffset = BishopOffset[sq];

            for (int index = 0; index < maxBlockerCombinations; index++) {
                ulong blockers = ExpandBlockerBitsToBoardMask(index, mask);
                FlatBishopTable[baseOffset + index] = ComputeSlidingAttacks(sq, blockers, bishop: true);
            }
        }
    }

    private static void BuildRookTables() {
        for (int square = 0; square < 64; square++) {
            ulong mask = RookMask[square];
            int blockerCount = RookRelevantBits[square];
            int maxBlockerCombinations = 1 << blockerCount;
            int baseOffset = RookOffset[square];

            for (int index = 0; index < maxBlockerCombinations; index++) {
                ulong blockers = ExpandBlockerBitsToBoardMask(index, mask);
                FlatRookTable[baseOffset + index] = ComputeSlidingAttacks(square, blockers, bishop: false);
            }
        }
    }
    
    // convert compact bit index into real board mask (inverse of PEXT).
    private static ulong ExpandBlockerBitsToBoardMask(int index, ulong relevantMask) {
        ulong result = 0;
        int bitPosition = 0;

        while (relevantMask != 0) {
            ulong leastBit = relevantMask & (ulong)-(long)relevantMask;
            relevantMask ^= leastBit;

            if (((index >> bitPosition) & 1) != 0)
                result |= leastBit;

            bitPosition++;
        }

        return result;
    }

    // simple directional ray tracing slider logic (bishop or rook).
    private static ulong ComputeSlidingAttacks(int square, ulong blockers, bool bishop) {
        ulong attacks = 0;
        int rank = square / 8;
        int file = square % 8;

        int[] deltaRank = bishop ? new[] { 1,  1, -1, -1 } : new[] { 1, -1, 0, 0 };
        int[] deltaFile = bishop ? new[] { 1, -1,  1, -1 } : new[] { 0,  0, 1, -1 };

        for (int d = 0; d < 4; d++) {
            int r = rank + deltaRank[d];
            int f = file + deltaFile[d];

            while (r >= 0 && r < 8 && f >= 0 && f < 8) {
                ulong squareMask = 1UL << (r * 8 + f);
                attacks |= squareMask;

                if ((blockers & squareMask) != 0)
                    break;

                r += deltaRank[d];
                f += deltaFile[d];
            }
        }

        return attacks;
    }
}