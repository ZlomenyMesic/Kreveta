/*
 *  Stockshrimp chess engine 1.0
 *  developed by ZlomenyMesic
 */

using Stockshrimp_1.movegen.pieces;

namespace Stockshrimp_1.movegen;

internal static class Movegen {
    internal static Move[] GetLegalMoves(Board b, int col) {
        List<Move> moves = [];

        // generate all pseudo-legal moves
        AddAllMoves(b, col, moves);

        // remove the illegal ones
        List<Move> legal = [];
        for (int i = 0; i < moves.Count; i++) {

            bool l = b.IsLegal(moves[i]);

            if (l) {
                legal.Add(moves[i]);
            }
        }

        return [.. legal];
    }

    internal static bool IsKingChecked(Board b, int col) {

        int col_o = col == 0 ? 1 : 0;

        ulong sq = b.pieces[col, 5];

        ulong _p = GetMovesBB(sq, b, 0, col);
        if ((_p & b.pieces[col_o, 0]) != 0) return true;

        ulong _n = GetMovesBB(sq, b, 1, col);
        if ((_n & b.pieces[col_o, 1]) != 0) return true;

        ulong _b = GetMovesBB(sq, b, 2, col);
        if ((_b & b.pieces[col_o, 2]) != 0) return true;

        ulong _r = GetMovesBB(sq, b, 3, col);
        if ((_r & b.pieces[col_o, 3]) != 0) return true;
        if (((_b | _r) & b.pieces[col_o, 4]) != 0) return true;

        ulong _k = GetMovesBB(sq, b, 5, col);
        if ((_k & b.pieces[col_o, 5]) != 0) return true;

        return false;
    }

    private static void AddAllMoves(Board b, int col, List<Move> moves) {

        // loop through every piece type and add start the respective move search loop
        for (int i = 0; i < 6; i++) {
            LoopPiecesBB(b, b.pieces[col, i], i, col, moves);
        }

        ulong cast = King.GetCastlingMoves(b.pieces[col, 5], b, col);
        LoopMovesBB(cast, b, BB.LS1B(b.pieces[col, 5]), 7, col, moves);
    }

    private static ulong GetMovesBB(ulong sq, Board b, int p, int col) {

        // occupied by opposite color
        ulong o = b.Occupied(col == 0 ? 1 : 0);

        // empty squares
        ulong e = b.Empty();

        // occupied by both
        ulong oo = ~e;

        // both combined
        ulong free = o | e;

        // return a bitboard of possible moves depending on the piece type
        return p switch {
            0 => Pawn.GetPawnPushes(sq, b, col, e) | Pawn.GetPawnCaptures(sq, b, col, o),
            1 => Knight.GetKnightMoves(sq, b, col, free),
            2 => Bishop.GetBishopMoves(sq, b, col, free, oo),
            3 => Rook.GetRookMoves(sq, b, col, free, oo),

            // queen = bishop + rook
            4 => Bishop.GetBishopMoves(sq, b, col, free, oo) | Rook.GetRookMoves(sq, b, col, free, oo),
            5 => King.GetKingMoves(sq, b, col, free),
            _ => 0
        };
    }

    private static void LoopPiecesBB(Board b, ulong pieces, int p, int col, List<Move> moves) {
        ulong moves_bb;
        int start;

        // iteratively remove the pieces from the bitboard and generate their moves
        while (pieces != 0) {

            // "bit scan forward reset" also removes the least significant bit
            (pieces, start) = BB.LS1BReset(pieces);
            ulong sq = Consts.SqMask[start];

            // generate the moves
            moves_bb = GetMovesBB(sq, b, p, col);

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
            AddPieceMoves(p, col, start, end, capt, moves, b.enPassantSquare);
        }
    }

    private static void AddPieceMoves(int p, int col, int start, int end, int capt, List<Move> moves, int en_p_sq) {

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
