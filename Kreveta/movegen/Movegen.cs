//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.movegen.pieces;

using System.ComponentModel;
using System.Runtime.InteropServices;

// ReSharper disable InconsistentNaming

namespace Kreveta.movegen;

internal static class Movegen {

    internal static IEnumerable<Move> GetLegalMoves(Board board, bool onlyCaptures = false) {

        Color col = board.Color;

        List<Move> moves = [];

        // only generate captures (used in qsearch)
        if (onlyCaptures) {
            GetPseudoLegalCaptures(board, col, moves);
        }

        // otherwise all possible moves
        else GetPseudoLegalMoves(board, col, moves);

        // remove the illegal ones
        for (int i = 0; i < moves.Count; i++) {
            if (board.IsMoveLegal(moves[i], col))
                yield return moves[i];
        }
    }

    internal static void GetPseudoLegalMoves([In, ReadOnly(true)] in Board board, Color col, [In, ReadOnly(true)] in List<Move> moves) {

        // all occupied squares and squares occupied by opponent
        ulong occupied = board.Occupied;

        ulong occupiedOpp = col == Color.WHITE 
            ? board.BOccupied 
            : board.WOccupied;

        // all empty squares
        ulong empty = ~occupied;

        // squares, where moves can end - empty or occupied by opponent (captures)
        ulong free = empty | occupiedOpp;

        // loop through every piece type and add start the respective move search loop
        for (int i = 0; i < 6; i++) {
            LoopPiecesBB(board, board.Pieces[(byte)col][i], (PType)i, col, occupiedOpp, occupied, empty, free, moves);
        }

        // castling when in check is illegal
        if (!IsKingInCheck(board, col)) {
            ulong cast = King.GetCastlingTargets(board, col);
            LoopTargets(board, BB.LS1B(board.Pieces[(byte)col][(byte)PType.KING]), cast, PType.NONE, col, moves);
        }
    }

    private static void GetPseudoLegalCaptures([In, ReadOnly(true)] in Board board, Color col, [In, ReadOnly(true)] in List<Move> moves) {

        ulong occupied = board.Occupied;

        ulong occupiedOpp = col == Color.WHITE 
            ? board.BOccupied 
            : board.WOccupied;

        // loop through every piece (same as above)
        // we only generate captures, though
        for (int i = 0; i < 6; i++) {
            LoopPiecesBB(board, board.Pieces[(byte)col][i], (PType)i, col, occupiedOpp, occupied, occupiedOpp, occupiedOpp, moves, true);
        }

        // no need to generate castling moves - they can never be a capture
    }

    internal static bool IsKingInCheck([In, ReadOnly(true)] in Board board, Color col) {

        ulong kingSq = board.Pieces[(byte)col][(byte)PType.KING];

        Color colOpp = col == Color.WHITE 
            ? Color.BLACK 
            : Color.WHITE;

        ulong occupied = board.Occupied;

        ulong occupiedOpp = colOpp == Color.WHITE
            ? board.WOccupied
            : board.BOccupied;

        ulong targets = Pawn.GetPawnCaptureTargets(kingSq, 0, col, occupiedOpp);
        if ((targets & board.Pieces[(byte)colOpp][(byte)PType.PAWN]) != 0) 
            return true;

        targets = Knight.GetKnightTargets(kingSq, ulong.MaxValue);
        if ((targets & board.Pieces[(byte)colOpp][(byte)PType.KNIGHT]) != 0) 
            return true;

        targets = Bishop.GetBishopTargets(kingSq, ulong.MaxValue, occupied);
        if ((targets & board.Pieces[(byte)colOpp][(byte)PType.BISHOP]) != 0) 
            return true;

        ulong rookTargets = Rook.GetRookTargets(kingSq, ulong.MaxValue, occupied);
        if ((rookTargets & board.Pieces[(byte)colOpp][(byte)PType.ROOK]) != 0) 
            return true;

        if (((targets | rookTargets) & board.Pieces[(byte)colOpp][(byte)PType.QUEEN]) != 0) 
            return true;

        targets = King.GetKingTargets(kingSq, ulong.MaxValue);
        if ((targets & board.Pieces[(byte)colOpp][(byte)PType.KING]) != 0) 
            return true;

        return false;
    }

    private static void LoopPiecesBB([In, ReadOnly(true)] in Board board, ulong pieces, PType type, Color col, ulong occupiedOpp, ulong occupied, ulong empty, ulong free, [In, ReadOnly(true)] in List<Move> moves, bool onlyCaptures = false) {

        // iteratively remove the pieces from the bitboard and generate their moves
        while (pieces != 0) {

            // "bit scan forward reset" also removes the least significant bit
            int start = BB.LS1BReset(ref pieces);
            ulong sq = Consts.SqMask[start];

            // generate the moves
            ulong targets = GetTargets(board, sq, type, col, occupiedOpp, occupied, empty, free, onlyCaptures);

            // loop the found moves and add them
            LoopTargets(board, start, targets, type, col, moves);
        }
    }

    private static ulong GetTargets([In, ReadOnly(true)] in Board board, ulong sq, PType type, Color col, ulong occupiedOpp, ulong occupied, ulong empty, ulong free, bool onlyCaptures) {

        // return a bitboard of possible moves depending on the piece type
        return type switch {

            PType.PAWN => (onlyCaptures ? 0 : Pawn.GetPawnPushTargets(sq, col, empty)) 
                          | Pawn.GetPawnCaptureTargets(sq, board.EnPassantSq, col, occupiedOpp),

            PType.KNIGHT => Knight.GetKnightTargets(sq, free),
            PType.BISHOP => Bishop.GetBishopTargets(sq, free, occupied),
            PType.ROOK => Rook.GetRookTargets(sq, free, occupied),

            // queen = bishop + rook
            PType.QUEEN => Bishop.GetBishopTargets(sq, free, occupied)
                          | Rook.GetRookTargets(sq, free, occupied),

            PType.KING => King.GetKingTargets(sq, free),
            _ => 0
        };
    }

    private static void LoopTargets([In, ReadOnly(true)] in Board board, int start, ulong targets, PType type, Color col, [In, ReadOnly(true)] in List<Move> moves) {
        Color colOpp = col == Color.WHITE 
            ? Color.BLACK 
            : Color.WHITE;
        
        // same principle as above
        while (targets != 0) {
            int end = BB.LS1BReset(ref targets);

            PType capt = PType.NONE;

            // get the potential capture type
            if (type != PType.NONE) {
                for (int i = 0; i < 5; i++) {
                    if ((board.Pieces[(byte)colOpp][i] & Consts.SqMask[end]) != 0) {
                        capt = (PType)i;
                        break;
                    }
                }
            }
            
            // add the move
            AddMovesToList(type, col, start, end, capt, moves, board.EnPassantSq);
        }
    }

    private static void AddMovesToList(PType type, Color col, int start, int end, PType capt, [In, ReadOnly(true)] in List<Move> moves, int enPassantSq) {

        // add the generated move to the list
        switch (type) {

            // pawns have a special designated method to prevent nesting (promotions)
            case PType.PAWN: {
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
            }

            // special case for castling
            case PType.NONE: {
                moves.Add(new(start, end, PType.KING, PType.NONE, PType.KING)); 
                return;
            }

            // any other move
            default: {
                moves.Add(new(start, end, type, capt, PType.NONE)); 
                return;
            }
        }
    }
}
