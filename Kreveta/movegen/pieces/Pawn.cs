//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.consts;

namespace Kreveta.movegen.pieces;

internal static class Pawn {
    // returns a bitboard of possible move end squares
    internal static ulong GetPawnPushTargets(byte sq, Color col, ulong empty) {
        ulong pawn = 1UL << sq;

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
    internal static unsafe ulong GetPawnCaptureTargets(byte sq, int enPassantSq, Color col, ulong occupiedOpp) {
        ulong targets = LookupTables.PawnCaptTargets[sq + (int)col * 64];

        // must be validated - otherwise illegal a8 promotions
        ulong enPassantMask = enPassantSq != 64
            ? 1UL << enPassantSq : 0UL;

        // & with occupied sqaures of opposite color and en passant square
        return targets & (occupiedOpp | enPassantMask);
    }
}