using System.Drawing;
using Stockshrimp_1.evaluation;


/*
 *  Stockshrimp chess engine 1.0
 *  developed by ZlomenyMesic
 */

using Stockshrimp_1.movegen;
using Stockshrimp_1.movegen.pieces;

namespace Stockshrimp_1;

internal static class Game {

    // current game state is saved here
    internal static Board board = new();

    // to let the engine know which side it plays on
    internal static int col_to_play = 0;

    internal static void SetPosFEN(string[] toks) {
        board.Erase();

        // Each rank is described, starting with rank 8 and ending with rank 1,
        // with a "/" between each one; within each rank, the contents of the squares are described
        // in order from the a-file to the h - file.Each piece is identified by a single letter taken
        // from the standard English names in algebraic notation (pawn = "P", knight = "N", bishop = "B",
        // rook = "R", queen = "Q" and king = "K"). White pieces are designated using uppercase letters ("PNBRQK"),
        // while black pieces use lowercase letters ("pnbrqk"). A set of one or more consecutive empty squares
        // within a rank is denoted by a digit from "1" to "8", corresponding to the number of squares.

        for (int i = 0, sq = 0; i < toks[2].Length; i++) {
            char c = toks[2][i];

            if (char.IsDigit(c)) sq += c - '0';

            else if (char.IsLetter(c)) {
                if (!Consts.PIECES.Contains(char.ToLower(toks[2][i]))) {
                    Console.WriteLine($"invalid piece in FEN: {toks[2][i]}");
                    board.Erase();
                    return;
                }

                int col = char.IsUpper(c) ? 0 : 1;
                int piece = Consts.PIECES.IndexOf(char.ToLower(c));

                board.pieces[col, piece] |= Consts.SqMask[sq++];
            }
        }

        // 2. ACTIVE COLOR
        // "w" means white to move, "b" means black.

        switch (toks[3]) {
            case "w": col_to_play = 0; break;
            case "b": col_to_play = 1; break;
            default: Console.WriteLine($"invalid side to move: {toks[3]}"); return;
        }
        board.side_to_move = col_to_play;

        // 3. CASTLING RIGHTS
        // If neither side can castle, this is "-". Otherwise, this has one or more letters: "K" (White can castle kingside),
        // "Q"(White can castle queenside), "k"(Black can castle kingside), and / or "q"(Black can castle queenside).

        board.castling_flags = 0;

        for (int i = 0; i < toks[4].Length; i++) {
            switch (toks[4][i]) {
                case 'K': board.castling_flags |= 0x1; break;
                case 'Q': board.castling_flags |= 0x2; break;
                case 'k': board.castling_flags |= 0x4; break;
                case 'q': board.castling_flags |= 0x8; break;
                default:
                    if (toks[4][i] != '-') {
                        Console.WriteLine($"invalid castling availiability: {toks[2][i]}");
                        return;
                    } else continue;
            }
        }

        // 4. EN PASSANT TARGET SQUARE
        // This is a square over which a pawn has just passed while moving two squares; it is given in algebraic
        // notation. If there is no en passant target square, this field uses the character "-". This is recorded
        // regardless of whether there is a pawn in position to capture en passant. An updated version of the
        // spec has since made it so the target square is only recorded if a legal en passant move is possible but
        // the old version of the standard is the one most commonly used.

        if (toks[5].Length == 2 && char.IsDigit(toks[3][0]) && char.IsDigit(toks[3][1]))
            board.en_passant_sq = (byte)int.Parse(toks[3]);
        else if (toks[5].Length == 1 && toks[5][0] == '-')
            board.en_passant_sq = 64;
        else {
            Console.WriteLine($"invalid en passant square: {toks[3]}");
            return;
        }

        // we don't need the fullmove number, the halfmove number will be done soon

        // 5. HALFMOVE CLOCK
        // The number of halfmoves since the last capture or pawn advance, used for the fifty-move rule.

        // 6 FULLMOVE NUMBER
        // The number of the full moves. It starts at 1 and is incremented after Black's move.

        // TODO: FINISH FEN

        // position command can be followed by a sequence of moves
        int m_start = toks.ToList().IndexOf("moves");

        // if the sequence exists
        if (m_start != -1) {

            // play the moves
            for (int i = m_start + 1; i < toks.Length; i++) {
                board.DoMove(Move.FromString(board, toks[i]));
                col_to_play = col_to_play == 0 ? 1 : 0;
            }
        }

        board.Print();
    }

    internal static void TestingFunction() {

    }
}
