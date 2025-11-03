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
        if ((col == Color.WHITE && (board.CastRights & CastRights.WHITE) != 0UL 
             || col == Color.BLACK && (board.CastRights & CastRights.BLACK) != 0UL) 
            && !Check.IsKingChecked(in board, col)) 
        {
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
                PType.PAWN   => (onlyCaptures ? 0UL : Pawn.GetPawnPushTargets(start, col, empty))
                              | Pawn.GetPawnCaptureTargets(start, enPassantSq, col, opponentOccupied),
                PType.KNIGHT => Knight.GetKnightTargets(start, destMask),
                PType.BISHOP => Pext.GetBishopTargets(start, destMask, occupied),
                PType.ROOK   => Pext.GetRookTargets(start, destMask, occupied),
                PType.QUEEN  => Pext.GetBishopTargets(start, destMask, occupied)
                              | Pext.GetRookTargets(start, destMask, occupied),
                PType.KING   => King.GetKingTargets(start, destMask),
                _ => 0UL
            };

            while (targets != 0UL) {
                byte end   = BB.LS1BReset(ref targets);
                PType capt = PType.NONE;
                
                // detect captured piece using bitmask
                ulong targetMask = 1UL << end;
                if      ((board.Pieces[oppBase + 0] & targetMask) != 0UL) capt = PType.PAWN;
                else if ((board.Pieces[oppBase + 1] & targetMask) != 0UL) capt = PType.KNIGHT;
                else if ((board.Pieces[oppBase + 2] & targetMask) != 0UL) capt = PType.BISHOP;
                else if ((board.Pieces[oppBase + 3] & targetMask) != 0UL) capt = PType.ROOK;
                else if ((board.Pieces[oppBase + 4] & targetMask) != 0UL) capt = PType.QUEEN;
                
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
}