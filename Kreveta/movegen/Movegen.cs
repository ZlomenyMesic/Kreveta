//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.consts;
using Kreveta.movegen.pieces;

using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

// ReSharper disable InconsistentNaming

namespace Kreveta.movegen;

internal static unsafe class Movegen {

    private const int MaxMoveCount = 110;

    private static readonly Move* _pseudoLegalMoveBuffer = (Move*)NativeMemory.AlignedAlloc((nuint)(MaxMoveCount * sizeof(Move)), 32);
    private static readonly Move* _legalMoveBuffer       = (Move*)NativeMemory.AlignedAlloc((nuint)(MaxMoveCount * sizeof(Move)), 32);

    private static byte _curPL;
    private static byte _curL;

    internal static Span<Move> GetLegalMoves(Board board, bool onlyCaptures = false) {

        Color col = board.Color;

        _curPL = 0;
        _curL  = 0;

        // only generate captures (used in qsearch)
        if (onlyCaptures) {
            GeneratePseudoLegalCaptures(board, col);
        }

        // otherwise all possible moves
        else GeneratePseudoLegalMoves(board, col);

        // remove the illegal ones
        for (int i = 0; i < _curPL; i++) {
            if (board.IsMoveLegal(_pseudoLegalMoveBuffer[i], col)) {
                _legalMoveBuffer[_curL++] = _pseudoLegalMoveBuffer[i];
            }
        }

        Move[] result = new Move[_curL];
        for (int i = 0; i < _curL; i++) {
            result[i] = _legalMoveBuffer[i];
        }

        return result;
    }

    internal static Span<Move> GetPseudoLegalMoves(Board board) {
        Color col = board.Color;

        _curPL = 0;

        GeneratePseudoLegalMoves(board, col);

        Move[] result = new Move[_curPL];
        for (int i = 0; i < _curPL; i++) {
            result[i] = _pseudoLegalMoveBuffer[i];
        }

        return result;
    }

    private static void GeneratePseudoLegalMoves([In, ReadOnly(true)] in Board board, Color col) {

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
            LoopPiecesBB(board, board.Pieces[(byte)col][i], (PType)i, col, occupiedOpp, occupied, empty, free);
        }

        // castling when in check is illegal
        if (IsKingInCheck(board, col)) 
            return;
        
