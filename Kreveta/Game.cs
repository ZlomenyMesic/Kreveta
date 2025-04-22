//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.movegen;
using Kreveta.openingbook;
using Kreveta.search;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Kreveta;

internal static class Game {

    // current chessboard state is saved here (root node)
    internal static Board board = new();

    // the side the engine plays on
    internal static Color color = 0;

    // if the engine receives the "ucinewgame" command, we know
    // we will be playing a whole game rather than just analyzing
    // a single position. this allows us to implement some stuff,
    // such as keeping the tt from the previous turn
    internal static bool FullGame = false;

    // used to save previous positions to avoid (or embrace) 3-fold repetition
    internal static List<ulong>    HistoryPositions = [];
    internal static HashSet<ulong> Draws            = [];

    internal static void SetPosFEN([NotNull, In, ReadOnly(true)] in string[] toks) {

        // clear the board from previous game/search
        board.Clear();

        // erase the draw counters
        HistoryPositions = [];
        Draws            = [];

        // 1. POSITION
        // starting from rank 8 to rank 1, ranks are separated by a "/". on each rank, pieces are
        // denoted as per the standard algebraic notation (PNBRQK-pnbrqk). one or more empty squares
        // between pieces are denoted by a single digit (1-8), corresponding to the number of squares
        for (int i = 0, sq = 0; i < toks[2].Length; i++) {
            char c = toks[2][i];

            // increase the square counter (empty squares)
            if (char.IsDigit(c)) {
                sq += c - '0';
                continue;
            }

            // wrong letter or character?
            if (!Consts.Pieces.Contains(char.ToLower(c))) {

                // this character is used to indicate another
                // rank. we don't need this information, though
                if (c is '\\' or '/')
                    continue;

                UCI.Log($"invalid character in FEN: {c}", UCI.LogLevel.ERROR);

                // clear the board to prevent chaos
                board.Clear();
                return;
            }

            // color (white = uppercase, black = lowercase)
            Color col = char.IsUpper(c)
                ? Color.WHITE
                : Color.BLACK;

            // piece type (0-5)
            int piece = Consts.Pieces.IndexOf(char.ToLower(c));

            // add the piece to the board
            board.Pieces[(byte)col, piece] |= Consts.SqMask[sq];

            if (col == Color.WHITE) board.WOccupied |= Consts.SqMask[sq];
            else board.BOccupied |= Consts.SqMask[sq];

            sq++;
        }

        // 2. ACTIVE COLOR
        // which color's turn is it
        switch (toks[3]) {

            // white
            case "w": color = Color.WHITE; break;

            // black
            case "b": color = Color.BLACK; break;

            default:  UCI.Log($"invalid side to move: {toks[3]}", UCI.LogLevel.ERROR);

                      board.Clear(); 
                      return;
        }
        board.color = color;

        // 3. CASTLING RIGHTS
        // if neither side can castle, this is "-". otherwise, we can have up to 4 letters.
        // "k" and "q" marks kingside and queenside castling rights respectively. just to clarify, this has nothing 
        // to do with the legality of castling in the next move, it denotes the castling rights availability.
        for (int i = 0; i < toks[4].Length; i++) {
            switch (toks[4][i]) {

                // white kingside
                case 'K': board.castRights |= CastlingRights.K; break;

                // white queenside
                case 'Q': board.castRights |= CastlingRights.Q; break;

                // black kingside
                case 'k': board.castRights |= CastlingRights.k; break;

                // black queenside
                case 'q': board.castRights |= CastlingRights.q; break;

                default:
                    if (toks[4][i] != '-') {
                        UCI.Log($"invalid castling availability: {toks[2][i]}", UCI.LogLevel.ERROR);

                        board.Clear();
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

        if (toks[5].Length == 2 && byte.TryParse(toks[5], out byte enPassantSq))
            board.enPassantSq = enPassantSq;

        else if (toks[5].Length == 1 && toks[5][0] == '-')
            board.enPassantSq = 64;

        else {
            UCI.Log($"invalid en passant square: {toks[5]}", UCI.LogLevel.ERROR);

            board.Clear();
            return;
        }

        // the initial board hash
        //board.Hash = Zobrist.GetHash(board);

        // we don't need the fullmove number, the halfmove number will be done soon

        // 5. HALFMOVE CLOCK
        // The number of halfmoves since the last capture or pawn advance, used for the fifty-move rule.

        // 6 FULLMOVE NUMBER
        // The number of full moves. It starts at 1 and is incremented after Black's move.

        // TODO: FINISH FEN

        // position command can be followed by a sequence of moves
        int moveSeqStart = toks.ToList().IndexOf("moves");

        // we save the previous positions as 3-fold repetition exists
        HistoryPositions.Add(Zobrist.GetHash(board));

        List<string> sequence = [];

        // if the sequence exists
        if (moveSeqStart != -1) {

            // play the sequence of moves
            for (int i = moveSeqStart + 1; i < toks.Length; i++) {
                sequence.Add(toks[i]);

                if (!Move.IsCorrectFormat(toks[i])) {
                    UCI.Log($"invalid move: {toks[i]}", UCI.LogLevel.ERROR);

                    board.Clear();
                    return;
                }

                board.PlayMove(Move.FromString(board, toks[i]));
                HistoryPositions.Add(Zobrist.GetHash(board));

                // switch the engine's color
                color = color == Color.WHITE 
                    ? Color.BLACK 
                    : Color.WHITE;
            }
        }

        // try to save a book move
        OpeningBook.SaveSequence([.. sequence], toks[2]);

        // save drawing positions in "draws"
        List3FoldDraws();

        //board.Hash = Zobrist.GetHash(board);
        board.Print();
    }

    // save all positions that would cause a 3-fold repetition draw in
    // the next move. all previous positions are save in history_positions
    // and those which occur twice (or more) are considered as drawing.
    private static void List3FoldDraws() {
        Dictionary<ulong, int> occurences = [];

        foreach (ulong hash in HistoryPositions) {

            // the position has already occured
            if (occurences.TryGetValue(hash, out _)) {

                // increase the counter and if we reached 2,
                // save the position as a 3-fold repetition draw
                if (++occurences[hash] == 2) {
                    Draws.Add(hash);
                }
            }

            // otherwise add the first occurence
            else occurences.Add(hash, 1);
        }
    }

    internal static void TestingFunction() {

    }
}
