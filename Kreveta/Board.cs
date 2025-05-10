//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

// Remove unnecessary suppression
#pragma warning disable IDE0079

// Specify CultureInfo
#pragma warning disable CA1304

using Kreveta.consts;
using Kreveta.movegen;

using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Diagnostics.Contracts;

namespace Kreveta;

// can be neither readonly nor record
internal struct Board {

    // all pieces are saved here in so called bitboards.
    // we have 12 different ulongs (bitboards) which represent
    // all possible pieces. these bitboards are empty, and
    // the pieces are stored as one-bits in these large bbs.
    // since a chessboard has 64 squares and ulong has 64
    // bits, we don't waste any memory or anything else.
    [Required, DebuggerDisplay("indexed [color, piece_type]")]
    internal readonly ulong[][] Pieces = new ulong[2][];

    // these two bitboards simply represent all occupied
    // squares by a certain color. it turns out to be a little
    // faster than OR the bitboards above, although it takes
    // 16 additional bytes of memory.
    internal ulong WOccupied;
    internal ulong BOccupied;

    // all occupied squares
    [ReadOnly(true), DefaultValue(0UL)]
    internal readonly ulong Occupied 
        => WOccupied | BOccupied;

    // all empty squares (bitwise inverse of occupied)
    [ReadOnly(true), DefaultValue(0xFFFFFFFFFFFFFFFFUL)]
    private readonly ulong Empty => ~Occupied;

    // square over which a double pushing
    // pawn has passed one move ago
    internal byte EnPassantSq = 64;

    // the current state of castling rights
    [EnumDataType(typeof(CastlingRights))]
    internal CastlingRights CastlingRights = 0;

    // the side to move
    [EnumDataType(typeof(Color))]
    internal Color Color = 0;

    public Board() {
        Pieces[(byte)Color.WHITE] = new ulong[6];
        Pieces[(byte)Color.BLACK] = new ulong[6];
    }

    internal void Clear() {
        Array.Clear(Pieces);

        WOccupied      = 0UL;
        BOccupied      = 0UL;

        EnPassantSq    = 64;
        CastlingRights = CastlingRights.NONE;
        Color          = Color.NONE;
    }

    // returns the piece at a certain square. this method isn't
    // really the fastest, but it's useful in the case where we
    // generate piece types for input moves
    [Pure]
    internal PType PieceAt(int index) {
        ulong sq = 1UL << index;

        // empty square, return immediately
        if ((Empty & sq) != 0UL)
            return PType.NONE;

        // now we loop the piece types and check whether the
        // square is occupied by any side
        for (int i = 0; i < 6; i++) {
            if (((Pieces[(byte)Color.WHITE][i] | Pieces[(byte)Color.BLACK][i]) & sq) != 0UL)
                return (PType)i;
        }

        // we shouldn't ever get here
        return PType.NONE;
    }

    #region MOVEPLAY    

    // performs a move on the board
    internal void PlayMove(Move move) {

        EnPassantSq = 64;
        Color = Color == Color.WHITE
            ? Color.BLACK
            : Color.WHITE;

        // start and end squares
        byte start8 = (byte)move.Start;
        byte end8   = (byte)move.End;

        // start and end squares represented as bitboards
        ulong start = 1UL << start8;
        ulong end   = 1UL << end8;

        // color and opposite color
        Color col = (WOccupied & start) == 0UL
            ? Color.BLACK
            : Color.WHITE;

        Color colOpp = col == Color.WHITE
            ? Color.BLACK
            : Color.WHITE;

        // other stuff
        PType prom  = move.Promotion;
        PType piece = move.Piece;
        PType capt  = move.Capture;

        // en passant
        if (prom == PType.PAWN) {

            // the pawn that is to be captured
            ulong captureSq = col == Color.WHITE
                ? end << 8
                : end >> 8;

            // xor the captured pawn and move our pawn
            Pieces[(byte)colOpp][(byte)PType.PAWN] ^= captureSq;
            Pieces[(byte)col][   (byte)PType.PAWN] ^= start | end;

            if (col == Color.WHITE) {
                WOccupied ^= start | end;
                BOccupied ^= captureSq;
            } else {
                BOccupied ^= start | end;
                WOccupied ^= captureSq;
            }
        }

        // castling
        else if (prom == PType.KING) {

            // get the rook move respetive to the king move
            ulong rook = end switch {
                0x0000000000000004 => 0x0000000000000009, // q
                0x0000000000000040 => 0x00000000000000A0, // k
                0x0400000000000000 => 0x0900000000000000, // Q
                0x4000000000000000 => 0xA000000000000000, // K
                _ => 0UL
            };

            // king
            Pieces[(byte)col][(byte)PType.KING] ^= start | end;

            // rook
            Pieces[(byte)col][(byte)PType.ROOK] ^= rook;

            if (col == Color.WHITE) WOccupied ^= rook | start | end;
            else                    BOccupied ^= rook | start | end;
        }

        // promotion
        else if (prom != PType.NONE) {
            Pieces[(byte)col][(byte)piece] ^= start;
            Pieces[(byte)col][(byte)prom]  ^= end;

            if (col == Color.WHITE) WOccupied ^= start | end;
            else                    BOccupied ^= start | end;
        }

        // regular move
        else {
            //Console.WriteLine($"{col} {piece} {prom}");
            Pieces[(byte)col][(byte)piece] ^= start | end;

            // if we double pushed a pawn, set the en passant square
            if (piece == PType.PAWN && (col == Color.WHITE
                ? start >> 16 == end
                : start << 16 == end))

                // en passant square is the square over which the
                // pawn has double pushed, not the capture square
                EnPassantSq = BB.LS1B(col == Color.WHITE
                    ? start >> 8
                    : start << 8);

            if (col == Color.WHITE) WOccupied ^= start | end;
            else                    BOccupied ^= start | end;
        }

        // capture
        if (capt != PType.NONE) {
            Pieces[(byte)colOpp][(byte)capt] ^= end;

            if (col == Color.WHITE) BOccupied ^= end;
            else                    WOccupied ^= end;
        }

        if (CastlingRights != CastlingRights.NONE && piece == PType.KING) {

            // remove castling rights after a king moves
            CastlingRights &= (CastlingRights)(col == Color.WHITE
                ? 0xC   // all except KQ
                : 0x3); // all except kq
        }

        if (CastlingRights != CastlingRights.NONE
            && (piece == PType.ROOK || capt == PType.ROOK)) {

            // if rook moved we need the starting square
            // if rook was captured we need the ending square
            byte rookSq = piece == PType.ROOK
                ? start8
                : end8;

            byte mask = rookSq switch {
                63 => 0xE, // all except K
                56 => 0xD, // all except Q
                7  => 0xB, // all except k
                0  => 0x7, // all except q
                _  => 0xF
            };

            // remove castling rights after a rook moves
            CastlingRights &= (CastlingRights)mask;
        }
    }

