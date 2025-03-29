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

internal static class Bishop {
    internal static ulong GetBishopMoves(ulong bishop, ulong free, ulong occupied) {
        ulong moves = 0;

        int sq = BB.LS1B(bishop);

        int diag = 7 + (sq >> 3) - (sq & 7);
        int occupancy = (int)((occupied & Consts.AntidiagMask[diag]) * Consts.AntidiagMagic[diag] >> 56);
        moves |= LookupTables.AntidiagMoves[sq][(occupancy >> 1) & 63];

        diag = (sq >> 3) + (sq & 7);
        occupancy = (int)((occupied & Consts.DiagMask[diag]) * Consts.DiagMagic[diag] >> 56);
        moves |= LookupTables.DiagMoves[sq][(occupancy >> 1) & 63];

        return moves & free;
    }
}
