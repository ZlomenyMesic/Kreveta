/*
 *  Stockshrimp chess engine 1.0
 *  developed by ZlomenyMesic
 */

namespace Stockshrimp_1.movegen.pieces;

internal static class Pawn {
    // returns a bitboard of possible move end squares
    internal static ulong GetPawnPushes(ulong pawn, Board b, int col, ulong empty) {

        // chess speaks for itself
        ulong singlePush = col == 0
            ? pawn >> 8 & empty
            : pawn << 8 & empty;

        // another single push but make sure the pawn moved from the starting position
        ulong doublePush = col == 0
            ? singlePush >> 8 & empty & 0x000000FF00000000
            : singlePush << 8 & empty & 0x00000000FF000000;

        return singlePush | doublePush;
    }

    // returns a bitboard of possible capture end squares
    internal static ulong GetPawnCaptures(ulong pawn, Board b, int col, ulong occ_opp) {

        // in both cases we ensure the pawn hasn't jumped to the other side of the board

        // captures to the left
        ulong l = col == 0
            ? pawn >> 9 & 0x7F7F7F7F7F7F7F7F
            : pawn << 7 & 0x7F7F7F7F7F7F7F7F;
        // captures to the right
        ulong r = col == 0
            ? pawn >> 7 & 0xFEFEFEFEFEFEFEFE
            : pawn << 9 & 0xFEFEFEFEFEFEFEFE;

        // & with occupied sqaures of opposite color
        return (l | r) & (occ_opp | Consts.SqMask[b.enPassantSquare]);
    }

    // this method is only used to prevent nesting in a switch case
    // moves are added into the list
    internal static void AddPawnMoves(int col, int start, int end, int capt, List<Move> moves, int en_p_sq) {

        // in case of promotion
        if ((col == 0 && end < 8) | (col == 1 && end > 55)) {

            // loop through possible promotions - N B R Q
            for (int i = 1; i <= 4; i++) {
                moves.Add(new(start, end, 0, capt, i));
            }
        } 
        else if (end == en_p_sq) {

            // en passant
            moves.Add(new(start, end, 0, 6, 0));
        } 
        else {

            // regular moves
            moves.Add(new(start, end, 0, capt, 6));
        }
    }
}
