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
using System.Drawing;
using System.Runtime.CompilerServices;

namespace Kreveta.movegen;

internal static class Movegen {

    [MethodImpl(MethodImplOptions.Synchronized)]
    internal static List<Move> GetLegalMoves(Board board, bool onlyCaptures = false) {

        Color col = board.color;

        List<Move> moves = [];

        // only generate captures (used in qsearch)
        if (onlyCaptures) {
            GetPseudoLegalCaptures(board, col, moves);
        }

        // otherwise all possible moves
        else GetPseudoLegalMoves(board, col, moves);

        // remove the illegal ones
        List<Move> legal = [];
        for (int i = 0; i < moves.Count; i++) {

            if (board.IsMoveLegal(moves[i], col)) {
                legal.Add(moves[i]);
            }
        }

        return legal;
    }

    internal static void GetPseudoLegalMoves(Board board, Color col, List<Move> moves) {

        // squares occupied by opposite color
        ulong occupiedOpp = board.Occupied(col == Color.WHITE 
            ? Color.BLACK 
            : Color.WHITE);

        // all occupied squares (both colors)
        ulong occupied = occupiedOpp | board.Occupied(col);

        // all empty squares
        ulong empty = ~occupied;

        // squares, where moves can end - empty or occupied by opponent (captures)
        ulong free = empty | occupiedOpp;

        // loop through every piece type and add start the respective move search loop
        for (int i = 0; i < 6; i++) {
            LoopPiecesBB(board, board.pieces[(byte)col, i], (PType)i, col, occupiedOpp, occupied, empty, free, moves);
        }

        // castling when in check is illegal
        if (!IsKingInCheck(board, col)) {
            ulong cast = King.GetCastlingTargets(board, col);
            LoopTargets(board, BB.LS1B(board.pieces[(byte)col, (byte)PType.KING]), cast, PType.NONE, col, moves);
        }
    }

    internal static void GetPseudoLegalCaptures(Board board, Color col, List<Move> moves) {

        ulong occupiedOpp = board.Occupied(col == Color.WHITE 
            ? Color.BLACK 
            : Color.WHITE);

        ulong occupied = occupiedOpp | board.Occupied(col);

        // loop through every piece (same as above)
        // we only generate captures, though
        for (int i = 0; i < 6; i++) {
            LoopPiecesBB(board, board.pieces[(byte)col, i], (PType)i, col, occupiedOpp, occupied, occupiedOpp, occupiedOpp, moves, true);
        }

        // no need to generate castling moves - they can never be a capture
    }

    internal static bool IsKingInCheck(Board board, Color col) {

        Color colOpp = col == Color.WHITE 
            ? Color.BLACK 
            : Color.WHITE;

        ulong kingSq = board.pieces[(byte)col, 5];

        ulong occupiedOpp = board.Occupied(colOpp);
        ulong occupied    = occupiedOpp | board.Occupied(col);

        ulong targets = Pawn.GetPawnCaptureTargets(kingSq, 0, col, occupiedOpp);
        if ((targets & board.pieces[(byte)colOpp, (byte)PType.PAWN]) != 0) 
            return true;

        targets = Knight.GetKnightTargets(kingSq, ulong.MaxValue);
        if ((targets & board.pieces[(byte)colOpp, (byte)PType.KNIGHT]) != 0) 
            return true;

        targets = Bishop.GetBishopTargets(kingSq, ulong.MaxValue, occupied);
        if ((targets & board.pieces[(byte)colOpp, (byte)PType.BISHOP]) != 0) 
            return true;

        ulong rookTargets = Rook.GetRookTargets(kingSq, ulong.MaxValue, occupied);
        if ((rookTargets & board.pieces[(byte)colOpp, (byte)PType.ROOK]) != 0) 
            return true;

        if (((targets | rookTargets) & board.pieces[(byte)colOpp, (byte)PType.QUEEN]) != 0) 
            return true;

        targets = King.GetKingTargets(kingSq, ulong.MaxValue);
        if ((targets & board.pieces[(byte)colOpp, (byte)PType.KING]) != 0) 
            return true;

        return false;
    }

