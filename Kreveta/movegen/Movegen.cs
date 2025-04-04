/*
 * |============================|
 * |                            |
 * |    Kreveta chess engine    |
 * | engineered by ZlomenyMesic |
 * | -------------------------- |
 * |      started 4-3-2025      |
 * | -------------------------- |
 * |                            |
 * | read README for additional |
 * | information about the code |
 * |    and usage that isn't    |
 * |  included in the comments  |
 * |                            |
 * |============================|
 */

using Kreveta.movegen.pieces;
using System.Runtime.CompilerServices;

namespace Kreveta.movegen;

internal static class Movegen {

    [MethodImpl(MethodImplOptions.Synchronized)]
    internal static List<Move> GetLegalMoves(Board b, bool only_captures = false) {

        Color col = b.color;

        List<Move> moves = [];

        // only generate captures (used in qsearch)
        if (only_captures) {
            GetPseudoLegalCaptures(b, col, moves);
        }

        // otherwise all possible moves
        else GetPseudoLegalMoves(b, col, moves);

        // remove the illegal ones
        List<Move> legal = [];
        for (int i = 0; i < moves.Count; i++) {

            if (b.IsMoveLegal(moves[i], col)) {
                legal.Add(moves[i]);
            }
        }

        return legal;
    }

    internal static void GetPseudoLegalMoves(Board b, Color col, List<Move> moves) {

        // squares occupied by opposite color
        ulong occ_opp = b.Occupied(col == Color.WHITE ? Color.BLACK : Color.WHITE);

        // all occupied squares (both colors)
        ulong occ = occ_opp | b.Occupied(col);

        // all empty squares
        ulong empty = ~occ;

        // squares, where moves can end - empty or occupied by opponent (captures)
        ulong free = empty | occ_opp;

        // loop through every piece type and add start the respective move search loop
        for (int i = 0; i < 6; i++) {
            LoopPiecesBB(b, b.pieces[(byte)col, i], (PType)i, col, moves, occ_opp, occ, empty, free);
        }

        // castling when in check is illegal
        if (!IsKingInCheck(b, col)) {
            ulong cast = King.GetCastlingMoves(b, col);
            LoopMovesBB(cast, b, BB.LS1B(b.pieces[(byte)col, (byte)PType.KING]), PType.NONE, col, moves);
        }
    }

    internal static void GetPseudoLegalCaptures(Board b, Color col, List<Move> moves) {

        ulong occ_opp = b.Occupied(col == Color.WHITE ? Color.BLACK : Color.WHITE);
        ulong occ = occ_opp | b.Occupied(col);

        // loop through every piece (same as above)
        // we only generate captures, though
        for (int i = 0; i < 6; i++) {
            LoopPiecesBB(b, b.pieces[(byte)col, i], (PType)i, col, moves, occ_opp, occ, occ_opp, occ_opp, true);
        }

        // no need to generate castling moves - they can never be a capture
    }

    internal static bool IsKingInCheck(Board b, Color col) {

        Color col_opp = col == Color.WHITE ? Color.BLACK : Color.WHITE;

        ulong king_sq = b.pieces[(byte)col, 5];

        ulong occ_opp = b.Occupied(col_opp);
        ulong occ = occ_opp | b.Occupied(col);

        ulong targets = Pawn.GetPawnCaptures(king_sq, 0, col, occ_opp);
        if ((targets & b.pieces[(byte)col_opp, (byte)PType.PAWN]) != 0) 
            return true;

        targets = Knight.GetKnightMoves(king_sq, ulong.MaxValue);
        if ((targets & b.pieces[(byte)col_opp, (byte)PType.KNIGHT]) != 0) 
            return true;

        targets = Bishop.GetBishopMoves(king_sq, ulong.MaxValue, occ);
        if ((targets & b.pieces[(byte)col_opp, (byte)PType.BISHOP]) != 0) 
            return true;

        ulong rook_targets = Rook.GetRookMoves(king_sq, ulong.MaxValue, occ);
        if ((rook_targets & b.pieces[(byte)col_opp, (byte)PType.ROOK]) != 0) 
            return true;

        if (((targets | rook_targets) & b.pieces[(byte)col_opp, (byte)PType.QUEEN]) != 0) 
            return true;

        targets = King.GetKingMoves(king_sq, ulong.MaxValue);
        if ((targets & b.pieces[(byte)col_opp, (byte)PType.KING]) != 0) 
            return true;

        return false;
    }

    private static void LoopPiecesBB(Board b, ulong pieces, PType p, Color col, List<Move> moves, ulong occ_opp, ulong occ, ulong empty, ulong free, bool only_captures = false) {
        ulong moves_bb;
        int start;

        // iteratively remove the pieces from the bitboard and generate their moves
        while (pieces != 0) {

            // "bit scan forward reset" also removes the least significant bit
            (pieces, start) = BB.LS1BReset(pieces);
            ulong sq = Consts.SqMask[start];

            // generate the moves
            moves_bb = GetMovesBB(sq, b, p, col, occ_opp, occ, empty, free, only_captures);

            // loop the found moves and add them
            LoopMovesBB(moves_bb, b, start, p, col, moves);
        }
    }

    private static ulong GetMovesBB(ulong sq, Board b, PType p, Color col, ulong occ_opp, ulong occ, ulong empty, ulong free, bool only_captures) {

        // return a bitboard of possible moves depending on the piece type
        return p switch {

            // don't generate pawn pushes when we only want captures
            PType.PAWN => (only_captures ? 0 : Pawn.GetPawnPushes(sq, col, empty)) 
                | Pawn.GetPawnCaptures(sq, b.enPassantSq, col, occ_opp),

            PType.KNIGHT => Knight.GetKnightMoves(sq, free),
            PType.BISHOP => Bishop.GetBishopMoves(sq, free, occ),
            PType.ROOK => Rook.GetRookMoves(sq, free, occ),

            // queen = bishop + rook
            PType.QUEEN => Bishop.GetBishopMoves(sq, free, occ) | Rook.GetRookMoves(sq, free, occ),
            PType.KING => King.GetKingMoves(sq, free),
            _ => 0
        };
    }

    private static void LoopMovesBB(ulong moves_bb, Board b, int start, PType p, Color col, List<Move> moves) {
        int end;

        // same principle as above
        while (moves_bb != 0) {
            (moves_bb, end) = BB.LS1BReset(moves_bb);

            // get the potential capture piece type
            PType capt = p != PType.NONE
                ? b.PieceAt(end).type : PType.NONE;

            // add the move
            AddMovesToList(p, col, start, end, capt, moves, b.enPassantSq);
        }
    }

    private static void AddMovesToList(PType p, Color col, int start, int end, PType capt, List<Move> moves, int en_p_sq) {

        // add the generated move to the list
        switch (p) {

            // pawns have a special designated method to prevent nesting (promotions)
            case PType.PAWN: Pawn.AddPawnMoves(col, start, end, capt, moves, en_p_sq); return;

            // special case for castling
            case PType.NONE: moves.Add(new(start, end, PType.KING, PType.NONE, PType.KING)); return;

            // any other move
            default: moves.Add(new(start, end, p, capt, PType.NONE)); break;
        }
    }
}
