//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.movegen;

namespace Kreveta;

internal class Board {

    // [color, piece_type]
    // all pieces are saved here
    internal ulong[,] pieces = new ulong[2, 6];

    // square over which a double pushing pawn has passed one move ago
    internal byte enPassantSq = 64;

    // 0 0 0 0 q k Q K
    internal CastlingRights castRights = 0;

    internal Color color = 0;

    internal void Clear() {
        pieces      = new ulong[2, 6];
        enPassantSq = 64;
        castRights  = CastlingRights.NONE;
        color       = Color.NONE;
    }

    // returns all squares occupied by the color
    internal ulong Occupied(Color col) {
        return pieces[(byte)col, (byte)PType.PAWN] 
             | pieces[(byte)col, (byte)PType.KNIGHT] 
             | pieces[(byte)col, (byte)PType.BISHOP] 
             | pieces[(byte)col, (byte)PType.ROOK] 
             | pieces[(byte)col, (byte)PType.QUEEN] 
             | pieces[(byte)col, (byte)PType.KING];
    }

    // all occupied squares
    internal ulong Occupied() {
        return Occupied(Color.WHITE) 
             | Occupied(Color.BLACK);
    }

    // return all empty squares
    internal ulong Empty() {
        return ~(Occupied(Color.WHITE) 
               | Occupied(Color.BLACK));
    }

    // returns the piece at a certain square
    // (color, piece_type)
    internal (Color col, PType type) PieceAt(int index) {
        ulong sq = Consts.SqMask[index];

        for (int i = 0; i < 6; i++) {
            
            // white
            if ((pieces[(byte)Color.WHITE, i] & sq) != 0)
                return (Color.WHITE, (PType)i);

            // black
            if ((pieces[(byte)Color.BLACK, i] & sq) != 0)
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
        int start32 = move.Start();
        int end32   = move.End();

        // start and end squares represented as bitboards
        ulong start = Consts.SqMask[start32];
        ulong end   = Consts.SqMask[end32];

        // TODO - TRY TO GET COLOR FROM SIDETOMOVE

        // color and opposite color
        Color col = (Occupied(Color.WHITE) & start) == 0 
            ? Color.BLACK 
            : Color.WHITE;
        Color colOpp = col == Color.WHITE ? Color.BLACK : Color.WHITE;

        // other stuff
        PType prom  = move.Promotion();
        PType piece = move.Piece();
        PType capt  = move.Capture();

        // en passant
        if (prom == PType.PAWN) {

            // the pawn that is to be captured
            ulong captureSq = col == Color.WHITE
                ? end << 8
                : end >> 8;

            // xor the captured pawn and move our pawn
            pieces[(byte)colOpp, (byte)PType.PAWN] ^= captureSq;
            pieces[(byte)col,    (byte)PType.PAWN] ^= start | end;
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
            pieces[(byte)col, (byte)piece] ^= start | end;

            // rook
            pieces[(byte)col, (byte)PType.ROOK] ^= rook;
        }

        // promotion
        else if (prom != PType.NONE) {
            pieces[(byte)col, (byte)piece] ^= start;
            pieces[(byte)col, (byte)prom]  ^= end;
        } 

        // regular move
        else {
            pieces[(byte)col, (byte)piece] ^= start | end;

            // if we double pushed a pawn, set the en passant square
            if (piece == PType.PAWN && (col == Color.WHITE 
                ? (start >> 16 == end) 
                : (start << 16 == end)))

                // en passant square is the square over which the
                // pawn has double pushed, not the capture square
                enPassantSq = (byte)BB.LS1B(col == Color.WHITE 
                    ? start >> 8 
                    : start << 8);
        }

        // capture
        if (capt != PType.NONE) {
            pieces[(byte)colOpp, (byte)capt] ^= end;
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
        ulong start = Consts.SqMask[move.Start()];
        ulong end  = Consts.SqMask[move.End()];

        // opposite color
        Color col_op = col == Color.WHITE 
            ? Color.BLACK 
            : Color.WHITE;

        // other stuff
        PType prom  = move.Promotion();
        PType piece = move.Piece();
        PType capt  = move.Capture();

        // en passant
        if (prom == PType.PAWN) {
            ulong captureSq = col == Color.WHITE
                ? end << 8
                : end >> 8;

            pieces[(byte)col_op, (byte)PType.PAWN] ^= captureSq;
            pieces[(byte)col,    (byte)PType.PAWN] ^= start | end;
        }

        // promotion
        else if (prom != PType.KING && prom != PType.NONE) {
            pieces[(byte)col, (byte)piece] ^= start;
            pieces[(byte)col, (byte)prom]  ^= end;
        }

        // regular move
        else pieces[(byte)col, (byte)piece] ^= start | end;

        // capture
        if (capt != PType.NONE) pieces[(byte)col_op, (byte)capt] ^= end;
    }

    internal List<Board> GetChildren() {

        List<Move> moves = Movegen.GetLegalMoves(this);

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
            castRights  = castRights,
            enPassantSq = enPassantSq,
            color       = color
        };

        for (int i = 0; i < 6; i++) {
            @new.pieces[(byte)Color.WHITE, i] = pieces[(byte)Color.WHITE, i];
            @new.pieces[(byte)Color.BLACK, i] = pieces[(byte)Color.BLACK, i];
        }

        return @new;
    }

    internal void Print() {
        char[] c_pieces = new char[64];
        Array.Fill(c_pieces, '-');

        for (int i = 0; i < 2; i++) {
            for (int j = 0; j < 6; j++) {
                ulong copy = pieces[i, j];

                while (true) {
                    (copy, int index) = BB.LS1BReset(copy);
                    if (index == -1) break;

                    c_pieces[index] = Consts.Pieces[j];
                    if (i == 0) c_pieces[index] = char.ToUpper(c_pieces[index]);
                }
            }
        }

        for (int i = 0; i < 64; i++) {
            Console.Write($"{c_pieces[i]} ");
            if ((i + 1) % 8 == 0) Console.WriteLine();
        }
    }
}
