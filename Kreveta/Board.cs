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
        int start_32 = move.Start();
        int end_32 = move.End();

        // start and end squares represented as bitboards
        ulong start = Consts.SqMask[start_32];
        ulong end = Consts.SqMask[end_32];

        // TODO - TRY TO GET COLOR FROM SIDETOMOVE

        // color and opposite color
        Color col = (Occupied(Color.WHITE) & start) == 0 
            ? Color.BLACK 
            : Color.WHITE;
        Color col_op = col == Color.WHITE ? Color.BLACK : Color.WHITE;

        // other stuff
        PType prom  = move.Promotion();
        PType piece = move.Piece();
        PType capt  = move.Capture();

        // en passant
        if (prom == PType.PAWN) {

            // the pawn that is to be captured
            ulong cap_sq = col == Color.WHITE
                ? end << 8
                : end >> 8;

            // xor the captured pawn and move our pawn
            pieces[(byte)col_op, (byte)PType.PAWN] ^= cap_sq;
            pieces[(byte)col,    (byte)PType.PAWN] ^= start | end;
        } 

        // castling
        else if (prom == PType.KING) {

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

            if (piece == PType.PAWN && (col == Color.WHITE ? (start >> 16 == end) : (start << 16 == end)))
                enPassantSq = (byte)BB.LS1B(col == Color.WHITE ? start >> 8 : start << 8);
        }

        // capture
        if (capt != PType.NONE) {
            pieces[(byte)col_op, (byte)capt] ^= end;
        }

        if (castRights != CastlingRights.NONE && piece == PType.KING) {

            // remove castling rights after a king moves
            castRights &= (CastlingRights)(col == Color.WHITE 
                ? 0xC   // all except KQ
                : 0x3); // all except kq
        }

        if (castRights != CastlingRights.NONE 
            && (piece == PType.ROOK || capt == PType.ROOK)) {

            // if rook moved we check move starting square
            // if rook was captured we check move ending square
            int cause = piece == PType.ROOK
                ? start_32
                : end_32;

            int mask = cause switch {
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

        color = color == Color.WHITE ? Color.BLACK : Color.WHITE;

        // start & end squares
        ulong start = Consts.SqMask[move.Start()];
        ulong end = Consts.SqMask[move.End()];
        ulong s_e = start | end;

        // opposite color
        Color col_op = col == Color.WHITE 
            ? Color.BLACK 
            : Color.WHITE;

        // other stuff
        PType prom =  move.Promotion();
        PType piece = move.Piece();
        PType capt =  move.Capture();

        ulong en_p_cap_sq;

        // en passant
        if (prom == PType.PAWN) {
            en_p_cap_sq = col == Color.WHITE
                ? end << 8
                : end >> 8;

            pieces[(byte)col_op, (byte)PType.PAWN] ^= en_p_cap_sq;
            pieces[(byte)col,    (byte)PType.PAWN] ^= s_e;
        }

        // promotion
        else if (prom != PType.KING && prom != PType.NONE) {
            pieces[(byte)col, (byte)piece] ^= start;
            pieces[(byte)col, (byte)prom]  ^= end;
        }

        // regular move
        else pieces[(byte)col, (byte)piece] ^= s_e;

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
        Board c = Clone();

        c.enPassantSq = 64;
        c.color = c.color == Color.WHITE 
            ? Color.BLACK 
            : Color.WHITE;

        return c;
    }

    internal bool IsMoveLegal(Move move, Color col) {

        PlayReversibleMove(move, col);

        bool is_legal = !Movegen.IsKingInCheck(this, col);

        PlayReversibleMove(move, col);

        return is_legal;
    }

    internal void Erase() {
        for (int i = 0; i < 6; i++) {
            pieces[(byte)Color.WHITE, i] = 0;
            pieces[(byte)Color.BLACK, i] = 0;
        }

        enPassantSq = 0;
        castRights = 0;
        color = Color.NONE;
    }

    internal Board Clone() {
        Board n = new() {
            castRights = castRights,
            enPassantSq = enPassantSq,
            color = color
        };

        for (int i = 0; i < 6; i++) {
            n.pieces[(byte)Color.WHITE, i] = pieces[(byte)Color.WHITE, i];
            n.pieces[(byte)Color.BLACK, i] = pieces[(byte)Color.BLACK, i];
        }

        return n;
    }

    internal void Print() {
        char[] c_pieces = new char[64];
        Array.Fill(c_pieces, '-');

        for (int i = 0; i < 2; i++) {
            for (int j = 0; j < 6; j++) {
                ulong copyPieces = pieces[i, j];

                while (true) {
                    (copyPieces, int index) = BB.LS1BReset(copyPieces);
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
