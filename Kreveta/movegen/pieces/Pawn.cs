//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

namespace Kreveta.movegen.pieces;

internal static class Pawn {
    // returns a bitboard of possible move end squares
    internal static ulong GetPawnPushTargets(ulong pawn, Color col, ulong empty) {

        // chess speaks for itself
        ulong singlePush = col == Color.WHITE
            ? pawn >> 8 & empty
            : pawn << 8 & empty;

        // another single push but make sure the pawn moved from the starting position
        ulong doublePush = col == Color.WHITE
            ? singlePush >> 8 & empty & 0x000000FF00000000
            : singlePush << 8 & empty & 0x00000000FF000000;

        return singlePush | doublePush;
    }

    // returns a bitboard of possible capture end squares
    internal static ulong GetPawnCaptureTargets(ulong pawn, int enPassantSq, Color col, ulong occupiedOpp) {

        // in both cases we ensure the pawn hasn't jumped to the other side of the board

        // captures to the left
        ulong left = col == Color.WHITE
            ? pawn >> 9 & 0x7F7F7F7F7F7F7F7F
            : pawn << 7 & 0x7F7F7F7F7F7F7F7F;

        // captures to the right
        ulong right = col == Color.WHITE
            ? pawn >> 7 & 0xFEFEFEFEFEFEFEFE
            : pawn << 9 & 0xFEFEFEFEFEFEFEFE;

        ulong enPassantMask = enPassantSq != 64 
            ? Consts.SqMask[enPassantSq] : 0;

        // & with occupied sqaures of opposite color and en passant square
        return (left | right) & (occupiedOpp | enPassantMask);
    }
}
