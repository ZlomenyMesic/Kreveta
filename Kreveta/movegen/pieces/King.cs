//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using System.Runtime.CompilerServices;

namespace Kreveta.movegen.pieces;

internal static class King {
    internal static readonly ulong[][] CASTLING_MASK = [
        [0x6000000000000000, 0x0E00000000000000],
        [0x0000000000000060, 0x000000000000000E]
    ];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ulong GetKingMoves(ulong king, ulong free) {
        ulong moves = LookupTables.KingMoves[BB.LS1B(king)];
        return moves & free;
    }

    internal static ulong GetCastlingMoves(Board b, Color col) {
        ulong occ = b.Occupied();

        bool kingside =  ((byte)b.castRights & (col == Color.WHITE ? 0x1 : 0x4)) != 0; // K : k
        bool queenside = ((byte)b.castRights & (col == Color.WHITE ? 0x2 : 0x8)) != 0; // Q : q

        if (kingside) kingside &= (occ & CASTLING_MASK[(byte)col][0]) == 0;
        if (queenside) queenside &= (occ & CASTLING_MASK[(byte)col][1]) == 0;

        int start = col == Color.WHITE ? 60 : 4;

        // check for check on square passed
        if (kingside)  kingside  &= b.IsMoveLegal(new(start, col == Color.WHITE ? 61 : 5, PType.KING, PType.NONE, PType.NONE), col);
        if (queenside) queenside &= b.IsMoveLegal(new(start, col == Color.WHITE ? 59 : 3, PType.KING, PType.NONE, PType.NONE), col);

        return (kingside ? (col == Color.WHITE ? Consts.SqMask[62] : Consts.SqMask[6]) : 0)
            | (queenside ? (col == Color.WHITE ? Consts.SqMask[58] : Consts.SqMask[2]) : 0);
    }
}