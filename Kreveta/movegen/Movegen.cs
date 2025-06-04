//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.consts;
using Kreveta.movegen.pieces;

using System;

// ReSharper disable InconsistentNaming

namespace Kreveta.movegen;

internal static unsafe class Movegen {

    private const int MoveBufferSize = 110;

    // indices used to access the buffers; also act as move counters
    private static byte _curPL;

    // returns all legal moves that can be played from a position. the color
    // to play is determined by the Color field in board. for the qsearch,
    // there is also the option to only generate legal captures
    internal static int GetLegalMoves(ref Board board, Span<Move> buffer, bool onlyCaptures = false) {

        // reset the indices for buffers
        _curPL = 0;
        byte _curL  = 0;
        
        Span<Move> pseudoLegalBuffer = stackalloc Move[MoveBufferSize];

        // only generate pseudolegal captures (used in qsearch)
        if (onlyCaptures) {
            GeneratePseudoLegalCaptures(board, board.Color, pseudoLegalBuffer);
        }

        // otherwise all pseudolegal moves
        else GeneratePseudoLegalMoves(board, board.Color, pseudoLegalBuffer);

        // select the legal ones and add them to the legal move buffer
        for (int i = 0; i < _curPL; i++) {
            if (board.IsMoveLegal(pseudoLegalBuffer[i], board.Color)) {
                buffer[_curL++] = pseudoLegalBuffer[i];
            }
        }
        
        // return the number of legal moves in the buffer
        return _curL;
    }

    internal static int GetPseudoLegalMoves(ref Board board, Span<Move> moves) {
        _curPL = 0;
        GeneratePseudoLegalMoves(board, board.Color, moves);
        return _curPL;
    }

    private static void GeneratePseudoLegalMoves(in Board board, Color col, Span<Move> buffer) {
        
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
            LoopPiecesBB(board, board.Pieces[(byte)col * 6 + i], (PType)i, col, occupiedOpp, occupied, empty, free, buffer);
        }

        // castling when in check is illegal
        if (IsKingInCheck(board, col))
            return;

