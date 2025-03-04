/*
 *  Stockshrimp chess engine 1.0
 *  developed by ZlomenyMesic
 */

namespace Stockshrimp_1.movegen.pieces;

internal static class Knight {
    internal static ulong GetKnightMoves(ulong knight, Board b, int col, ulong free) {
        ulong moves = LookupTables.KnightMoves[BB.LS1B(knight)] ;
        return moves & free;
    }
}