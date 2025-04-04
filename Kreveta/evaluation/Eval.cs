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

using Kreveta.movegen.pieces;
using System.Runtime.CompilerServices;

namespace Kreveta.evaluation;

internal static class Eval {

    private const ulong CENTER = 0x00007E7E7E7E0000;

    private const int MATE_SCORE_BASE = 9000;
    private const int MATE_SCORE = 9999;

    private const int SIDE_TO_MOVE_BONUS = 5;

    private const int DOUBLED_PAWN_PENALTY = -7;
    private const int ISOLATED_PAWN_PENALTY = -21;

    private const int BISHOP_PAIR_BONUS = 35;

    private const int OPEN_FILE_ROOK_BONUS = 24;

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
        => (short)((col == Color.WHITE ? -1 : 1) * (MATE_SCORE - ply));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsMateScore(int s)
        => Math.Abs(s) > MATE_SCORE_BASE;


    internal static short StaticEval(Board b) {

        ulong w_occ = b.Occupied(Color.WHITE);
        ulong b_occ = b.Occupied(Color.BLACK);

        int piece_count = BB.Popcount(w_occ | b_occ);

        short eval = 0;

        short w_eval = 0, b_eval = 0;

        for (int i = 0; i < 6; i++) {

            ulong w_copy = b.pieces[(byte)Color.WHITE, i];
            ulong b_copy = b.pieces[(byte)Color.BLACK, i];

            while (w_copy != 0) {
                (w_copy, int w_sq) = BB.LS1BReset(w_copy);

                w_eval += GetTableValue((PType)i, Color.WHITE, w_sq, piece_count);
            }

            while (b_copy != 0) {
                (b_copy, int b_sq) = BB.LS1BReset(b_copy);

                b_eval += GetTableValue((PType)i, Color.BLACK, b_sq, piece_count);
            }
        }

        eval = (short)(w_eval - b_eval);

        // pawn structure eval includes:
        // 
        // 1. doubled (or tripled) pawns penalty
        // 2. isolated pawn penalty
        //
        eval += PawnStructureEval(b.pieces[(byte)Color.WHITE, (byte)PType.PAWN], Color.WHITE, piece_count);
        eval -= PawnStructureEval(b.pieces[(byte)Color.BLACK, (byte)PType.PAWN], Color.BLACK, piece_count);

        // knight eval includes:
        //
        // 1. decreasing value in the endgame
        //
        eval += KnightEval(b, piece_count);

        // bishop eval includes:
        //
        // 1. bishop pair bonus
        //
        eval += BishopEval(b);

        // rook eval includes:
        //
        // 1. increasing value in the endgame
        // 2. bonuses for rooks on open or semi-open files
        //
        eval += RookEval(b, piece_count, w_occ | b_occ);

        // king eval includes:
        //
        // 1. friendly pieces protecting the king
        //
        eval += KingEval(b, piece_count);

        // side to move should also get a slight advantage
        eval += (short)(b.color == Color.WHITE ? SIDE_TO_MOVE_BONUS : -SIDE_TO_MOVE_BONUS);

        return (short)(eval/* + r.Next(-6, 6)*/);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static short GetTableValue(PType p, Color col, int pos, int piece_count) {
        // this method uses the value tables in EvalTables.cs, and is used to evaluate a piece position
        // there are two tables - midgame and endgame, this is important, because the pieces should be
        // in different positions as the game progresses (e.g. a king in the midgame should be in the corner,
        // but in the endgame in the center)

        int i = ((byte)p * 64) + (col == Color.WHITE
            ? (63 - pos) 
            : (pos >> 3) * 8 + (7 - (pos & 7)));

        int mg_value = EvalTables.Midgame[i];
        int eg_value = EvalTables.Endgame[i];

        return (short)(mg_value * piece_count / 32 + eg_value * (32 - piece_count) / 32);
    }

    // bonuses or penalties for pawn structure
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static short PawnStructureEval(ulong p, Color col, int piece_count) {

        int eval = 0;

        int colMult = col == Color.WHITE ? 1 : -1;

        for (int i = 0; i < 8; i++) {
            ulong file = Consts.RelevantFileMask[i];

            // count the number of pawns on the file
            int file_occ = BB.Popcount(file & p);
            if (file_occ == 0) continue;

            // penalize doubled pawns
            eval += (file_occ - 1) * DOUBLED_PAWN_PENALTY * colMult;

            // current file + files on the sides
            ulong adj = AdjFiles[i];

            // if the number of pawns on current file is equal to the number of pawns
            // on the current plus adjacent files, we know the pawn/s are isolated
            int adj_occ = BB.Popcount(adj & p);
            eval += file_occ != adj_occ ? 0 : ISOLATED_PAWN_PENALTY * colMult;
        }

        return (short)eval;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static short KnightEval(Board b, int piece_count) {
        short eval = 0;

        // knights are less valuable if be have fewer pieces on the board.
        // number of white knights and black knights on the board:
        int w_knights = BB.Popcount(b.pieces[0, 1]);
        int b_knights = BB.Popcount(b.pieces[1, 1]);

        // subtract some eval for white if it has knights
        eval -= (short)(w_knights * (32 - piece_count) / 4);

        // add some eval for black it has knights
        eval += (short)(b_knights * (32 - piece_count) / 4);

        return eval;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static short BishopEval(Board b) {

        short eval = 0;

        // accidental bishop pairs may appear in the endgame - a side can
        // have two bishops, but of the same color, so it isn't really
        // a bishop pair. this error should, however, be rare and inconsequential

        // i did some testing with checking the colors of the bishops and it
        // slows down the eval quite a lot, that's why it isn't implemented

        // does white have two (or more) bishops?
        eval += (short)(BB.Popcount(b.pieces[0, 2]) > 1 ? BISHOP_PAIR_BONUS : 0);

        // does black have two (or more) bishops?
        eval -= (short)(BB.Popcount(b.pieces[1, 2]) > 1 ? BISHOP_PAIR_BONUS : 0);

        return eval;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static short RookEval(Board b, int piece_count, ulong occ) {
        short eval = 0;

        // rooks are, as opposed to knights, more valuable if be have fewer pieces on the board.
        // number of white rooks and black rooks on the board:
        int w_rooks = BB.Popcount(b.pieces[0, 3]);
        int b_rooks = BB.Popcount(b.pieces[1, 3]);

        // add some eval for white if it has rooks
        eval += (short)(w_rooks * (32 - piece_count) / 2);

        // subtract some eval for black it has rooks
        eval -= (short)(b_rooks * (32 - piece_count) / 2);

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
    private static short KingEval(Board b, int piece_count) {
        int eval = 0;

        // same color pieces around the king - protection
        ulong w_protection = King.GetKingMoves(b.pieces[(byte)Color.WHITE, (byte)PType.KING], b.Occupied(Color.WHITE));
        ulong b_protection = King.GetKingMoves(b.pieces[(byte)Color.BLACK, (byte)PType.KING], b.Occupied(Color.BLACK));

        // bonus for the number of friendly pieces protecting the king
        short w_prot_bonus = (short)(BB.Popcount(w_protection) * 2);
        short b_prot_bonus = (short)(BB.Popcount(b_protection) * 2);

        eval += w_prot_bonus - b_prot_bonus;

        return (short)eval;
    }
}