/*
 * |============================|
 * |                            |
 * |    Kreveta chess engine    |
 * | engineered by ZlomenyMesic |
 * | -------------------------- |
 * |      started 4-3-2025      |
 * | -------------------------- |
 * |                            |
 * | read README for additional |
 * | information about the code |
 * |    and usage that isn't    |
 * |  included in the comments  |
 * |                            |
 * |============================|
 */

using Kreveta.movegen;
using Kreveta.opening_book;
using Kreveta.search;

namespace Kreveta;

internal static class Game {

    // current chessboard state is saved here (root node)
    internal static Board board = new();

    // to let the engine know which side it plays on
    internal static int engine_col = 0;

    // used to save previous positions to avoid (or embrace) 3-fold repetition
    internal static List<ulong> history_positions = [];
    internal static List<ulong> draws = [];

    internal static void SetPosFEN(string[] toks) {

        // clear the board from previous game/search
        board.Erase();

        // erase the draw counters
        history_positions = [];
        draws = [];

        // 1. POSITION
        // starting from rank 8 to rank 1, ranks are separated by a "/". on each rank, pieces are
        // denoted as per the standard algebraic notation (PNBRQK-pnbrqk). one or more empty squares
        // between pieces are denoted by a single digit (1-8), corresponding to the number of squares
        for (int i = 0, sq = 0; i < toks[2].Length; i++) {
            char c = toks[2][i];

            // increase the square counter (empty squares)
            if (char.IsDigit(c)) sq += c - '0';

            else if (char.IsLetter(c)) {

                // wrong letter?
                if (!Consts.PIECES.Contains(char.ToLower(toks[2][i]))) {
                    Console.WriteLine($"invalid piece in FEN: {toks[2][i]}");

                    // clear the board to prevent chaos
                    board.Erase();
                    return;
                }

                // color (white = uppercase, black = lowercase)
                int col = char.IsUpper(c) ? 0 : 1;

                // piece type (0-5)
                int piece = Consts.PIECES.IndexOf(char.ToLower(c));

                // add the piece to the board
                board.pieces[col, piece] |= Consts.SqMask[sq++];
            }
        }

        // 2. ACTIVE COLOR
        // which color's turn is it
        switch (toks[3]) {

            // white
            case "w": engine_col = 0; break;

            // black
            case "b": engine_col = 1; break;

            default: Console.WriteLine($"invalid side to move: {toks[3]}"); return;
        }
        board.color = engine_col;

        // 3. CASTLING RIGHTS
        // if neither side can castle, this is "-". otherwise, we can have up to 4 letters.
        // "k" and "q" marks kingside and queenside castling rights respectively. just to clarify, this has nothing 
        // to do with the legality of castling in the next move, it denotes the castling rights availability.
        board.castling = 0;

        for (int i = 0; i < toks[4].Length; i++) {
            switch (toks[4][i]) {

                // white kingside
                case 'K': board.castling |= Board.CastlingRights.K; break;

                // white queenside
                case 'Q': board.castling |= Board.CastlingRights.Q; break;

                // black kingside
                case 'k': board.castling |= Board.CastlingRights.k; break;

                // black queenside
                case 'q': board.castling |= Board.CastlingRights.q; break;

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
        // The number of full moves. It starts at 1 and is incremented after Black's move.

        // TODO: FINISH FEN

        // position command can be followed by a sequence of moves
        int m_start = toks.ToList().IndexOf("moves");

        // we save the previous positions as 3-fold repetition exists
        history_positions.Add(Zobrist.GetHash(board));

        List<string> sequence = [];

        // if the sequence exists
        if (m_start != -1) {

            // play the sequence of moves
            for (int i = m_start + 1; i < toks.Length; i++) {
                sequence.Add(toks[i]);

                board.PlayMove(Move.FromString(board, toks[i]));
                history_positions.Add(Zobrist.GetHash(board));

                // switch the engine's color
                engine_col = engine_col == 0 ? 1 : 0;
            }
        }

        // try to save a book move
        OpeningBook.SaveSequence([.. sequence], toks[2]);

        // save drawing positions in "draws"
        List3FoldDraws();

        board.Print();
    }

    // save all positions that would cause a 3-fold repetition draw in
    // the next move. all previous positions are save in history_positions
    // and those which occur twice (or more) are considered as drawing.
    private static void List3FoldDraws() {
        Dictionary<ulong, int> occurences = [];

        foreach (ulong hash in history_positions) {

            // the position has already occured
            if (occurences.TryGetValue(hash, out _)) {

                // increase the counter and if we reached 2,
                // save the position as a 3-fold repetition draw
                if (++occurences[hash] == 2) {
                    draws.Add(hash);
                }
            }

            // otherwise add the first occurence
            else occurences.Add(hash, 1);
        }
    }

    internal static void TestingFunction() {

    }
}
