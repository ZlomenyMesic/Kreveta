/*
 *  Stockshrimp chess engine 1.0
 *  developed by ZlomenyMesic
 */

using Stockshrimp_1.movegen;
using System.Drawing;

namespace Stockshrimp_1;

internal class Board {
    // [color, piece_type]
    // all pieces are saved here
    internal ulong[,] pieces = new ulong[2, 6];


    internal byte en_passant_sq = 64;

    
    // 0 0 0 0 q k Q K
    internal byte castling_flags = 0;

    internal int side_to_move = 0;

    // returns all squares occupied by the color
    internal ulong Occupied(int col) {
        return pieces[col, 0] | pieces[col, 1] | pieces[col, 2] | pieces[col, 3] | pieces[col, 4] | pieces[col, 5];
    }

    // all occupied squares
    internal ulong Occupied() {
        return Occupied(0) | Occupied(1);
    }

    // return all empty squares
    internal ulong Empty() {
        return ~(Occupied(0) | Occupied(1));
    }

    // returns the piece at a certain square
    // (color, piece_type)
    internal (int, int) PieceAt(int index) {
        ulong sq = Consts.SqMask[index];

        for (int i = 0; i < 6; i++) {
            
            // white
            if ((pieces[0, i] & sq) != 0) return (0, i);

            // black
            if ((pieces[1, i] & sq) != 0) return (1, i);
        }

        // empty square
        return (2, 6);
    }

    // performs a move on the board
    internal void DoMove(Move move) {

        en_passant_sq = 64;
        side_to_move = side_to_move == 0 ? 1 : 0;

        // start & end squares
        int start_32 = move.Start();
        int end_32 = move.End();

        ulong start = Consts.SqMask[start_32];
        ulong end = Consts.SqMask[end_32];

        // TODO - TRY TO GET COLOR FROM SIDETOMOVE

        // color and opposite color
        int col = (Occupied(0) & start) == 0 ? 1 : 0;
        int col_op = col == 0 ? 1 : 0;

        // other stuff
        int prom = move.Promotion();
        int piece = move.Piece();
        int capt = move.Capture();

        // en passant
        if (prom == 0) {
            ulong cap_sq = col == 0
                ? end << 8
                : end >> 8;

            pieces[col_op, 0] ^= cap_sq;
            pieces[col, 0]    ^= start | end;
        } 

        // castling
        else if (prom == 5) {

            ulong rook = end switch {
                0x0000000000000004 => 0x0000000000000009,
                0x0000000000000040 => 0x00000000000000A0,
                0x0400000000000000 => 0x0900000000000000,
                0x4000000000000000 => 0xA000000000000000,
                _ => 0
            };

            // king
            pieces[col, piece] ^= start | end;

            // rook
            pieces[col, 3] ^= rook;
        }

        // promotion
        else if (prom != 6) {
            pieces[col, piece] ^= start;
            pieces[col, prom] ^= end;
        } 

        // regular move
        else {
            pieces[col, piece] ^= start | end;

            if (piece == 0 && (col == 0 ? (start >> 16 == end) : (start << 16 == end)))
                en_passant_sq = (byte)BB.LS1B(col == 0 ? start >> 8 : start << 8);
        }

        // capture
        if (capt != 6) {
            pieces[col_op, capt] ^= end;
        }

        if (castling_flags != 0 && piece == 5) {
            // remove castling rights after the king moves
            castling_flags = (byte)(castling_flags & (col == 0 ? 0xC : 0x3));
        }

        if (castling_flags != 0 && (piece == 3 || capt == 3)) {

            // if rook moved we check move starting square
            // if rook was captured we check move ending square
            int cause = piece == 3
                ? start_32
                : end_32;

            int mask = cause switch {
                63 => 0xE,
                56 => 0xD,
                7 => 0xB,
                0 => 0x7,
                _ => 0xF
            };

            // remove castling rights after a rook moves
            castling_flags &= (byte)mask;
        }
    }

    internal void DoMoveReversible(Move move, int col) {

        side_to_move = side_to_move == 0 ? 1 : 0;

        // start & end squares
        ulong s = Consts.SqMask[move.Start()];
        ulong e = Consts.SqMask[move.End()];
        ulong s_e = s | e;

        // opposite color
        int col_op = col == 0 ? 1 : 0;

        // other stuff
        int prom = move.Promotion();
        int piece = move.Piece();
        int capt = move.Capture();

        ulong en_p_cap_sq;

        // en passant
        if (prom == 0) {
            en_p_cap_sq = col == 0
                ? e << 8
                : e >> 8;

            pieces[col_op, 0] ^= en_p_cap_sq;
            pieces[col, 0] ^= s_e;
        }

        // promotion
        else if (prom != 5 && prom != 6) {
            pieces[col, piece] ^= s;
            pieces[col, prom] ^= e;
        }

        // regular move
        else pieces[col, piece] ^= s_e;

        // capture
        if (capt != 6) pieces[col_op, capt] ^= e;
    }

    internal List<Board> GetChildren() {

        List<Move> moves = Movegen.GetLegalMoves(this);

        List<Board> children = [];

        for (int i = 0; i < moves.Count; i++) {

            Board child = Clone();
            child.DoMove(moves[i]);
            children.Add(child);
        }

        return children;
    }

    // no move
    internal Board GetNullChild() {
        Board c = Clone();

        c.en_passant_sq = 64;
        c.side_to_move = c.side_to_move == 0 ? 1 : 0;

        return c;
    }

    internal bool IsMoveLegal(Move move, int col) {

        DoMoveReversible(move, col);

        bool is_legal = !Movegen.IsKingInCheck(this, col);

        DoMoveReversible(move, col);

        return is_legal;
    }

    internal void Erase() {
        for (int i = 0; i < 6; i++) {
            pieces[0, i] = 0;
            pieces[1, i] = 0;
        }

        en_passant_sq = 0;
        castling_flags = 0;
    }

    internal Board Clone() {
        Board n = new() {
            castling_flags = castling_flags,
            en_passant_sq = en_passant_sq,
            side_to_move = side_to_move
        };

        for (int i = 0; i < 6; i++) {
            n.pieces[0, i] = pieces[0, i];
            n.pieces[1, i] = pieces[1, i];
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

                    c_pieces[index] = Consts.PIECES[j];
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
