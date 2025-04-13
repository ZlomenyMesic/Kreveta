//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.movegen;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Kreveta;

internal sealed class Board {

    // all pieces are saved here in so called bitboards.
    // we have 12 different ulongs (bitboards) which represent
    // all possible pieces. these bitboards are empty and
    // the pieces are stored as one-bits in these large bbs.
    // since a chessboard has 64 squares and ulong has 64
    // bits, we don't waste any memory or anything else.
    [Required]
    [DebuggerDisplay("indexed [color, piece_type]")]
    internal ulong[,] Pieces = new ulong[2, 6];

    // these two bitboards simply represent all occupied
    // squares by a certain color. it turns out to be a little
    // faster than OR the bitboards above, although it takes
    // 16 additional bytes of memory.
    internal ulong WOccupied = 0;
    internal ulong BOccupied = 0;

    // all occupied squares
    [ReadOnly(true)]
    [DefaultValue(0UL)]
    internal ulong Occupied => WOccupied | BOccupied;

    // all empty squares (bitwise inverse of occupied)
    [ReadOnly(true)]
    [DefaultValue(0xFFFFFFFFFFFFFFFFUL)]
    internal ulong Empty => ~Occupied;

    // square over which a double pushing
    // pawn has passed one move ago
    internal byte enPassantSq = 64;

    // the current state of castling rights
    // 0 0 0 0 q k Q K
    [EnumDataType(typeof(CastlingRights))]
    internal CastlingRights castRights = 0;

    // the side to move
    [EnumDataType(typeof(Color))]
    internal Color color = 0;

    internal void Clear() {
        Pieces      = new ulong[2, 6];
        WOccupied   = 0;
        BOccupied   = 0;

        enPassantSq = 64;
        castRights  = CastlingRights.NONE;
        color       = Color.NONE;
    }

    // returns the piece at a certain square
    // (color, piece_type)
    [Obsolete("PieceAt is fairly slow, try a different approach", false)]
    internal (Color col, PType type) PieceAt(int index) {
        ulong sq = Consts.SqMask[index];

        if ((Empty & sq) != 0) 
            return (Color.NONE, PType.NONE);

        for (int i = 0; i < 6; i++) {
            
            // white
            if ((Pieces[(byte)Color.WHITE, i] & sq) != 0)
                return (Color.WHITE, (PType)i);

            // black
            if ((Pieces[(byte)Color.BLACK, i] & sq) != 0)
                return (Color.BLACK, (PType)i);
        }

        // empty square
        return (Color.NONE, PType.NONE);
    }

    // performs a move on the board
    internal void PlayMove(Move move) {

        enPassantSq = 64;
        color = color == Color.WHITE 
            ? Color.BLACK 
            : Color.WHITE;

        // start and end squares
        int start32 = move.Start;
        int end32   = move.End;

        // start and end squares represented as bitboards
        ulong start = Consts.SqMask[start32];
        ulong end   = Consts.SqMask[end32];

        // TODO - TRY TO GET COLOR FROM SIDETOMOVE

        // color and opposite color
        Color col = (WOccupied & start) == 0 
            ? Color.BLACK 
            : Color.WHITE;

        Color colOpp = col == Color.WHITE ? Color.BLACK : Color.WHITE;

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
            Pieces[(byte)colOpp, (byte)PType.PAWN] ^= captureSq;
            Pieces[(byte)col,    (byte)PType.PAWN] ^= start | end;

            if (col == Color.WHITE) {
                WOccupied ^= start | end;
                BOccupied ^= captureSq;
            }
            else {
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
                _ => 0
            };

            // king
            Pieces[(byte)col, (byte)PType.KING] ^= start | end;

            // rook
            Pieces[(byte)col, (byte)PType.ROOK] ^= rook;

            if (col == Color.WHITE) WOccupied ^= rook | start | end;
            else BOccupied ^= rook | start | end;
        }

        // promotion
        else if (prom != PType.NONE) {
            Pieces[(byte)col, (byte)piece] ^= start;
            Pieces[(byte)col, (byte)prom]  ^= end;

            if (col == Color.WHITE) WOccupied ^= start | end;
            else BOccupied ^= start | end;
        } 