        ulong cast = King.GetCastlingTargets(board, col);
        LoopTargets(board, BB.LS1B(board.Pieces[(byte)col * 6 + 5]), cast, PType.NONE, col, buffer);
    }

    private static void GeneratePseudoLegalCaptures(in Board board, Color col, Span<Move> buffer) {

        ulong occupied = board.Occupied;

        ulong occupiedOpp = col == Color.WHITE
            ? board.BOccupied
            : board.WOccupied;

        // loop through every piece (same as above)
        // we only generate captures, though
        for (int i = 0; i < 6; i++) {
            LoopPiecesBB(board, board.Pieces[(byte)col * 6 + i], (PType)i, col, occupiedOpp, occupied, occupiedOpp, occupiedOpp, buffer, true);
        }

        // no need to generate castling moves - they can never be a capture
    }

    internal static bool IsKingInCheck(in Board board, Color col) {
        ulong kingSq = board.Pieces[(byte)col * 6 + 5];

        byte colOpp = (byte)(col == Color.WHITE ? 6 : 0);

        ulong occupied = board.Occupied;

        ulong occupiedOpp = col == Color.WHITE
            ? board.BOccupied
            : board.WOccupied;
        
        ulong targets = Pawn.GetPawnCaptureTargets(kingSq, 0, col, occupiedOpp);
        if ((targets & board.Pieces[colOpp]) != 0UL)
            return true;

        targets = Knight.GetKnightTargets(kingSq, ulong.MaxValue);
        if ((targets & board.Pieces[colOpp + 1]) != 0UL)
            return true;

        targets = Bishop.GetBishopTargets(kingSq, ulong.MaxValue, occupied);
        if ((targets & board.Pieces[colOpp + 2]) != 0UL)
            return true;

        ulong rookTargets = Rook.GetRookTargets(kingSq, ulong.MaxValue, occupied);
        
        if ((rookTargets & board.Pieces[colOpp + 3]) != 0UL)
            return true;

        if (((targets | rookTargets) & board.Pieces[colOpp + 4]) != 0UL)
            return true;

        targets = King.GetKingTargets(kingSq, ulong.MaxValue);
        if ((targets & board.Pieces[colOpp + 5]) != 0UL)
            return true;

        return false;
    }

    private static void LoopPiecesBB(
        in Board board,
        ulong pieces,
        PType type,
        Color col,
        ulong occupiedOpp,
        ulong occupied,
        ulong empty,
        ulong free,
        Span<Move> buffer,
        bool onlyCaptures = false) {

        // iteratively remove the pieces from the bitboard and generate their moves
        while (pieces != 0UL) {

            // "bit scan forward reset" also removes the least significant bit
            byte start = BB.LS1BReset(ref pieces);
            ulong sq = 1UL << start;

            // generate the moves
            ulong targets = GetTargets(board, sq, type, col, occupiedOpp, occupied, empty, free, onlyCaptures);

            // loop the found moves and add them
            LoopTargets(board, start, targets, type, col, buffer);
        }
    }

    private static ulong GetTargets(
        in Board board,
        ulong sq,
        PType type,
        Color col,
        ulong occupiedOpp,
        ulong occupied,
        ulong empty,
        ulong free,
        bool onlyCaptures) {

        // return a bitboard of possible moves depending on the piece type
        return type switch {

            PType.PAWN   => (onlyCaptures ? 0UL
                          : Pawn.GetPawnPushTargets(sq, col, empty))
                          | Pawn.GetPawnCaptureTargets(sq, board.EnPassantSq, col, occupiedOpp),

            PType.KNIGHT => Knight.GetKnightTargets(sq, free),
            PType.BISHOP => Bishop.GetBishopTargets(sq, free, occupied),
            PType.ROOK   => Rook.GetRookTargets(sq, free, occupied),

            // queen = bishop + rook
            PType.QUEEN => Bishop.GetBishopTargets(sq, free, occupied)
                          | Rook.GetRookTargets(sq, free, occupied),

            PType.KING  => King.GetKingTargets(sq, free),
            _ => 0UL
        };
    }

    private static void LoopTargets(in Board board, byte start, ulong targets, PType type, Color col, Span<Move> buffer) {
        var colOpp = col == Color.WHITE
            ? Color.BLACK
            : Color.WHITE;

        // same principle as above
        while (targets != 0UL) {
            byte end = BB.LS1BReset(ref targets);

            PType capt = PType.NONE;

            // get the potential capture type
            if (type != PType.NONE) {
                for (int i = 0; i < 5; i++) {
                    if ((board.Pieces[(byte)colOpp * 6 + i] & (1UL << end)) == 0UL)
                        continue;

                    capt = (PType)i;
                    break;
                }
            }

            // add the move
            AddMoveToBuffer(type, col, start, end, capt, board.EnPassantSq, buffer);
        }
    }

    private static void AddMoveToBuffer(PType type, Color col, byte start, byte end, PType capt, int enPassantSq, Span<Move> buffer) {

        // add the generated move to the list
        switch (type) {
            case PType.PAWN: {
                if ((end < 8 && col == Color.WHITE) | (end > 55 && col == Color.BLACK)) {

                    // all four possible promotions
                    buffer[_curPL++] = new(start, end, PType.PAWN, capt, PType.KNIGHT);
                    buffer[_curPL++] = new(start, end, PType.PAWN, capt, PType.BISHOP);
                    buffer[_curPL++] = new(start, end, PType.PAWN, capt, PType.ROOK);
                    buffer[_curPL++] = new(start, end, PType.PAWN, capt, PType.QUEEN);
                } 
                    
                // en passant - "pawn promotion"
                else if (end == enPassantSq) { 
                    buffer[_curPL++] = new(start, end, PType.PAWN, PType.NONE, PType.PAWN);
                } 
                
                // regular moves
                else buffer[_curPL++] = new(start, end, PType.PAWN, capt, PType.NONE);
                return;
            }

            // special case for castling
            case PType.NONE: {
                buffer[_curPL++] = new(start, end, PType.KING, PType.NONE, PType.KING);
                return;
            }

            // any other move
            default: {
                buffer[_curPL++] = new(start, end, type, capt, PType.NONE);
                return;
            }
        }
    }
}
