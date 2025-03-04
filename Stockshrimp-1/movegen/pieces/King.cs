/*
 *  Stockshrimp chess engine 1.0
 *  developed by ZlomenyMesic
 */

namespace Stockshrimp_1.movegen.pieces;

internal static class King {
    internal static readonly ulong[][] CASTLING_MASK = [[0x6000000000000000, 0x0E00000000000000], [0x0000000000000060, 0x000000000000000E]];

    internal static ulong GetKingMoves(ulong king, Board b, int col, ulong free) {
        ulong moves = LookupTables.KingMoves[BB.LS1B(king)];
        return moves & free;
    }

    internal static ulong GetCastlingMoves(ulong king, Board b, int col) {
        ulong occ = b.Occupied();

        bool kingside =  (b.castlingFlags & (col == 0 ? 0x1 : 0x4)) != 0;
        bool queenside = (b.castlingFlags & (col == 0 ? 0x2 : 0x8)) != 0;

        if (kingside) kingside &= (occ & CASTLING_MASK[col][0]) == 0;
        if (queenside) queenside &= (occ & CASTLING_MASK[col][1]) == 0;

        int start = col == 0 ? 60 : 4;

        // check for check in square passed
        
        if (kingside)  kingside  &= b.IsLegal(new(start, col == 0 ? 61 : 5, 5, 6, 6));
        if (queenside) queenside &= b.IsLegal(new(start, col == 0 ? 59 : 3, 5, 6, 6));

        return (kingside ? (col == 0 ? Consts.SqMask[62] : Consts.SqMask[6]) : 0)
            | (queenside ? (col == 0 ? Consts.SqMask[58] : Consts.SqMask[2]) : 0);
    }
}