        // regular move
        else {
            //Console.WriteLine($"{col} {piece} {prom}");
            Pieces[(byte)col, (byte)piece] ^= start | end;

            // if we double pushed a pawn, set the en passant square
            if (piece == PType.PAWN && (col == Color.WHITE 
                ? (start >> 16 == end) 
                : (start << 16 == end)))

                // en passant square is the square over which the
                // pawn has double pushed, not the capture square
                enPassantSq = (byte)BB.LS1B(col == Color.WHITE 
                    ? start >> 8 
                    : start << 8);

            if (col == Color.WHITE) WOccupied ^= start | end;
            else BOccupied ^= start | end;
        }

        // capture
        if (capt != PType.NONE) {
            Pieces[(byte)colOpp, (byte)capt] ^= end;

            if (col == Color.WHITE) BOccupied ^= end;
            else WOccupied ^= end;
        }

        if (castRights != CastlingRights.NONE && piece == PType.KING) {

            // remove castling rights after a king moves
            castRights &= (CastlingRights)(col == Color.WHITE 
                ? 0xC   // all except KQ
                : 0x3); // all except kq
        }

        if (castRights != CastlingRights.NONE 
            && (piece == PType.ROOK || capt == PType.ROOK)) {

            // if rook moved we need the starting square
            // if rook was captured we need the ending square
            int rookSq = piece == PType.ROOK
                ? start32
                : end32;

            int mask = rookSq switch {
                63 => 0xE, // all except K
                56 => 0xD, // all except Q
                7  => 0xB, // all except k
                0  => 0x7, // all except q
                _  => 0xF
            };

            // remove castling rights after a rook moves
            castRights &= (CastlingRights)mask;
        }
    }

    internal void PlayReversibleMove(Move move, Color col) {

        color = color == Color.WHITE 
            ? Color.BLACK 
            : Color.WHITE;

        // start & end squares
        ulong start = Consts.SqMask[move.Start];
        ulong end  = Consts.SqMask[move.End];

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

            Pieces[(byte)colOpp, (byte)PType.PAWN] ^= captureSq;
            Pieces[(byte)col,    (byte)PType.PAWN] ^= start | end;
        }

        // promotion
        else if (prom != PType.KING && prom != PType.NONE) {
            Pieces[(byte)col, (byte)piece] ^= start;
            Pieces[(byte)col, (byte)prom]  ^= end;
        }

        // regular move
        else Pieces[(byte)col, (byte)piece] ^= start | end;

        // capture
        if (capt != PType.NONE) {
            Pieces[(byte)colOpp, (byte)capt] ^= end;

            if (colOpp == Color.WHITE) WOccupied ^= end;
            else BOccupied ^= end;
        }

        if (col == Color.WHITE) WOccupied ^= start | end;
        else BOccupied ^= start | end;
    }

    internal List<Board> GetChildren() {

        List<Move> moves = Movegen.GetLegalMoves(this).ToList();

        List<Board> children = [];

        for (int i = 0; i < moves.Count; i++) {

            Board child = Clone();
            child.PlayMove(moves[i]);
            children.Add(child);
        }

        return children;
    }

    // no move
    internal Board GetNullChild() {
        Board @null = Clone();

        @null.enPassantSq = 64;
        @null.color = @null.color == Color.WHITE 
            ? Color.BLACK 
            : Color.WHITE;

        return @null;
    }

    internal bool IsMoveLegal(Move move, Color col) {

        PlayReversibleMove(move, col);

        bool isLegal = !Movegen.IsKingInCheck(this, col);

        PlayReversibleMove(move, col);

        return isLegal;
    }

    internal Board Clone() {
        Board @new = new() {
            WOccupied   = WOccupied,
            BOccupied   = BOccupied,

            castRights  = castRights,
            enPassantSq = enPassantSq,
            color       = color
        };

        for (int i = 0; i < 6; i++) {
            @new.Pieces[(byte)Color.WHITE, i] = Pieces[(byte)Color.WHITE, i];
            @new.Pieces[(byte)Color.BLACK, i] = Pieces[(byte)Color.BLACK, i];
        }

        return @new;
    }

    internal void Print() {
        char[] chars = new char[64];
        Array.Fill(chars, '-');

        for (int i = 0; i < 2; i++) {
            for (int j = 0; j < 6; j++) {
                ulong copy = Pieces[i, j];

                while (true) {
                    int index = BB.LS1BReset(ref copy);
                    if (index == -1) break;

                    chars[index] = Consts.Pieces[j];
                    if (i == 0) chars[index] = char.ToUpper(chars[index]);
                }
            }
        }

        for (int i = 0; i < 64; i++) {
            Console.Write($"{chars[i]} ");
            if ((i + 1) % 8 == 0) Console.WriteLine();
        }
    }
}
