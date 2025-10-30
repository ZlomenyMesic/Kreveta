//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.consts;
using Kreveta.movegen.pieces;

using System;
using System.Runtime.CompilerServices;

namespace Kreveta.movegen;

internal static unsafe class Movegen {
    
    // current index in pseudo-legal move buffer
    private static int _curPsL;
    
    internal static int GetLegalMoves(ref Board board, Span<Move> legalBuffer, bool onlyCaptures = false) {
        Span<Move> pseudoBuffer = stackalloc Move[Consts.MoveBufferSize];
        _curPsL = 0;

        if (onlyCaptures)
            GeneratePseudoLegalCaptures(in board, board.Color, pseudoBuffer);
        else GeneratePseudoLegalMoves(in board, board.Color, pseudoBuffer);

        int legalCount = 0;
        for (int i = 0; i < _curPsL; i++) {
            if (board.IsMoveLegal(pseudoBuffer[i], board.Color))
                legalBuffer[legalCount++] = pseudoBuffer[i];
        }

        return legalCount;
    }

    internal static int GetPseudoLegalMoves(ref Board board, Span<Move> pseudoBuffer) {
        _curPsL = 0;
        GeneratePseudoLegalMoves(in board, board.Color, pseudoBuffer);
        return _curPsL;
    }

    internal static bool IsKingInCheck(in Board board, Color col) {
        byte  kingSq      = BB.LS1B(board.Pieces[(byte)col * 6 + 5]);
        int   oppBase     = col == Color.WHITE ? 6 : 0;
        
        ulong occupied    = board.Occupied;
        ulong oppOccupied = col == Color.WHITE
            ? board.BOccupied : board.WOccupied;

        // bishop check
        ulong bishopRays = Pext.GetBishopTargets(kingSq, ulong.MaxValue, occupied);
        if ((bishopRays & board.Pieces[oppBase + 2]) != 0UL) return true;

        // rook check
        ulong rookRays = Pext.GetRookTargets(kingSq, ulong.MaxValue, occupied);
        if ((rookRays & board.Pieces[oppBase + 3]) != 0UL) 
            return true;

        // queen check - union of bishop and rook
        if (((bishopRays | rookRays) & board.Pieces[oppBase + 4]) != 0UL) 
            return true;

        // knight check
        if ((Knight.GetKnightTargets(kingSq, ulong.MaxValue) & board.Pieces[oppBase + 1]) != 0UL)
            return true;

        // pawn check
        if ((Pawn.GetPawnCaptureTargets(kingSq, 0, col, oppOccupied) & board.Pieces[oppBase + 0]) != 0UL)
            return true;

        // opposing king
        return (King.GetKingTargets(kingSq, ulong.MaxValue) & board.Pieces[oppBase + 5]) != 0UL;
    }

    private static void GeneratePseudoLegalMoves(in Board board, Color col, Span<Move> moveBuffer) {
        int   baseIndex        = (byte)col * 6;
        ulong occupied         = board.Occupied;
        ulong opponentOccupied = col == Color.WHITE ? board.BOccupied : board.WOccupied;
        ulong empty            = ~occupied;
        ulong moveDestMask     = empty | opponentOccupied;

        // promotion ranks as masks for quick check
        ulong promotionRank = col == Color.WHITE
            ? 0xFFUL : 0xFF00000000000000UL;

        // generate per piece type
        for (int pt = 0; pt < 6; pt++) {
            ulong pieces = board.Pieces[baseIndex + pt];
            if (pieces != 0UL)
                GeneratePieceMoves(in board, pieces, (PType)pt, col, opponentOccupied, occupied, empty, moveDestMask, moveBuffer, promotionRank, onlyCaptures: false);
        }

        // castling
        if (board.CastRights != CastRights.NONE && !IsKingInCheck(in board, col)) {
            ulong castTargets = King.GetCastlingTargets(in board, col);
            if (castTargets == 0UL)
                return;
            
            byte kingSq = BB.LS1B(board.Pieces[baseIndex + 5]);
            GenerateCastlingMoves(kingSq, castTargets, moveBuffer);
        }
    }

