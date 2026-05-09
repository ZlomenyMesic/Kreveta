//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

// Remove unnecessary suppression
#pragma warning disable IDE0079

// Specify CultureInfo
#pragma warning disable CA1304
#pragma warning disable CA1305

using Kreveta.consts;
using Kreveta.evaluation;
using Kreveta.movegen;
using Kreveta.nnue;
using Kreveta.search.transpositions;
using Kreveta.uci;

using System;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Kreveta.polyglot;

// ReSharper disable InconsistentNaming

namespace Kreveta;

// can be neither readonly nor record
[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal unsafe struct Board {

    // all pieces are stored in so-called bitboards. each piece-color combination
    // has its own bitboard, where ones represent the individual pieces. movegen
    // and other piece manipulation is significantly faster than using a mailbox
    internal ulong[] Pieces = new ulong[12];

    // additional bitboards storing all pieces of a certain color. this turns out
    // to be slightly faster than ORing six of the bitboards above repetitively
    internal ulong WOccupied;
    internal ulong BOccupied;

    // all occupied squares
    internal readonly ulong Occupied {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => WOccupied | BOccupied;
    }

    // all empty squares (bitwise inverse of occupied)
    private readonly ulong Empty {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ~Occupied;
    }

    // square over which a double pushing pawn has passed
    // one move ago. 64 if no en passant square is present
    internal byte EnPassantSq = 64;

    // the current state of castling rights
    internal CastRights CastRights = CastRights.NONE;

    // the side to move
    internal Color SideToMove = Color.NONE;

    // number of moves played that weren't pawn pushes or captures
    internal byte HalfMoveClock = 0;

    // the NNUE evaluator, which stores both accumulators
    internal NNUEEvaluator NNUEEval;
    
    // the static evaluation, hash and check flag of the current position. since
    // these values are used often, it's better than recomputing them each time
    internal short StaticEval = 0;
    internal ulong Hash       = 0UL;
    internal bool  IsCheck    = false;

    public Board() {
        Pieces   = new ulong[12];
        NNUEEval = null!;
    }
    
    // returns the piece at a certain square. this method is really slow,
    // but it's only used in places where it helps readability and doesn't
    // affect overall performance
    [Pure] internal PType PieceAt(int index) {
        ulong sq = 1UL << index;

        // empty square, return immediately
        if ((Empty & sq) != 0UL)
            return PType.NONE;

        // now we loop the piece types and check whether the square is occupied by any side
        for (int i = 0; i < 6; i++) {
            if (((Pieces[i] | Pieces[6 + i]) & sq) != 0UL)
                return (PType)i;
        }

        // we shouldn't ever get here
        return PType.NONE;
    }
    
    // performs a move on the board
    internal void PlayMove(Move move, bool updateStaticEval) {
        
        // the zobrist hash is not recomputed each move. since the changes
        // are small, all differences are applied directly in this method
        Hash ^= EnPassantSq != 64 ? ZobristHash.EnPassant[EnPassantSq & 7] : 0UL;
        Hash ^=                     ZobristHash.WhiteToMove;
        Hash ^=                     ZobristHash.Castling[(int)CastRights];
        
        // save time by storing piece hashes like this
        ReadOnlySpan<ulong> pieceHashes = ZobristHash.Pieces;
        int hashColStride = SideToMove == Color.WHITE ? 6 : 0;
        
        EnPassantSq = 64;

        // switch the side to move
        Color col    = SideToMove;
        Color colOpp = 1 - col;
        SideToMove  ^= (Color)1;

        // start and end squares
        byte start8 = (byte)move.Start;
        byte end8   = (byte)move.End;

        // start and end squares represented as bitboards
        ulong start = 1UL << start8;
        ulong end   = 1UL << end8;

        // other stuff
        PType prom  = move.Promotion;
        PType piece = move.Piece;
        PType capt  = move.Capture;

        // remove starting piece from hash
        Hash ^= pieceHashes[start8 * 12 + (int)piece + hashColStride];

        // reset the half move clock
        if (piece == PType.PAWN || capt != PType.NONE)
            HalfMoveClock = 0;
        else HalfMoveClock++;

        // en passant
        if (prom == PType.PAWN) {

            // the pawn that is to be captured
            ulong captureSq = col == Color.WHITE
                ? end << 8
                : end >> 8;

            // xor the captured pawn and move our pawn
            Pieces[(byte)colOpp * 6] ^= captureSq;
            Pieces[(byte)col    * 6] ^= start | end;

            // remove the captured piece and add the new pawn
            Hash ^= pieceHashes[BB.LS1B(captureSq) * 12 + 6 - hashColStride];
            Hash ^= pieceHashes[end8               * 12     + hashColStride];

            if (col == Color.WHITE) {
                WOccupied ^= start | end;
                BOccupied ^= captureSq;
            } else {
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
                _ => 0UL
            };

            // king and rook
            Pieces[(byte)col * 6 + (byte)PType.KING] ^= start | end;
            Pieces[(byte)col * 6 + (byte)PType.ROOK] ^= rook;

            if (col == Color.WHITE) WOccupied ^= rook | start | end;
            else                    BOccupied ^= rook | start | end;
            
            Hash ^= pieceHashes[end8 * 12 + 5 + hashColStride];
            
            // the rook must be both removed and added, thus we need to reset the first bit
            Hash ^= pieceHashes[BB.LS1BReset(ref rook) * 12 + 3 + hashColStride];
            Hash ^= pieceHashes[BB.LS1B(rook)          * 12 + 3 + hashColStride];
        }

        // regular promotion
        else if (prom != PType.NONE) {
            Pieces[(byte)col * 6 + (byte)piece] ^= start;
            Pieces[(byte)col * 6 + (byte)prom]  ^= end;

            if (col == Color.WHITE) WOccupied ^= start | end;
            else                    BOccupied ^= start | end;

            // promotion piece is handled separately
            Hash ^= pieceHashes[end8 * 12 + (int)prom + hashColStride];
        }

        // regular move
        else {
            Pieces[(byte)col * 6 + (byte)piece] ^= start | end;
            Hash ^= pieceHashes[end8 * 12 + (int)piece + hashColStride];

            // if we double pushed a pawn, set the en passant square
            if (piece == PType.PAWN && (col == Color.WHITE
                    ? start >> 16 == end
                    : start << 16 == end)) {

                // en passant square is the square over which the
                // pawn has double pushed, not the capture square
                EnPassantSq = BB.LS1B(col == Color.WHITE
                    ? start >> 8
                    : start << 8);

                // hash the new en passant square
                Hash ^= ZobristHash.EnPassant[EnPassantSq & 7];
            }

            if (col == Color.WHITE) WOccupied ^= start | end;
            else                    BOccupied ^= start | end;
        }

        // capture
        if (capt != PType.NONE) {
            Pieces[(byte)colOpp * 6 + (byte)capt] ^= end;
            Hash ^= pieceHashes[end8 * 12 + (int)capt + 6 - hashColStride];

            if (col == Color.WHITE) BOccupied ^= end;
            else                    WOccupied ^= end;
        }

        if (CastRights != CastRights.NONE && piece == PType.KING) {
            
            // remove castling rights after a king moves
            CastRights &= (CastRights)(col == Color.WHITE
                ? 0xC   // all except KQ
                : 0x3); // all except kq
        }

        // moving and capturing rooks must be separated
        if (CastRights != CastRights.NONE && piece == PType.ROOK) {
            byte mask = start8 switch {
                63 => 0xE, // all except K
                56 => 0xD, // all except Q
                7  => 0xB, // all except k
                0  => 0x7, // all except q
                _  => 0xF
            };

            // remove castling rights after a rook moves
            CastRights &= (CastRights)mask;
        }

        if (CastRights != CastRights.NONE && capt == PType.ROOK) {
            byte mask = end8 switch {
                63 => 0xE, // all except K
                56 => 0xD, // all except Q
                7  => 0xB, // all except k
                0  => 0x7, // all except q
                _  => 0xF
            };

            // remove castling rights after a rook moves
            CastRights &= (CastRights)mask;
        }

        Hash ^= ZobristHash.Castling[(int)CastRights];
        
        // the check flag is used in perft as well, so no harm is done
        IsCheck = Check.IsKingChecked(in this, colOpp);
        
        // in some cases, such as Perft, the static eval is useless, and
        // would only slow the engine down.
        if (updateStaticEval) {
            
            // don't compute or look up any static evaluation when in check
            if (IsCheck) {
                NNUEEval.Update(in this, move, col, false);
                StaticEval *= -1;
            }
            
            // otherwise update the accumulators as usual, and compute static evaluation.
            // we also use SETT as a transposition table solely for static evals. if the
            // position is found, we skip evaluation, and use the stored score
            else {
                bool foundEval = SETT.TryGetEval(Hash, out short eval);
                NNUEEval.Update(in this, move, col, !foundEval);
            
                StaticEval = foundEval 
                    ? eval : Eval.StaticEval(in this);
            
                // if not yet stored, store it
                if (!foundEval)
                    SETT.Store(Hash, StaticEval);
            }
        }
    }

    private void PlayReversibleMove(Move move) {
        Color col = SideToMove;
        
        // start & end squares
        ulong start = 1UL << move.Start;
        ulong end   = 1UL << move.End;

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

            Pieces[(byte)colOpp * 6] ^= captureSq;
            Pieces[(byte)col    * 6] ^= start | end;
            
            if (col == Color.WHITE) BOccupied ^= captureSq;
            else                    WOccupied ^= captureSq;
        }
        
        else if (prom == PType.KING) {
            // get the rook move respective to the king move
            ulong rook = end switch {
                0x0000000000000004 => 0x0000000000000009, // q
                0x0000000000000040 => 0x00000000000000A0, // k
                0x0400000000000000 => 0x0900000000000000, // Q
                0x4000000000000000 => 0xA000000000000000, // K
                _                  => 0UL
            };

            // move king (MISSING in original)
            Pieces[(byte)col * 6 + (byte)PType.KING] ^= start | end;

            // rook
            Pieces[(byte)col * 6 + (byte)PType.ROOK] ^= rook;

            // update rook occupancy now; king occupancy will be toggled
            // by the final occupancy XOR that happens after this branch
            if (col == Color.WHITE) WOccupied ^= rook;
            else                    BOccupied ^= rook;
        }

        // promotion
        else if (prom != PType.NONE) {
            Pieces[(byte)col * 6 + (byte)piece] ^= start;
            Pieces[(byte)col * 6 + (byte)prom]  ^= end;
        }

        // regular move
        else Pieces[(byte)col * 6 + (byte)piece] ^= start | end;

        // capture
        if (capt != PType.NONE) {
            Pieces[(byte)colOpp * 6 + (byte)capt] ^= end;

            if (colOpp == Color.WHITE) WOccupied ^= end;
            else                       BOccupied ^= end;
        }

        if (col == Color.WHITE) WOccupied ^= start | end;
        else                    BOccupied ^= start | end;
    }
    
    // checks whether a move is legal from this position. this is done by using
    // the reversible XOR-only play move function, which turns out to be faster
    // than cloning this board and playing the move regularly
    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool IsMoveLegal(Move move, Color col) {
        PlayReversibleMove(move);
        bool isLegal = !Check.IsKingChecked(in this, col);
        PlayReversibleMove(move);
        
        return isLegal;
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int GamePhase() {
        // calculates the current game phase based on piece count and type.
        // the value ranges between 0 and 70, with 0 signifying absolute
        // endgames and 70 being the initial opening state
        ulong phase = ulong.PopCount(Pieces[0] | Pieces[6])
                + 3 * ulong.PopCount(Pieces[1] | Pieces[2] | Pieces[7] | Pieces[8])
                + 5 * ulong.PopCount(Pieces[3] | Pieces[9])
                + 9 * ulong.PopCount(Pieces[4] | Pieces[10]);

        return Math.Max(0, (int)phase - 8);
    }
    
    // does this side have any pieces except pawns and the king?
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Pure] internal bool HasNonPawnMaterial(Color col)
        => ((col == Color.WHITE ? WOccupied : BOccupied) 
            ^ (Pieces[    (int)col * 6] // exclude pawns
             | Pieces[5 + (int)col * 6] // exclude the king
           )) != 0UL;
    
    // does this side have any pieces that are able to pin another piece
    // to the opposite king? this includes bishops, rooks, and queens
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Pure] internal bool HasPinningMaterial(Color col)
        => (Pieces[2 + (int)col * 6] // bishops
          | Pieces[3 + (int)col * 6] // rooks
          | Pieces[4 + (int)col * 6] // queens
           ) != 0UL;
    
    // clone the board; when searching, we always clone the current board, play available moves
    // on these "children", and pass them down the tree. commonly, this is handled by implementing
    // an "undo move" method, allowing us to work with just a single board copy, but i had to be
    // different. furthermore, to ensure we don't have to allocate new memory for NNUE accumulators,
    // it is good to pass the current ply as well, so the memory pool can be accessed
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Pure] internal Board Clone(int childPly = -1) {
        
        var newPieces = new ulong[12];
        Unsafe.CopyBlockUnaligned(
            destination: ref Unsafe.As<ulong, byte>(ref newPieces[0]),
            source:      ref Unsafe.As<ulong, byte>(ref Pieces[0]),
            byteCount:   96); // 12 * sizeof(ulong)
        
        return this with {
            Pieces   = newPieces,
            NNUEEval = childPly >= 0
            
                // try to get pre-allocated accumulators from the pool
                ? NNUEEvaluator.GetFromPool(in NNUEEval, childPly)
                : new NNUEEvaluator(in NNUEEval)
        };
    }
    
    // used for Perft, clones the board the same way as the method
    // above, but leaves out accumulator copies and pool lookups
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [Pure] internal Board CloneNoNNUE() {
        var newPieces = new ulong[12];
        Unsafe.CopyBlockUnaligned(
            destination: ref Unsafe.As<ulong, byte>(ref newPieces[0]),
            source:      ref Unsafe.As<ulong, byte>(ref Pieces[0]),
            byteCount:   96);
        
        return this with {
            Pieces   = newPieces,
            NNUEEval = null!
        };
    }

    internal void Print() {
        char[] chars = new char[64];
        Array.Fill(chars, ' ');
        
        // first, draw the file letters (a-h) at the top. rank numbers are
        // also printed, though that happens during the actual main loop
        UCI.Output.Write("\n " + RGB.Blue + "+ ");
        foreach (char file in Consts.Files)
            UCI.Output.Write($"{file} ");

        // loop all piece types
        for (int i = 0; i < 6; i++) {
            ulong wCopy = Pieces[i];
            ulong bCopy = Pieces[6 + i];

            while (wCopy != 0UL) {
                int sq = BB.LS1BReset(ref wCopy);

                // pieces are uppercase for white
                chars[sq] = char.ToUpper(Consts.Pieces[i]);
            }

            while (bCopy != 0UL) {
                int sq = BB.LS1BReset(ref bCopy);
                chars[sq] = Consts.Pieces[i];
            }
        }
        
        // next actually print the characters to console
        for (int i = 0; i <= 64; i++) {
            int rank = i >> 3;
            
            if ((i & 7) == 0) {
                UCI.Output.Write(RGB.Reset);
                    
                if      (rank == 5) UCI.Output.Write($"  FEN string:     {Game.Board.FEN()}");
                else if (rank == 6) UCI.Output.Write($"  TT hash:        {Game.Board.Hash}");
                else if (rank == 7) UCI.Output.Write($"  Polyglot hash:  {PolyglotZobristHash.Hash(in Game.Board)}");
                else if (rank == 8) UCI.Output.Write($"  Is check:       {(Game.Board.IsCheck ? RGB.Green : RGB.Red)}{Game.Board.IsCheck}{RGB.Reset}");

                if (rank == 8) break;
                
                // newline character at the end of each rank, and rank numbers
                UCI.Output.Write($"\n {RGB.Blue}{8 - rank}{RGB.Reset} ");
            }
            
            // add color to the squares themselves
            UCI.Output.Write(
                (i & 1) == ((i >> 3) & 1)
                ? RGB.BGLight : RGB.BGDark
            );
            
            // to make things more pretty, the pieces have color too
            UCI.Output.Write(chars[i] switch {
                var c when char.IsAsciiLetterUpper(c) => RGB.White,
                var c when char.IsAsciiLetterLower(c) => RGB.Black,
                _                                     => string.Empty
            });
            
            UCI.Output.Write($"\e[1m{chars[i]} {RGB.Reset}");
        }
        
        // make sure we reset all color at the end
        UCI.Output.WriteLine($"\n{RGB.Reset}");
    }

    // returns the FEN (Forsyth-Edward's Notation) of this position, as can probably
    // be infered from the method's name. the FEN is a superior notation, allowing
    // us to encode any position quite easily. for a more detailed explanation, take
    // a look into Game.cs, where FEN strings are described in-depth
    internal string FEN() {
        var fen = new StringBuilder();

        int   curEmpty = 0;
        for (int i = 0; i < 64; i++) {
            PType piece;
            
            // all ranks must be separated with a slash '/'
            if ((i & 7) == 0 && i != 0) {
                
                // ensure we don't miss the previous empty squares
                if (curEmpty != 0) {
                    fen.Append(curEmpty);
                    curEmpty = 0;
                }
                fen.Append('/');
            }

            if ((piece = PieceAt(i)) != PType.NONE) {
                char p = Consts.Pieces[(int)piece];
                
                // piece characters must be uppercase for white
                if ((WOccupied & 1UL << i) != 0UL)
                    p = char.ToUpper(p);

                // if we're following a number of empty squares, add them
                if (curEmpty != 0) {
                    fen.Append(curEmpty);
                    curEmpty = 0;
                }
                
                // only after that actually add the piece
                fen.Append(p);
            }
            
            else curEmpty++;
        }

        if (curEmpty != 0)
            fen.Append(curEmpty);

        // side to move (either w or b)
        fen.Append(SideToMove == Color.WHITE ? " w " : " b ");
        
        // castling rights
        if (CastRights == 0)
            fen.Append('-');
        else {
            int cr = (int)CastRights;
            if ((cr & 1) != 0) fen.Append('K'); // white kingside
            if ((cr & 2) != 0) fen.Append('Q'); // white queenside
            if ((cr & 4) != 0) fen.Append('k'); // black kingside
            if ((cr & 8) != 0) fen.Append('q'); // black queenside
        }

        // en passant square; the square must be written in the common "e4" notation.
        // however, since our board indexing is reversed, the rank must be reversed, too
        if (EnPassantSq != 64)
            fen.Append(" " + Consts.Files[EnPassantSq & 7] + (8 - (EnPassantSq >> 3)));
        else fen.Append(" -");
        
        // halfmove and fullmove clock
        fen.Append($" {HalfMoveClock}");
        fen.Append($" {Game.Ply}");
        
        return fen.ToString();
    }

    internal static Board CreateStartpos() {
        var board = new Board {
            WOccupied     = 0xFFFF000000000000UL,
            BOccupied     = 0x000000000000FFFFUL,
            
            EnPassantSq   = 64,
            CastRights    = CastRights.ALL,
            SideToMove    = Color.WHITE,
            HalfMoveClock = 0,
            
            Pieces = {
                [0] =  0x00FF000000000000UL, // P
                [1] =  0x4200000000000000UL, // N
                [2] =  0x2400000000000000UL, // B
                [3] =  0x8100000000000000UL, // R
                [4] =  0x0800000000000000UL, // Q
                [5] =  0x1000000000000000UL, // K
                
                [6] =  0x000000000000FF00UL, // p
                [7] =  0x0000000000000042UL, // n
                [8] =  0x0000000000000024UL, // b
                [9] =  0x0000000000000081UL, // r
                [10] = 0x0000000000000008UL, // q
                [11] = 0x0000000000000010UL  // k
            }
        };
        
        board.Hash       = ZobristHash.GetHash(in board);
        board.IsCheck    = false;
        board.NNUEEval   = new NNUEEvaluator(in board);
        board.StaticEval = Eval.StaticEval(in board);
        
        return board;
    }
}

#pragma warning restore CA1304
#pragma warning restore CA1305
#pragma warning restore IDE0079