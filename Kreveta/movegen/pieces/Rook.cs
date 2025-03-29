/*
 * |============================|
 * |                            |
 * |    Kreveta chess engine    |
 * | engineered by ZlomenyMesic |
 * | -------------------------- |
 * |      started 4-3-2025      |
 * | -------------------------- |
 * |                            |
 * | read README for additional |
 * | information about the code |
 * |    and usage that isn't    |
 * |  included in the comments  |
 * |                            |
 * |============================|
 */

namespace Kreveta.movegen.pieces;

internal static class Rook {
    internal static ulong GetRookMoves(ulong rook, ulong free, ulong occupied) {
        ulong moves = 0;

        int sq = BB.LS1B(rook);

        int occupancy = (int)((occupied & Consts.RelevantRankMask[sq >> 3]) >> (8 * (sq >> 3)));
        moves |= LookupTables.RankMoves[sq][(occupancy >> 1) & 63];

        occupancy = (int)((occupied & Consts.RelevantFileMask[sq & 7]) * Consts.FileMagic[sq & 7] >> 56);
        moves |= LookupTables.FileMoves[sq][(occupancy >> 1) & 63];

        return moves & free;
    }
}
