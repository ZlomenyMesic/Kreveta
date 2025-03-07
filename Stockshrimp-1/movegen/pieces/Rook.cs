/*
 *  Stockshrimp chess engine 1.0
 *  developed by ZlomenyMesic
 */

namespace Stockshrimp_1.movegen.pieces;

internal static class Rook {
    internal static ulong GetRookMoves(ulong rook, ulong free, ulong occupied) {
        ulong moves = 0;

        int sq = BB.LS1B(rook);

        int occupancy = (int)((occupied & Consts.SixBitRankMask[sq >> 3]) >> (8 * (sq >> 3)));
        moves |= LookupTables.RankMoves[sq][(occupancy >> 1) & 63];

        occupancy = (int)((occupied & Consts.SixBitFileMask[sq & 7]) * Consts.FileMagic[sq & 7] >> 56);
        moves |= LookupTables.FileMoves[sq][(occupancy >> 1) & 63];

        return moves & free;
    }
}
