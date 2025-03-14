/*
 *  Stockshrimp chess engine 1.0
 *  developed by ZlomenyMesic
 */

using Stockshrimp_1.movegen.pieces;

namespace Stockshrimp_1.movegen;

internal static class Movegen {
    internal static List<Move> GetLegalMoves(Board b) {

        int col = b.side_to_move;

        List<Move> moves = [];

        // generate all pseudo-legal moves
        GetPseudoLegalMoves(b, col, moves);

        // remove the illegal ones
        List<Move> legal = [];
        for (int i = 0; i < moves.Count; i++) {

            if (b.IsMoveLegal(moves[i], col)) {
                legal.Add(moves[i]);
            }
        }

        return legal;
    }

    internal static void GetPseudoLegalMoves(Board b, int col, List<Move> moves) {

        ulong occ_opp = b.Occupied(col == 0 ? 1 : 0);
        ulong occ = occ_opp | b.Occupied(col);
        ulong empty = ~occ;
        ulong free = empty | occ_opp;

        // loop through every piece type and add start the respective move search loop
        for (int i = 0; i < 6; i++) {
            LoopPiecesBB(b, b.pieces[col, i], i, col, moves, occ_opp, occ, empty, free);
        }

        if (!IsKingInCheck(b, col)) {
            ulong cast = King.GetCastlingMoves(b, col);
            LoopMovesBB(cast, b, BB.LS1B(b.pieces[col, 5]), 7, col, moves);
        }
    }

    internal static bool IsKingInCheck(Board b, int col) {

        int col_o = col == 0 ? 1 : 0;

        ulong king_sq = b.pieces[col, 5];

        // TODO - FIX
        if (king_sq == 0) return true;

        ulong occ_opp = b.Occupied(col_o);
        ulong occ = occ_opp | b.Occupied(col);

        ulong _p = Pawn.GetPawnCaptures(king_sq, b.en_passant_sq, col, occ_opp);
        ulong _n = Knight.GetKnightMoves(king_sq, ulong.MaxValue);
        ulong _b = Bishop.GetBishopMoves(king_sq, ulong.MaxValue, occ);
        ulong _r = Rook.GetRookMoves(king_sq, ulong.MaxValue, occ);
        ulong _k = King.GetKingMoves(king_sq, ulong.MaxValue);

        if ((_p & b.pieces[col_o, 0]) != 0) return true;
        if ((_n & b.pieces[col_o, 1]) != 0) return true;
        if ((_b & b.pieces[col_o, 2]) != 0) return true;
        if ((_r & b.pieces[col_o, 3]) != 0) return true;
        if (((_b | _r) & b.pieces[col_o, 4]) != 0) return true;
        if ((_k & b.pieces[col_o, 5]) != 0) return true;

        return false;
    }

    private static ulong GetMovesBB(ulong sq, Board b, int p, int col, ulong occ_opp, ulong occ, ulong empty, ulong free) {

        // return a bitboard of possible moves depending on the piece type
        return p switch {
            0 => Pawn.GetPawnPushes(sq, col, empty) | Pawn.GetPawnCaptures(sq, b.en_passant_sq, col, occ_opp),
            1 => Knight.GetKnightMoves(sq, free),
            2 => Bishop.GetBishopMoves(sq, free, occ),
            3 => Rook.GetRookMoves(sq, free, occ),

            // queen = bishop + rook
            4 => Bishop.GetBishopMoves(sq, free, occ) | Rook.GetRookMoves(sq, free, occ),
            5 => King.GetKingMoves(sq, free),
            _ => 0
        };
    }

    private static void LoopPiecesBB(Board b, ulong pieces, int p, int col, List<Move> moves, ulong occ_opp, ulong occ, ulong empty, ulong free) {
        ulong moves_bb;
        int start;

        // iteratively remove the pieces from the bitboard and generate their moves
        while (pieces != 0) {

            // "bit scan forward reset" also removes the least significant bit
            (pieces, start) = BB.LS1BReset(pieces);
            ulong sq = Consts.SqMask[start];

            // generate the moves
            moves_bb = GetMovesBB(sq, b, p, col, occ_opp, occ, empty, free);

            // loop the found moves and add them
            LoopMovesBB(moves_bb, b, start, p, col, moves);
        }
    }

    private static void LoopMovesBB(ulong moves_bb, Board b, int start, int p, int col, List<Move> moves) {
        int end;

        // same principle as above
        while (moves_bb != 0) {
            (moves_bb, end) = BB.LS1BReset(moves_bb);

            // get the potential capture piece type
            int capt = 6;
            if (p != 7) capt = b.PieceAt(end).Item2;

            // add the move
            AddMovesToList(p, col, start, end, capt, moves, b.en_passant_sq);
        }
    }

    private static void AddMovesToList(int p, int col, int start, int end, int capt, List<Move> moves, int en_p_sq) {

        // add the generated move to the list
        switch (p) {

            // pawns have a special designated method to prevent nesting (promotions)
            case 0: Pawn.AddPawnMoves(col, start, end, capt, moves, en_p_sq); return;

            // special case for castling
            case 7: moves.Add(new(start, end, 5, 6, 5)); return;

            // any other move
            default: moves.Add(new(start, end, p, capt, 6)); break;
        }
    }
}
