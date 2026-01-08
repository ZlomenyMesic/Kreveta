//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

// Specify CultureInfo
#pragma warning disable CA1304

using Kreveta.consts;
using Kreveta.evaluation;
using Kreveta.movegen;
using Kreveta.nnue;
using Kreveta.search;
using Kreveta.search.transpositions;
using Kreveta.uci;

using System;

// ReSharper disable InconsistentNaming

namespace Kreveta;

internal static class Game {

    // current chessboard state is saved here (root node)
    internal static Board Board = new();

    // the side the engine plays on
    internal static Color EngineColor;

    // if the engine receives the "ucinewgame" command, we know we will be
    // playing a whole game rather than just analyzing a single position.
    internal static bool FullGame;
    
    // the score from the previous turn - applied when playing a full game
    internal static int  PreviousScore;

    private static void InvalidFENCallback(string context) {
        // reset the board
        Board       = Board.CreateStartpos();
        EngineColor = Color.WHITE;
        
        UCI.Log($"Invalid position - {context}");
    }

    internal static void SetStartpos(ReadOnlySpan<string> tokens) {
        Board       = Board.CreateStartpos();
        EngineColor = Color.WHITE;
        ThreeFold.Clear();
        
        PlayMoves(tokens);
    }

    // sets the current position using a tokenized fen string. this function is
    // also called when setting up the starting position, but with the startpos fen
    internal static void SetPosFEN(ReadOnlySpan<string> tokens) {
        // since the whole "position" command is sent, the first two tokens shall be skipped ("position fen")

        // if something is missing, we return right away instead of wasting time
        if (tokens[1] == "fen" && tokens.Length < 6) {
            InvalidFENCallback("some tokens are missing - position, side to move, castling rights and en passant square must be included");
            return;
        }

        // clear the board from previous game/search
        Board = new Board();
        ThreeFold.Clear();
        
        // the first token is the actual position. all ranks are separated by a "/". between the
        // slashes, pieces may be denoted with the simple "pnbrqk" or the uppercase variants for
        // white. empty squares between pieces are marked by a single digit (1-8)
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

                InvalidFENCallback($"invalid character in position: {c}");
                return;
            }

            // color (white = uppercase, black = lowercase)
            Color col = char.IsUpper(c)
                ? Color.WHITE
                : Color.BLACK;

            // piece type (0-5)
            int piece = Consts.Pieces.IndexOf(char.ToLower(c), StringComparison.Ordinal);

            // add the piece to the board
            Board.Pieces[(byte)col * 6 + piece] |= 1UL << sq;

            if (col == Color.WHITE) Board.WOccupied |= 1UL << sq;
            else                    Board.BOccupied |= 1UL << sq;