    private void PlayReversibleMove(Move move, Color col) {

        // start & end squares
        ulong start = 1UL << move.Start;
        ulong end   = 1UL << move.End;

        // opposite color
        Color colOpp = col == Color.WHITE
            ? Color.BLACK
            : Color.WHITE;

        // other stuff
        PType prom  = move.Promotion;
        PType piece = move.Piece;
        PType capt  = move.Capture;

        // en passant
        if (prom == PType.PAWN) {
            ulong captureSq = col == Color.WHITE
                ? end << 8
                : end >> 8;

            Pieces[(byte)colOpp][(byte)PType.PAWN] ^= captureSq;
            Pieces[(byte)col][   (byte)PType.PAWN] ^= start | end;
            
            if (colOpp == Color.WHITE) WOccupied ^= captureSq;
            else                       BOccupied ^= captureSq;
        }

        // promotion
        else if (prom is not PType.KING and not PType.NONE) {
            Pieces[(byte)col][(byte)piece] ^= start;
            Pieces[(byte)col][(byte)prom]  ^= end;
        }

        // regular move
        else Pieces[(byte)col][(byte)piece] ^= start | end;

        // capture
        if (capt != PType.NONE) {
            Pieces[(byte)colOpp][(byte)capt] ^= end;

            if (colOpp == Color.WHITE) WOccupied ^= end;
            else                       BOccupied ^= end;
        }

        if (col == Color.WHITE) WOccupied ^= start | end;
        else                    BOccupied ^= start | end;
    }

    #endregion

    // null move used for null move pruning
    [Pure]
    internal Board GetNullChild() {
        Board @null = Clone();

        @null.EnPassantSq = 64;
        @null.Color = @null.Color == Color.WHITE
            ? Color.BLACK
            : Color.WHITE;

        return @null;
    }

    // checks whether a move is legal from this position
    internal bool IsMoveLegal(Move move, Color col) {
        
        PlayReversibleMove(move, col);
        bool isLegal = !Movegen.IsKingInCheck(this, col);
        PlayReversibleMove(move, col);

        return isLegal;
    }

    [Pure]
    internal Board Clone() {

        Board @new = new() {
            WOccupied      = WOccupied,
            BOccupied      = BOccupied,

            CastlingRights = CastlingRights,
            EnPassantSq    = EnPassantSq,
            Color          = Color,
        };

        Array.Copy(Pieces[(byte)Color.WHITE], @new.Pieces[(byte)Color.WHITE], 6);
        Array.Copy(Pieces[(byte)Color.BLACK], @new.Pieces[(byte)Color.BLACK], 6);

        return @new;
    }

    internal void Print() {

        // empty squares are simply dashes
        char[] chars = new char[64];
        Array.Fill(chars, '-');

        // loop all piece types
        for (int i = 0; i < 6; i++) {
            ulong wCopy = Pieces[(byte)Color.WHITE][i];
            ulong bCopy = Pieces[(byte)Color.BLACK][i];

            while (wCopy != 0UL) {
                int sq = BB.LS1BReset(ref wCopy);

                // pieces are uppercase for white
                chars[sq] = char.ToUpper(Consts.Pieces[i]);
            }

            while (bCopy != 0UL) {
                int sq = BB.LS1BReset(ref bCopy);
                chars[sq] = Consts.Pieces[i];
            }
        }
        for (int i = 0; i < 64; i++) {
            UCI.Output.Write($"{chars[i]} {

                // newline character at the end of each rank
                (((i + 1) & 7) == 0 ? '\n' : string.Empty)}");
        }
    }
}

#pragma warning restore CA1304
#pragma warning restore IDE0079