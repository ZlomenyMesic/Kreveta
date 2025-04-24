//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using System.ComponentModel;

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Kreveta.movegen.pieces;

internal static class King {

    // masks of squares which need to be empty to allow
    // castling (squares between the king and the rook)
    private const ulong OOMask  = 0x6000000000000000;
    private const ulong OOOMask = 0x0E00000000000000;
    private const ulong ooMask  = 0x0000000000000060;
    private const ulong oooMask = 0x000000000000000E;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static unsafe ulong GetKingTargets(ulong king, ulong free) {
        ulong targets = LookupTables.KingTargets[BB.LS1B(king)];
        return targets & free;
    }

    internal static ulong GetCastlingTargets([NotNull, In, ReadOnly(true)] in Board board, Color col) {
        ulong occ = board.Occupied;

        bool kingside =  ((byte)board.castRights & (col == Color.WHITE ? 0x1 : 0x4)) != 0; // K : k
        bool queenside = ((byte)board.castRights & (col == Color.WHITE ? 0x2 : 0x8)) != 0; // Q : q

        kingside  &= (occ & (col == Color.WHITE ? OOMask  : ooMask))  == 0;
        queenside &= (occ & (col == Color.WHITE ? OOOMask : oooMask)) == 0;

        int start = col == Color.WHITE ? 60 : 4;

        // check for check on square passed
        if (kingside)  kingside  &= board.IsMoveLegal(new(start, col == Color.WHITE ? 61 : 5, PType.KING, PType.NONE, PType.NONE), col);
        if (queenside) queenside &= board.IsMoveLegal(new(start, col == Color.WHITE ? 59 : 3, PType.KING, PType.NONE, PType.NONE), col);

        return (kingside ? (col == Color.WHITE ? Consts.SqMask[62] : Consts.SqMask[6]) : 0)
            | (queenside ? (col == Color.WHITE ? Consts.SqMask[58] : Consts.SqMask[2]) : 0);
    }
}