    private static void LoopPiecesBB(Board board, ulong pieces, PType type, Color col, ulong occupiedOpp, ulong occupied, ulong empty, ulong free, List<Move> moves, bool onlyCaptures = false) {
        ulong targets;
        int start;

        // iteratively remove the pieces from the bitboard and generate their moves
        while (pieces != 0) {

            // "bit scan forward reset" also removes the least significant bit
            (pieces, start) = BB.LS1BReset(pieces);
            ulong sq = Consts.SqMask[start];

            // generate the moves
            targets = GetTargets(board, sq, type, col, occupiedOpp, occupied, empty, free, onlyCaptures);

            // loop the found moves and add them
            LoopTargets(board, start, targets, type, col, moves);
        }
    }

    private static ulong GetTargets(Board board, ulong sq, PType type, Color col, ulong occupiedOpp, ulong occupied, ulong empty, ulong free, bool onlyCaptures) {

        // return a bitboard of possible moves depending on the piece type
        return type switch {

            PType.PAWN => (onlyCaptures ? 0 : Pawn.GetPawnPushTargets(sq, col, empty)) 
                          | Pawn.GetPawnCaptureTargets(sq, board.enPassantSq, col, occupiedOpp),

            PType.KNIGHT => Knight.GetKnightTargets(sq, free),
            PType.BISHOP => Bishop.GetBishopTargets(sq, free, occupied),
            PType.ROOK => Rook.GetRookTargets(sq, free, occupied),

            // queen = bishop + rook
            PType.QUEEN => Bishop.GetBishopTargets(sq, free, occupied)
                          | Rook.GetRookTargets(sq, free, occupied),

            PType.KING => King.GetKingTargets(sq, free),
            _ => 0
        }; ;
    }

    private static void LoopTargets(Board board, int start, ulong targets, PType type, Color col, List<Move> moves) {
        Color colOpp = col == Color.WHITE 
            ? Color.BLACK 
            : Color.WHITE;

        int end;

        // same principle as above
        while (targets != 0) {
            (targets, end) = BB.LS1BReset(targets);

            PType capt = PType.NONE;

            if (type != PType.NONE) {
                for (int i = 0; i < 5; i++) {
                    if ((board.pieces[(byte)colOpp, i] & Consts.SqMask[end]) != 0) {
                        capt = (PType)i;
                        break;
                    }
                }
            }

            // get the potential capture piece type
            //PType capt = type != PType.NONE
            //    ? board.PieceAt(end).type : PType.NONE;

            // add the move
            AddMovesToList(type, col, start, end, capt, moves, board.enPassantSq);
        }
    }

    private static void AddMovesToList(PType type, Color col, int start, int end, PType capt, List<Move> moves, int enPassantSq) {

        // add the generated move to the list
        switch (type) {

            // pawns have a special designated method to prevent nesting (promotions)
            case PType.PAWN:
                if ((end < 8 && col == Color.WHITE) | (end > 55 && col == Color.BLACK)) {

                    // all four possible promotions
                    moves.Add(new(start, end, PType.PAWN, capt, PType.KNIGHT));
                    moves.Add(new(start, end, PType.PAWN, capt, PType.BISHOP));
                    moves.Add(new(start, end, PType.PAWN, capt, PType.ROOK));
                    moves.Add(new(start, end, PType.PAWN, capt, PType.QUEEN));
                } 
                else if (end == enPassantSq) {

                    // en passant - "pawn promotion"
                    moves.Add(new(start, end, PType.PAWN, PType.NONE, PType.PAWN));
                } 
                else {

                    // regular moves
                    moves.Add(new(start, end, PType.PAWN, capt, PType.NONE));
                }
                return;

            // special case for castling
            case PType.NONE: 
                moves.Add(new(start, end, PType.KING, PType.NONE, PType.KING)); 
                return;

            // any other move
            default: 
                moves.Add(new(start, end, type, capt, PType.NONE)); 
                return;
        }
    }
}
