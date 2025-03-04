/*
 *  Stockshrimp chess engine 1.0
 *  developed by ZlomenyMesic
 */

using Stockshrimp_1.movegen;

namespace Stockshrimp_1;

internal class Board {
    // [color, piece_type]
    // all pieces are saved here
    internal ulong[,] pieces = new ulong[2, 6];


    internal byte enPassantSquare = 0;

    
    // 0 0 0 0 q k Q K
    internal byte castlingFlags = 0;

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
    internal void PlayMove(Move move) {

        enPassantSquare = 0;

        // start & end squares
        int start_32 = move.Start();
        ulong start = Consts.SqMask[start_32];
        ulong end = Consts.SqMask[move.End()];

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
            int r_s = 0;
            int r_e = 0;

            switch (end) {
                case 0x0000000000000004: r_s = 0;  r_e = 3;  break;
                case 0x0000000000000040: r_s = 7;  r_e = 5;  break;
                case 0x0400000000000000: r_s = 56; r_e = 59; break;
                case 0x4000000000000000: r_s = 63; r_e = 61; break;
            }

            // king
            pieces[col, piece] ^= start | end;

            // rook
            PlayMove(new(r_s, r_e, 3, 6, 6));
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
                enPassantSquare = (byte)BB.LS1B(col == 0 ? start >> 8 : start << 8);
        }

        // capture
        if (capt != 6) {
            pieces[col_op, capt] ^= end;
        }

        if (castlingFlags != 0 && piece == 5) {
            // remove castling rights after the king moves
            castlingFlags = (byte)(castlingFlags & (col == 0 ? 0xC : 0x3));
        }

        if (castlingFlags != 0 && piece == 3) {

            int mask = 0;
            switch (start_32) {
                case 63: mask &= 0xE; break;
                case 56: mask &= 0xD; break;
                case 7:  mask &= 0xB; break;
                case 0:  mask &= 0x7; break;
            }

            // remove castling rights after a rook moves
            castlingFlags &= (byte)mask;
        }
    }

    internal Board[] GenerateChildren(int col) {
        Move[] legal = Movegen.GetLegalMoves(this, col);
        Board[] children = new Board[legal.Length];

        for (int i = 0; i < legal.Length; i++) {
            children[i] = Clone();
            children[i].PlayMove(legal[i]);
        }

        return children;
    }

    internal bool IsLegal(Move m) {
        (int col, _) = PieceAt(m.Start());

        Board clone = Clone();
        clone.PlayMove(m);

        return !Movegen.IsKingChecked(clone, col);
    }

    internal void Erase() {
        for (int i = 0; i < 6; i++) {
            pieces[0, i] = 0;
            pieces[1, i] = 0;
        }

        enPassantSquare = 0;
        castlingFlags = 0;
    }

    internal Board Clone() {
        Board b = new() {
            castlingFlags = castlingFlags,
            enPassantSquare = enPassantSquare
        };

        for (int i = 0; i < 6; i++) {
            b.pieces[0, i] = pieces[0, i];
            b.pieces[1, i] = pieces[1, i];
        }

        return b;
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
