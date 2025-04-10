//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.movegen.pieces;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Kreveta.evaluation;

internal static class Eval {

    private const ulong Center = 0x00007E7E7E7E0000;

    private const int MateScoreThreshold = 9000;
    private const int MateScoreDefault = 9999;

    private const int SideToMoveBonus = 5;

    private const int DoubledPawnPenalty = -6;
    private const int IsolatedPawnPenalty = -21;

    private const int BishopPairBonus = 35;

    private const int OpenFileRookBonus = 24;

    private static readonly Random r = new();

    private static readonly ulong[] AdjFiles = new ulong[8];

    static Eval() {

        // adjacent files for isolated pawn eval
        for (int i = 0; i < 8; i++) {
            AdjFiles[i] = Consts.RelevantFileMask[i]
                | (i != 0 ? Consts.RelevantFileMask[i - 1] : 0)
                | (i != 7 ? Consts.RelevantFileMask[i + 1] : 0);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static short GetMateScore(Color col, int ply)
        => (short)((col == Color.WHITE ? -1 : 1) * (MateScoreDefault - ply));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsMateScore(int s)
        => Math.Abs(s) > MateScoreThreshold;

    internal static short StaticEval([NotNull] in Board board) {

        ulong wOccupied = board.WOccupied;
        ulong bOccupied = board.BOccupied;

        int pieceCount = BB.Popcount(wOccupied | bOccupied);

        short wEval = 0, bEval = 0;

        for (int i = 0; i < 6; i++) {

            ulong wCopy = board.Pieces[(byte)Color.WHITE, i];
            ulong bCopy = board.Pieces[(byte)Color.BLACK, i];

            while (wCopy != 0) {
                int sq = BB.LS1BReset(ref wCopy);
                wEval += GetTableValue((PType)i, Color.WHITE, sq, pieceCount);
            }

            while (bCopy != 0) {
                int sq = BB.LS1BReset(ref bCopy);
                bEval += GetTableValue((PType)i, Color.BLACK, sq, pieceCount);
            }
        }

        short eval = (short)(wEval - bEval);

        // pawn structure eval includes:
        // 
        // 1. doubled (or tripled) pawns penalty
        // 2. isolated pawn penalty
        //
        eval += PawnStructureEval(board.Pieces[(byte)Color.WHITE, (byte)PType.PAWN], Color.WHITE);
        eval -= PawnStructureEval(board.Pieces[(byte)Color.BLACK, (byte)PType.PAWN], Color.BLACK);

        // knight eval includes:
        //
        // 1. decreasing value in the endgame
        //
        eval += KnightEval(board, pieceCount);

        // bishop eval includes:
        //
        // 1. bishop pair bonus
        //
        eval += BishopEval(board);

        // rook eval includes:
        //
        // 1. increasing value in the endgame
        // 2. bonuses for rooks on open or semi-open files
        //
        eval += RookEval(board, pieceCount);

        // king eval includes:
        //
        // 1. friendly pieces protecting the king
        //
        eval += KingEval(board, pieceCount);

        // side to move should also get a slight advantage
        eval += (short)(board.color == Color.WHITE ? SideToMoveBonus : -SideToMoveBonus);

        return (short)(eval/* + r.Next(-6, 6)*/);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static short GetTableValue(PType type, Color col, int sq, int pieceCount) {
        // this method uses the value tables in EvalTables.cs, and is used to evaluate a piece position
        // there are two tables - midgame and endgame, this is important, because the pieces should be
        // in different positions as the game progresses (e.g. a king in the midgame should be in the corner,
        // but in the endgame in the center)

        int i = ((byte)type * 64) + (col == Color.WHITE
            ? (63 - sq) 
            : (sq >> 3) * 8 + (7 - (sq & 7)));

        int mg_value = EvalTables.Midgame[i];
        int eg_value = EvalTables.Endgame[i];

        return (short)(mg_value * pieceCount / 32 + eg_value * (32 - pieceCount) / 32);
    }

    // bonuses or penalties for pawn structure
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static short PawnStructureEval(ulong p, Color col) {

        int eval = 0;

        int colMult = col == Color.WHITE ? 1 : -1;

        for (int i = 0; i < 8; i++) {
            ulong file = Consts.RelevantFileMask[i];

            // count the number of pawns on the file
            int file_occ = BB.Popcount(file & p);
            if (file_occ == 0) continue;

            // penalize doubled pawns
            eval += (file_occ - 1) * DoubledPawnPenalty * colMult;

            // current file + files on the sides
            ulong adj = AdjFiles[i];

            // if the number of pawns on current file is equal to the number of pawns
            // on the current plus adjacent files, we know the pawn/s are isolated
            int adj_occ = BB.Popcount(adj & p);
            eval += file_occ != adj_occ ? 0 : IsolatedPawnPenalty * colMult;
        }

        return (short)eval;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static short KnightEval([NotNull] in Board board, int pieceCount) {
        short eval = 0;

        // knights are less valuable if be have fewer pieces on the board.
        // number of white knights and black knights on the board:
        int wKnights = BB.Popcount(board.Pieces[0, 1]);
        int bKnights = BB.Popcount(board.Pieces[1, 1]);

        // subtract some eval for white if it has knights
        eval -= (short)(wKnights * (32 - pieceCount) / 4);

        // add some eval for black it has knights
        eval += (short)(bKnights * (32 - pieceCount) / 4);

        return eval;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static short BishopEval([NotNull] in Board board) {

        short eval = 0;

        // accidental bishop pairs may appear in the endgame - a side can
        // have two bishops, but of the same color, so it isn't really
        // a bishop pair. this error should, however, be rare and inconsequential

        // i did some testing with checking the colors of the bishops and it
        // slows down the eval quite a lot, that's why it isn't implemented

        // does white have two (or more) bishops?
        eval += (short)(BB.Popcount(board.Pieces[0, 2]) > 1 ? BishopPairBonus : 0);

        // does black have two (or more) bishops?
        eval -= (short)(BB.Popcount(board.Pieces[1, 2]) > 1 ? BishopPairBonus : 0);

        return eval;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static short RookEval([NotNull] in Board board, int pieceCount) {
        short eval = 0;

        // rooks are, as opposed to knights, more valuable if be have fewer pieces on the board.
        // number of white rooks and black rooks on the board:
        int wRooks = BB.Popcount(board.Pieces[0, 3]);
        int bRooks = BB.Popcount(board.Pieces[1, 3]);

        // add some eval for white if it has rooks
        eval += (short)(wRooks * (32 - pieceCount) / 2);

        // subtract some eval for black it has rooks
        eval -= (short)(bRooks * (32 - pieceCount) / 2);

        //for (int i = 0; i < 2; i++) {
        //    ulong copy = b.pieces[i, 3];

        //    while (copy != 0) {
        //        (copy, int sq) = BB.LS1BReset(copy);

        //        // how many pieces (regardless of color) are on the same file as the rook
        //        int file_occ = BB.Popcount(Consts.FileMask[sq & 7] & occ);

        //        // the bonus gets smaller with more pieces on the file
        //        eval += (short)(OPEN_FILE_ROOK_BONUS / file_occ

        //            // more bonus for open files later in the game
        //            * (32 - piece_count) / 8
        //            * (i == 0 ? 1 : -1));
        //    }
        //}

        return eval;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static short QueenEval(ulong q, Color col, int piece_count) {
        int eval = 0;

        //eval += PiecesTableEval(q, 4, col, piece_count);

        return (short)eval;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static short KingEval([NotNull] in Board board, int pieceCount) {
        int eval = 0;

        // same color pieces around the king - protection
        ulong wProtection = King.GetKingTargets(board.Pieces[(byte)Color.WHITE, (byte)PType.KING], board.WOccupied);
        ulong bProtection = King.GetKingTargets(board.Pieces[(byte)Color.BLACK, (byte)PType.KING], board.BOccupied);

        // bonus for the number of friendly pieces protecting the king
        short wProtBonus = (short)(BB.Popcount(wProtection) * 2);
        short bProtBonus = (short)(BB.Popcount(bProtection) * 2);

        eval += wProtBonus - bProtBonus;

        return (short)eval;
    }
}