            sq++;
        }

        int pieceCount = (int)ulong.PopCount(Board.Occupied);
        if (pieceCount is > 32 or < 2) {
            InvalidFENCallback($"illegal number of pieces: {pieceCount}");
            return;
        }

        // the second token tells us, which color's turn it is. "w" means white,
        // "b" means black. the actual color to play may be still modified by the
        // moves played from this position, though
        switch (tokens[3]) {

            // white
            case "w": EngineColor = Color.WHITE; break;

            // black
            case "b": EngineColor = Color.BLACK; break;

            default: InvalidFENCallback($"invalid side to move: \"{tokens[3]}\""); return;
        }

        Board.SideToMove = EngineColor;

        // the third token marks, which sides still have their castling rights. if neither
        // side can castle, this is a dash. otherwise, the characters may be "k" or "q" for
        // kingside and queenside castling respectively, or once again uppercase for white.
        // just to clarify, this has nothing to do with the legality of castling in the next
        // move, it denotes the castling rights availability.
        for (int i = 0; i < tokens[4].Length; i++) {
            switch (tokens[4][i]) {

                // white kingside
                case 'K': Board.CastRights |= CastRights.K; break;

                // white queenside
                case 'Q': Board.CastRights |= CastRights.Q; break;

                // black kingside
                case 'k': Board.CastRights |= CastRights.k; break;

                // black queenside
                case 'q': Board.CastRights |= CastRights.q; break;

                default: {
                        if (tokens[4][i] == '-')
                            continue;

                        InvalidFENCallback($"invalid castling availability: \"{tokens[4][i]}\"");
                        return;
                    }
            }
        }

        // the fourth token is the en passant square, which is the square over which
        // a double-pushing pawn has passed in the previous move, regardless of whether
        // there is another pawn to capture en passant. if no pawn double-pushed, this
        // is also simply a dash.
        if (tokens[5].Length == 2 && byte.TryParse(tokens[5], out byte enPassantSq))
            Board.EnPassantSq = enPassantSq;
        
        else if (tokens[5].Length == 2) {
            int sq = (8 - (tokens[5][1] - '0')) * 8 + Consts.Files.IndexOf(tokens[5][0], StringComparison.Ordinal);
            Board.EnPassantSq = (byte)sq;
        }

        else if (tokens[5].Length == 1 && tokens[5][0] == '-')
            Board.EnPassantSq = 64;
        
        // the fifth token is the halfmove clock - how many quiet half moves have
        // happened in a row already. this is used to check for 50 move rule draw.
        // there is sometimes also a full move clock, which just counts moves from
        // the beginning of the game, but that is useless
        if (tokens.Length >= 7 && byte.TryParse(tokens[6], out byte halfmoveClock))
            Board.HalfMoveClock = halfmoveClock;

        else if (tokens.Length != 6) {
            InvalidFENCallback($"invalid halfmove clock: \"{tokens[5]}\"");
            return;
        }

        Board.NNUEEval   = new NNUEEvaluator(in Board);
        Board.StaticEval = Eval.StaticEval(in Board);
        Board.IsCheck    = Check.IsKingChecked(in Board, EngineColor);
        Board.Hash       = ZobristHash.Hash(in Board);

        // the fen string can be followed by a sequence of moves, which have
        // been played from the position. for example, most GUIs would pass
        // a position like "position startpos moves e2e4 e7e5 g1f3"
        PlayMoves(tokens);
    }

    private static void PlayMoves(ReadOnlySpan<string> tokens) {
        // ReSharper disable once InvokeAsExtensionMethod
        int moveSeqStart = MemoryExtensions.IndexOf(tokens, "moves");

        // no move sequence was found
        if (moveSeqStart == -1) return;

        // we save all known previous positions as 3-fold repetition exists
        ThreeFold.AddAndCheck(Board.Hash);

        // play the sequence of moves
        for (int i = moveSeqStart + 1; i < tokens.Length; i++) {
            var move = tokens[i].ToMove(Board);
            
            if (!Move.IsCorrectFormat(tokens[i]) || Board.PieceAt(move.Start) == PType.NONE) {
                InvalidFENCallback($"invalid move: \"{tokens[i]}\"");
                return;
            }

            Board.PlayMove(move, true);
            ThreeFold.AddAndCheck(Board.Hash);

            // switch the engine's color
            EngineColor = EngineColor == Color.WHITE
                ? Color.BLACK
                : Color.WHITE;
        }
    }
    
    // sometimes, the user might try to search a position that's either illegal,
    // or already decided, so we must check for these cases to prevent crashes
    internal static bool IsTerminalPosition(out string error) {

        // either of the kings is missing (this needs to be evaluated first, because
        // everything else stands on top of the assumption that both kings are present)
        byte wKings = (byte)ulong.PopCount(Board.Pieces[(byte)PType.KING]);
        byte bKings = (byte)ulong.PopCount(Board.Pieces[6 + (byte)PType.KING]);
        
        if (wKings != 1) {
            error = $"{wKings} white kings on the board";
            return true;
        }
        
        if (bKings != 1) {  
            error = $"{bKings} black kings on the board";
            return true;
        }

        // no legal moves for the engine in this position
        if (Movegen.GetLegalMoves(ref Board, stackalloc Move[Consts.MoveBufferSize]) == 0) {
            error = Check.IsKingChecked(Board, EngineColor)
            
                // if we are in check and have no legal moves, that means
                // we are already checkmated and thus cannot search anything
                ? "the engine is checkmated"
                
                // otherwise we are stalemated and also cannot search
                : "the engine is stalemated";
            
            return true;
        }
        
        // if the opposite side is in check, even though it's our turn to play,
        // the position is obviously illegal. as much as i would love to allow
        // capturing the king, in certain positions the engine crashes :(
        if (Check.IsKingChecked(Board, EngineColor == Color.WHITE ? Color.BLACK : Color.WHITE)) {
            error = "the opponent is in check";
            return true;
        }
        
        error = string.Empty;
        return false;
    }

    // in order to mimic rational human play, recaptures are taken into account. if in one
    // search the pv is our best move followed by two captures on the same square, it means
    // we expect our opponent to capture one of our pieces, and we immediately recapture the
    // aggressor. if such a scenario is found, the recapture is stored, and if the opponent
    // indeed plays as we expected, we skip search and play it instantly.
    private static ulong _recaptPosHash;
    private static Move  _recapture;
    
    // this evaluates the pv and if all succeeds, the recapture is stored
    internal static void TryStoreRecapture(Move[] pv, int depth) {
        _recaptPosHash = 0UL;
        _recapture     = default;
        
        if (depth < 8                       // don't trust low depths too much
            || !FullGame                    // don't attempt any recaptures when not playing a full game
            || pv.Length < 3                // the pv isn't even long enough
            || pv[1].Capture == PType.NONE  // either of the moves isn't a capture
            || pv[2].Capture == PType.NONE
            || pv[1].End != pv[2].End)      // the captures don't land on the same square (not a recapture)
            return;
        
        // play the two moves
        var temp = Board.Clone();
        temp.PlayMove(pv[0], false);
        temp.PlayMove(pv[1], false);
        
        // store the recapture, and the position from which it is played
        _recaptPosHash = temp.Hash;
        _recapture     = pv[2];
    }

    // try to get the recapture once in a new search
    internal static Move TryGetRecapture() {
        if (!FullGame || _recapture == default)
            return default;
        
        // we must make sure that the opponent actually played the capture
        return Board.Hash == _recaptPosHash 
            ? _recapture : default;
    }
}

#pragma warning restore CA1304