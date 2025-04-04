//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

namespace Kreveta.movegen.pieces;

internal static class Pawn {
    // returns a bitboard of possible move end squares
    internal static ulong GetPawnPushes(ulong pawn, Color col, ulong empty) {

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
    internal static ulong GetPawnCaptures(ulong pawn, int en_p_sq, Color col, ulong occ_opp) {

        // in both cases we ensure the pawn hasn't jumped to the other side of the board

        // captures to the left
        ulong l = col == Color.WHITE
            ? pawn >> 9 & 0x7F7F7F7F7F7F7F7F
            : pawn << 7 & 0x7F7F7F7F7F7F7F7F;
        // captures to the right
        ulong r = col == Color.WHITE
            ? pawn >> 7 & 0xFEFEFEFEFEFEFEFE
            : pawn << 9 & 0xFEFEFEFEFEFEFEFE;

        ulong en_p_sq_mask = en_p_sq != 64 ? Consts.SqMask[en_p_sq] : 0;

        // & with occupied sqaures of opposite color and en passant square
        return (l | r) & (occ_opp | en_p_sq_mask);
    }

    // this method is only used to prevent nesting in a switch case
    // moves are added into the list
    internal static void AddPawnMoves(Color col, int start, int end, PType capt, List<Move> moves, int en_p_sq) {

        // in case of promotion
        if ((col == Color.WHITE && end < 8) | (col == Color.BLACK && end > 55)) {

            moves.Add(new(start, end, PType.PAWN, capt, PType.KNIGHT));
            moves.Add(new(start, end, PType.PAWN, capt, PType.BISHOP));
            moves.Add(new(start, end, PType.PAWN, capt, PType.ROOK));
            moves.Add(new(start, end, PType.PAWN, capt, PType.QUEEN));
        } 
        else if (end == en_p_sq) {

            // en passant - "pawn promotion"
            moves.Add(new(start, end, PType.PAWN, PType.NONE, PType.PAWN));
        } 
        else {

            // regular moves
            moves.Add(new(start, end, PType.PAWN, capt, PType.NONE));
        }
    }
}
