//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

// Remove unnecessary suppression
#pragma warning disable IDE0079

// Specify CultureInfo
#pragma warning disable CA1304

using Kreveta.movegen;
using Kreveta.openingbook;
using Kreveta.search;

using System.ComponentModel;
using System.Runtime.InteropServices;

// ReSharper disable InconsistentNaming

namespace Kreveta;

internal static class Game {

    // current chessboard state is saved here (root node)
    internal static Board board = new();

    // the side the engine plays on
    internal static Color color;

    // if the engine receives the "ucinewgame" command, we know
    // we will be playing a whole game rather than just analyzing
    // a single position. this allows us to implement some stuff,
    // such as keeping the tt from the previous turn
    internal static bool FullGame;

    // used to save previous positions to avoid (or embrace) 3-fold repetition
    private static List<ulong>     HistoryPositions = [];
    internal static HashSet<ulong> Draws            = [];

    private static readonly Action<string> InvalidFENCallback = delegate(string context) {
        board.Clear();
        UCI.Log($"invalid position - {context}", UCI.LogLevel.ERROR);
    };

    internal static void SetPosFEN([In, ReadOnly(true)] in string[] tokens) {

        // clear the board from previous game/search
        board = new();

        // erase the draw counters
        HistoryPositions = [];
        Draws            = [];

        // 1. POSITION
        // starting from rank 8 to rank 1, ranks are separated by a "/". on each rank, pieces are
        // denoted as per the standard algebraic notation (PNBRQK-pnbrqk). one or more empty squares
        // between pieces are denoted by a single digit (1-8), corresponding to the number of squares
        for (int i = 0, sq = 0; i < tokens[2].Length; i++) {
            char c = tokens[2][i];

            // increase the square counter (empty squares)
            if (char.IsDigit(c)) {
                sq += c - '0';
                continue;
            }

            // wrong letter or character?
            if (!Consts.Pieces.Contains(char.ToLower(c), StringComparison.Ordinal)) {

                // this character is used to indicate another
                // rank. we don't need this information, though
                if (c is '\\' or '/')
                    continue;

                InvalidFENCallback($"invalid character in FEN: {c}");
                return;
            }

            // color (white = uppercase, black = lowercase)
            Color col = char.IsUpper(c)
                ? Color.WHITE
                : Color.BLACK;

            // piece type (0-5)
            int piece = Consts.Pieces.IndexOf(char.ToLower(c), StringComparison.Ordinal);

            // add the piece to the board
            board.Pieces[(byte)col][piece] |= 1UL << sq;

            if (col == Color.WHITE) board.WOccupied |= 1UL << sq;
            else board.BOccupied |= 1UL << sq;

            sq++;
        }

        // 2. ACTIVE COLOR
        // which color's turn is it
        switch (tokens[3]) {

            // white
            case "w": color = Color.WHITE; break;

            // black
            case "b": color = Color.BLACK; break;

            default:  InvalidFENCallback($"invalid side to move in FEN: {tokens[3]}"); return;
        }
        
        board.Color = color;

        // 3. CASTLING RIGHTS
        // if neither side can castle, this is "-". otherwise, we can have up to 4 letters.
        // "k" and "q" marks kingside and queenside castling rights respectively. just to clarify, this has nothing 
        // to do with the legality of castling in the next move, it denotes the castling rights availability.
        for (int i = 0; i < tokens[4].Length; i++) {
            switch (tokens[4][i]) {

                // white kingside
                case 'K': board.CastlingRights |= CastlingRights.K; break;

                // white queenside
                case 'Q': board.CastlingRights |= CastlingRights.Q; break;

                // black kingside
                case 'k': board.CastlingRights |= CastlingRights.k; break;

                // black queenside
                case 'q': board.CastlingRights |= CastlingRights.q; break;

                default: {
                    if (tokens[4][i] == '-') 
                        continue;
                    
                    InvalidFENCallback($"invalid castling availability in FEN: {tokens[4][i]}"); 
                    return;
                }
            }
        }

        // 4. EN PASSANT TARGET SQUARE
        // This is a square over which a pawn has just passed while moving two squares; it is given in algebraic
        // notation. If there is no en passant target square, this field uses the character "-". This is recorded
        // regardless of whether there is a pawn in position to capture en passant. An updated version of the
        // spec has since made it so the target square is only recorded if a legal en passant move is possible but
        // the old version of the standard is the one most commonly used.

        if (tokens[5].Length == 2 && byte.TryParse(tokens[5], out byte enPassantSq))
            board.EnPassantSq = enPassantSq;

        else if (tokens[5].Length == 1 && tokens[5][0] == '-')
            board.EnPassantSq = 64;

        else {
            InvalidFENCallback($"invalid en passant square in FEN: {tokens[5]}");
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
        int moveSeqStart = Array.IndexOf(tokens, "moves");

        // we save the previous positions as 3-fold repetition exists
        HistoryPositions.Add(Zobrist.GetHash(board));

        List<string> sequence = [];

        // if the sequence exists
        if (moveSeqStart != -1) {

            // play the sequence of moves
            for (int i = moveSeqStart + 1; i < tokens.Length; i++) {
                sequence.Add(tokens[i]);

                if (!Move.IsCorrectFormat(tokens[i])) {
                    InvalidFENCallback($"invalid move: {tokens[i]}");
                    return;
                }

                board.PlayMove(Move.FromString(board, tokens[i]));
                HistoryPositions.Add(Zobrist.GetHash(board));

                // switch the engine's color
                color = color == Color.WHITE 
                    ? Color.BLACK 
                    : Color.WHITE;
            }
        }

        // try to save a book move
        OpeningBook.SaveSequence([.. sequence], tokens[2]);

        // save drawing positions in "draws"
        List3FoldDraws();

        //board.Hash = Zobrist.GetHash(board);
        board.Print();
    }

    // save all positions that would cause a 3-fold repetition draw in
    // the next move. all previous positions are saved in HistoryPositions
    // and those which occur twice (or more) are considered as drawing.
    private static void List3FoldDraws() {
        Dictionary<ulong, int> occurences = [];

        foreach (var hash in HistoryPositions) {

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

#pragma warning restore CA1304
#pragma warning restore IDE0079