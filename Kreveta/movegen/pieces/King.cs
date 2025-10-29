//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.consts;

using System.Runtime.CompilerServices;

// ReSharper disable InconsistentNaming

namespace Kreveta.movegen.pieces;

internal static class King {

    // masks of squares, which need to be empty to allow
    // castling (squares between the king and the rook)
    private const ulong OOMask  = 0x6000000000000000;
    private const ulong OOOMask = 0x0E00000000000000;
    private const ulong ooMask  = 0x0000000000000060;
    private const ulong oooMask = 0x000000000000000E;

    // returns a bitboard of all moves targets (ending squares)
    // of a certain king (does not include castling)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static unsafe ulong GetKingTargets(ulong king, ulong free) {
        
        // same as with knights, the king targets are indexed
        // directly by the square index and then & with empty
        // and enemy-occupied squares to avoid friendly captures
        ulong targets = LookupTables.KingTargets[BB.LS1B(king)];
        return targets & free;
    }

    // this returns the move targets for castling only
    internal static ulong GetCastlingTargets(in Board board, Color col) {
        ulong occ = board.Occupied;
        bool isWhite = col == Color.WHITE;

        // first we check whether the side even holds
        // the required castling rights at all
        bool kingside =  ((byte)board.CastRights & (isWhite ? 0x1 : 0x4)) != 0; // K : k
        bool queenside = ((byte)board.CastRights & (isWhite ? 0x2 : 0x8)) != 0; // Q : q

        // now we ensure the squares between the king, and the rooks are empty
        kingside  &= (occ & (isWhite ? OOMask  : ooMask))  == 0UL;
        queenside &= (occ & (isWhite ? OOOMask : oooMask)) == 0UL;

        if (!(kingside || queenside))
            return 0UL;

        // starting squares of kings
        int start = isWhite ? 60 : 4;

        // and last but not least we check whether castling
        // would make us go through check, which is illegal
        // (moving into check is handled elsewhere)
        if (kingside)  kingside  &= board.IsMoveLegal(new(start, isWhite ? 61 : 5, PType.KING, PType.NONE, PType.NONE), col);
        if (queenside) queenside &= board.IsMoveLegal(new(start, isWhite ? 59 : 3, PType.KING, PType.NONE, PType.NONE), col);

        // for each side return the btiboard containing
        // the target square of a certain castling move
        return (kingside  ? isWhite 
            ? 0x4000000000000000UL 
            : 0x0000000000000020 : 0UL)

             | (queenside ? isWhite
            ? 0x0400000000000000UL 
            : 0x0000000000000004 : 0UL);
    }
}