    private static void GeneratePseudoLegalCaptures(in Board board, Color col, Span<Move> moveBuffer) {
        int baseIndex = (byte)col * 6;
        
        ulong occupied    = board.Occupied;
        ulong oppOccupied = col == Color.WHITE 
            ? board.BOccupied : board.WOccupied;
        
        ulong promotionRank = col == Color.WHITE 
            ? 0xFFUL : 0xFF00000000000000UL;

        for (int pt = 0; pt < 6; pt++) {
            ulong pieces = board.Pieces[baseIndex + pt];
            if (pieces != 0UL)
                GeneratePieceMoves(in board, pieces, (PType)pt, col, oppOccupied, occupied, oppOccupied, oppOccupied, moveBuffer, promotionRank, onlyCaptures: true);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void GeneratePieceMoves(
        in Board board,
        ulong pieces,
        PType pieceType,
        Color col,
        ulong opponentOccupied,
        ulong occupied,
        ulong empty,
        ulong destMask,
        Span<Move> moveBuffer,
        ulong promotionRankMask,
        bool onlyCaptures)
    {
        int oppBase     = col == Color.WHITE ? 6 : 0;
        int enPassantSq = board.EnPassantSq;

        while (pieces != 0UL) {
            byte start = BB.LS1BReset(ref pieces);

            ulong targets = pieceType switch {
                PType.PAWN => (onlyCaptures ? 0UL : Pawn.GetPawnPushTargets(start, col, empty))
                              | Pawn.GetPawnCaptureTargets(start, enPassantSq, col, opponentOccupied),
                PType.KNIGHT => Knight.GetKnightTargets(start, destMask),
                PType.BISHOP => Pext.GetBishopTargets(start, destMask, occupied),
                PType.ROOK => Pext.GetRookTargets(start, destMask, occupied),
                PType.QUEEN => Pext.GetBishopTargets(start, destMask, occupied)
                              | Pext.GetRookTargets(start, destMask, occupied),
                PType.KING => King.GetKingTargets(start, destMask),
                _ => 0UL
            };

            while (targets != 0UL) {
                byte end   = BB.LS1BReset(ref targets);
                PType capt = PType.NONE;
                
                // detect captured piece using bitmask
                ulong targetMask = 1UL << end;
                if (pieceType != PType.NONE) {
                    if ((board.Pieces[oppBase + 0] & targetMask) != 0UL)      capt = PType.PAWN;
                    else if ((board.Pieces[oppBase + 1] & targetMask) != 0UL) capt = PType.KNIGHT;
                    else if ((board.Pieces[oppBase + 2] & targetMask) != 0UL) capt = PType.BISHOP;
                    else if ((board.Pieces[oppBase + 3] & targetMask) != 0UL) capt = PType.ROOK;
                    else if ((board.Pieces[oppBase + 4] & targetMask) != 0UL) capt = PType.QUEEN;
                }
                
                if (pieceType == PType.PAWN) {
                    bool promote = (1UL << end & promotionRankMask) != 0UL;
                    
                    // handle promotions
                    if (promote) {
                        moveBuffer[_curPsL++] = new Move(start, end, PType.PAWN, capt, PType.KNIGHT);
                        moveBuffer[_curPsL++] = new Move(start, end, PType.PAWN, capt, PType.BISHOP);
                        moveBuffer[_curPsL++] = new Move(start, end, PType.PAWN, capt, PType.ROOK);
                        moveBuffer[_curPsL++] = new Move(start, end, PType.PAWN, capt, PType.QUEEN);
                        continue;
                    }

                    // en passant
                    if (end == enPassantSq) {
                        moveBuffer[_curPsL++] = new Move(start, end, PType.PAWN, PType.NONE, PType.PAWN);
                        continue;
                    }
                }

                // handle castling
                /*if (pieceType == PType.NONE)
                    moveBuffer[_curPsL++] = new Move(start, end, PType.KING, PType.NONE, PType.KING);
                else
                    moveBuffer[_curPsL++] = new Move(start, end, pieceType, capt, PType.NONE);*/
                moveBuffer[_curPsL++] = new Move(start, end, pieceType, capt, PType.NONE);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void GenerateCastlingMoves(byte kingSquare, ulong castTargets, Span<Move> moveBuffer) {
        while (castTargets != 0UL) {
            byte end = BB.LS1BReset(ref castTargets);
            moveBuffer[_curPsL++] = new Move(kingSquare, end, PType.KING, PType.NONE, PType.KING);
        }
    }
}/*

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
    
    // indices used to access the buffers; also act as move counters
    private static byte _curPL;

    // returns all legal moves that can be played from a position. the color
    // to play is determined by the Color field in board. for the qsearch,
    // there is also the option to only generate legal captures
    internal static int GetLegalMoves(ref Board board, Span<Move> buffer, bool onlyCaptures = false) {

        // reset the indices for buffers
        _curPL = 0;
        byte _curL  = 0;
        
        Span<Move> pseudoLegalBuffer = stackalloc Move[Consts.MoveBufferSize];

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
        // all occupied squares
        ulong occupied = board.Occupied;

        // squares occupied by opponent
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

        // only generate castling moves when it's actually possible
        if (board.CastRights != CastRights.NONE) {
            // castling when in check is illegal
            if (IsKingInCheck(in board, col))
                return;
            
            ulong cast = King.GetCastlingTargets(in board, col);
            LoopTargets(board, BB.LS1B(board.Pieces[(byte)col * 6 + 5]), cast, PType.NONE, col, buffer);
        }
    }

    private static void GeneratePseudoLegalCaptures(in Board board, Color col, Span<Move> buffer) {
        ulong occupied = board.Occupied;

        ulong occupiedOpp = col == Color.WHITE
            ? board.BOccupied
            : board.WOccupied;

        // loop through every piece (same as above)
        // we only generate captures, though
        for (int i = 0; i < 6; i++) {
            
            // instead of empty and free, we pass the opponent squares
            // directly, so we don't waste time on generating quiets
            LoopPiecesBB(board, board.Pieces[(byte)col * 6 + i], (PType)i, col, occupiedOpp, occupied, occupiedOpp, occupiedOpp, buffer, true);
        }

        // no need to generate castling moves - they can never be a capture
    }

    // check whether the king of the specified color is currently in check. works
    // by pretending the king is a different piece and generating captures for it,
    // and then testing whether it could capture any of the same opponent pieces
    internal static bool IsKingInCheck(in Board board, Color col) {
        byte  kingSq   = BB.LS1B(board.Pieces[(byte)col * 6 + 5]);
        byte  colOpp   = (byte)(col == Color.WHITE ? 6 : 0);
        ulong occupied = board.Occupied;

        ulong occupiedOpp = col == Color.WHITE
            ? board.BOccupied
            : board.WOccupied;
        
        // i tried to order these based on how much i think certain pieces are
        // likely to be checking the king, but i am still unsure whether it
        // actually has any performance benefits

        ulong targets = Pext.GetBishopTargets(kingSq, ulong.MaxValue, occupied);
        if ((targets & board.Pieces[colOpp + 2]) != 0UL)
            return true;

        ulong rookTargets = Pext.GetRookTargets(kingSq, ulong.MaxValue, occupied);
        if ((rookTargets & board.Pieces[colOpp + 3]) != 0UL)
            return true;

        if (((targets | rookTargets) & board.Pieces[colOpp + 4]) != 0UL)
            return true;
        
        targets = Knight.GetKnightTargets(kingSq, ulong.MaxValue);
        if ((targets & board.Pieces[colOpp + 1]) != 0UL)
            return true;
        
        targets = Pawn.GetPawnCaptureTargets(kingSq, 0, col, occupiedOpp);
        if ((targets & board.Pieces[colOpp]) != 0UL)
            return true;

        targets = King.GetKingTargets(kingSq, ulong.MaxValue);
        return (targets & board.Pieces[colOpp + 5]) != 0UL;
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
            byte sq = BB.LS1BReset(ref pieces);

            // generate the moves
            ulong targets = GetTargets(sq, type, col, occupiedOpp, occupied, empty, free, board.EnPassantSq, onlyCaptures);

            // loop the found moves and add them
            LoopTargets(in board, sq, targets, type, col, buffer);
        }
    }

    private static ulong GetTargets(
        byte sq,
        PType type,
        Color col,
        ulong occupiedOpp,
        ulong occupied,
        ulong empty,
        ulong free,
        int   enPassantSq,
        bool  onlyCaptures) {

        // return a bitboard of possible moves depending on the piece type
        return type switch {

            PType.PAWN   => (onlyCaptures ? 0UL
                          : Pawn.GetPawnPushTargets(sq, col, empty))
                          | Pawn.GetPawnCaptureTargets(sq, enPassantSq, col, occupiedOpp),

            PType.KNIGHT => Knight.GetKnightTargets(sq, free),
            PType.BISHOP => Pext.GetBishopTargets(sq, free, occupied),
            PType.ROOK   => Pext.GetRookTargets(sq, free, occupied),

            // queen = bishop + rook
            PType.QUEEN => Pext.GetBishopTargets(sq, free, occupied)
                          | Pext.GetRookTargets(sq, free, occupied),

            PType.KING  => King.GetKingTargets(sq, free),
            _ => 0UL
        };
    }

    private static void LoopTargets(in Board board, byte start, ulong targets, PType type, Color col, Span<Move> buffer) {
        Color colOpp = col == Color.WHITE
            ? Color.BLACK
            : Color.WHITE;

        // same principle as above
        while (targets != 0UL) {
            byte end = BB.LS1BReset(ref targets);

            PType capt = PType.NONE;

            // get the potential capture type
            if (type != PType.NONE) {
                for (int i = 0; i < 5; i++) {
                    if ((board.Pieces[(byte)colOpp * 6 + i] & 1UL << end) == 0UL)
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
        // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
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
}*/