        ulong cast = King.GetCastlingTargets(board, col);
        LoopTargets(board, BB.LS1B(board.Pieces[(byte)col][(byte)PType.KING]), cast, PType.NONE, col);
    }

    private static void GeneratePseudoLegalCaptures([In, ReadOnly(true)] in Board board, Color col) {

        ulong occupied = board.Occupied;

        ulong occupiedOpp = col == Color.WHITE 
            ? board.BOccupied 
            : board.WOccupied;

        // loop through every piece (same as above)
        // we only generate captures, though
        for (int i = 0; i < 6; i++) {
            LoopPiecesBB(board, board.Pieces[(byte)col][i], (PType)i, col, occupiedOpp, occupied, occupiedOpp, occupiedOpp, true);
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
        if ((targets & board.Pieces[(byte)colOpp][(byte)PType.PAWN]) != 0UL) 
            return true;

        targets = Knight.GetKnightTargets(kingSq, ulong.MaxValue);
        if ((targets & board.Pieces[(byte)colOpp][(byte)PType.KNIGHT]) != 0UL) 
            return true;

        targets = Bishop.GetBishopTargets(kingSq, ulong.MaxValue, occupied);
        if ((targets & board.Pieces[(byte)colOpp][(byte)PType.BISHOP]) != 0UL) 
            return true;

        ulong rookTargets = Rook.GetRookTargets(kingSq, ulong.MaxValue, occupied);
        if ((rookTargets & board.Pieces[(byte)colOpp][(byte)PType.ROOK]) != 0UL) 
            return true;

        if (((targets | rookTargets) & board.Pieces[(byte)colOpp][(byte)PType.QUEEN]) != 0UL) 
            return true;

        targets = King.GetKingTargets(kingSq, ulong.MaxValue);
        if ((targets & board.Pieces[(byte)colOpp][(byte)PType.KING]) != 0UL) 
            return true;

        return false;
    }

    private static void LoopPiecesBB([In, ReadOnly(true)] in Board board, ulong pieces, PType type, Color col, ulong occupiedOpp, ulong occupied, ulong empty, ulong free, bool onlyCaptures = false) {

        // iteratively remove the pieces from the bitboard and generate their moves
        while (pieces != 0UL) {

            // "bit scan forward reset" also removes the least significant bit
            sbyte start = BB.LS1BReset(ref pieces);
            ulong sq = 1UL << start;

            // generate the moves
            ulong targets = GetTargets(board, sq, type, col, occupiedOpp, occupied, empty, free, onlyCaptures);

            // loop the found moves and add them
            LoopTargets(board, start, targets, type, col);
        }
    }

    private static ulong GetTargets([In, ReadOnly(true)] in Board board, ulong sq, PType type, Color col, ulong occupiedOpp, ulong occupied, ulong empty, ulong free, bool onlyCaptures) {

        // return a bitboard of possible moves depending on the piece type
        return type switch {

            PType.PAWN => (onlyCaptures ? 0UL 
                          : Pawn.GetPawnPushTargets(sq, col, empty)) 
                          | Pawn.GetPawnCaptureTargets(sq, board.EnPassantSq, col, occupiedOpp),

            PType.KNIGHT => Knight.GetKnightTargets(sq, free),
            PType.BISHOP => Bishop.GetBishopTargets(sq, free, occupied),
            PType.ROOK =>   Rook.GetRookTargets(sq, free, occupied),

            // queen = bishop + rook
            PType.QUEEN =>  Bishop.GetBishopTargets(sq, free, occupied)
                          | Rook.GetRookTargets(sq, free, occupied),

            PType.KING =>   King.GetKingTargets(sq, free),
            _ => 0UL
        };
    }

    private static void LoopTargets([In, ReadOnly(true)] in Board board, sbyte start, ulong targets, PType type, Color col) {
        Color colOpp = col == Color.WHITE 
            ? Color.BLACK 
            : Color.WHITE;
        
        // same principle as above
        while (targets != 0UL) {
            sbyte end = BB.LS1BReset(ref targets);

            PType capt = PType.NONE;

            // get the potential capture type
            if (type != PType.NONE) {
                for (int i = 0; i < 5; i++) {
                    if ((board.Pieces[(byte)colOpp][i] & (1UL << end)) == 0UL)
                        continue;
                    
                    capt = (PType)i;
                    break;
                }
            }
            
            // add the move
            AddMovesToList(type, col, start, end, capt, board.EnPassantSq);
        }
    }

    private static void AddMovesToList(PType type, Color col, sbyte start, sbyte end, PType capt, int enPassantSq) {

        // add the generated move to the list
        switch (type) {

            // pawns have a special designated method to prevent nesting (promotions)
            case PType.PAWN: {
                if ((end < 8 && col == Color.WHITE) | (end > 55 && col == Color.BLACK)) {

                    // all four possible promotions
                    _pseudoLegalMoveBuffer[_curPL++] = new(start, end, PType.PAWN, capt, PType.KNIGHT);
                    _pseudoLegalMoveBuffer[_curPL++] = new(start, end, PType.PAWN, capt, PType.BISHOP);
                    _pseudoLegalMoveBuffer[_curPL++] = new(start, end, PType.PAWN, capt, PType.ROOK);
                    _pseudoLegalMoveBuffer[_curPL++] = new(start, end, PType.PAWN, capt, PType.QUEEN);
                }
                else if (end == enPassantSq) {

                    // en passant - "pawn promotion"
                    _pseudoLegalMoveBuffer[_curPL++] = new(start, end, PType.PAWN, PType.NONE, PType.PAWN);
                }
                else {

                    // regular moves
                    _pseudoLegalMoveBuffer[_curPL++] = new(start, end, PType.PAWN, capt, PType.NONE);
                }

                return;
            }

            // special case for castling
            case PType.NONE: {
                _pseudoLegalMoveBuffer[_curPL++] = new(start, end, PType.KING, PType.NONE, PType.KING); 
                return;
            }

            // any other move
            default: {
                _pseudoLegalMoveBuffer[_curPL++] = new(start, end, type, capt, PType.NONE); 
                return;
            }
        }
    }
}
