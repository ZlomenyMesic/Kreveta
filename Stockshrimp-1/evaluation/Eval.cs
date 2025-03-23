/*
*  Stockshrimp chess engine 1.0
*  developed by ZlomenyMesic
*/

using Stockshrimp_1.movegen;
using System.Runtime.CompilerServices;

namespace Stockshrimp_1.evaluation;

internal static class Eval {

    const int MATE_SCORE_BASE = 9000;
    const int MATE_SCORE = 9999;

    const int SIDE_TO_MOVE_BONUS = 5;

    const int DOUBLED_PAWN_PENALTY = -7;
    const int ISOLATED_PAWN_PENALTY = -21;

    const int BISHOP_PAIR_BONUS = 35;

    private static readonly Random r = new();

    private static readonly ulong[] AdjFiles = new ulong[8];

    static Eval() {

        // adjacent files for isolated pawn eval
        for (int i = 0; i < 8; i++) {
            AdjFiles[i] = Consts.FileMask[i]
                | (i != 0 ? Consts.FileMask[i - 1] : 0)
                | (i != 7 ? Consts.FileMask[i + 1] : 0);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static short GetMateScore(int col, int ply)
        => (short)((col == 0 ? -1 : 1) * (MATE_SCORE - ply));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsMateScore(int s)
        => Math.Abs(s) > MATE_SCORE_BASE;


    // save the position of all current pieces as an array
    // [col, piece, square]
    private static readonly byte[,,] pieces = new byte[2, 6, 64];
    internal static short StaticEval(Board b) {

        int piece_count = BB.Popcount(b.Occupied());

        int eval = 0;

        for (int i = 0; i < 2; i++) {
            for (int j = 0; j < 6; j++) {

                ulong copy = b.pieces[i, j];

                if (copy == 0) continue;

                while (copy != 0) {
                    (copy, int sq) = BB.LS1BReset(copy);
                    //pieces[i, j, sq] = 1;

                    eval += GetTableValue(j, i, sq, piece_count) * (i == 0 ? 1 : -1);
                }
            }
        }

        // pawn structure eval includes:
        // 
        // 1. doubled (or tripled) pawns penalty
        // 2. isolated pawn penalty
        //
        eval += PawnStructureEval(b.pieces[0, 0], 0, piece_count);
        eval -= PawnStructureEval(b.pieces[1, 0], 1, piece_count);

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
        //
        eval += RookEval(b, piece_count);

        List<Move> moves = [];
        Movegen.GetPseudoLegalMoves(b, b.side_to_move, moves);

        eval += Math.Min(moves.Count / 2, 30);

        // side to move should also get a slight advantage
        eval += b.side_to_move == 0 ? SIDE_TO_MOVE_BONUS : -SIDE_TO_MOVE_BONUS;

        return (short)(eval/* + r.Next(-12, 12)*/);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int GetTableValue(int p, int col, int pos, int piece_count) {
        // this method uses the value tables in EvalTables.cs, and is used to evaluate a piece position
        // there are two tables - midgame and endgame, this is important, because the pieces should be
        // in different positions as the game progresses (e.g. a king in the midgame should be in the corner,
        // but in the endgame in the center)

        int i = (p * 64) + (col == 0 ? 63 - pos : pos);
        int mg_value = EvalTables.Midgame[i];
        int eg_value = EvalTables.Endgame[i];

        return (short)(mg_value * piece_count / 32 + eg_value * (32 - piece_count) / 32);
    }

    // bonuses or penalties for pawn structure
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static short PawnStructureEval(ulong p, int col, int piece_count) {

        int eval = 0;

        col = col == 0 ? 1 : -1;

        for (int i = 0; i < 8; i++) {
            ulong file = Consts.FileMask[i];

            // count the number of pawns on the file
            // and maybe penalize doubled pawns
            int file_occ = BB.Popcount(file & p);
            eval += (file_occ - 1) * DOUBLED_PAWN_PENALTY * col;

            // current file + files on the sides
            ulong adj = AdjFiles[i];

            // if the number of pawns on current file is equal to the number of pawns
            // on the current plus adjacent files, we know the pawn/s are isolated
            int sides_occ = BB.Popcount(adj & p);
            eval += file_occ != sides_occ ? 0 : ISOLATED_PAWN_PENALTY * col;
        }

        // TODO - PENALTY FOR NOT LEAVING D/E SQUARES

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
    private static short RookEval(Board b, int piece_count) {
        short eval = 0;

        // rooks are, as opposed to knights, more valuable if be have fewer pieces on the board.
        // number of white rooks and black rooks on the board:
        int w_rooks = BB.Popcount(b.pieces[0, 3]);
        int b_rooks = BB.Popcount(b.pieces[1, 3]);

        // add some eval for white if it has rooks
        eval -= (short)(w_rooks * (32 - piece_count) / 3);

        // suntract some eval for black it has rooks
        eval += (short)(b_rooks * (32 - piece_count) / 3);

        return eval;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static short QueenEval(ulong q, int col, int piece_count) {
        int eval = 0;

        //eval += PiecesTableEval(q, 4, col, piece_count);

        return (short)eval;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static short KingEval(ulong k, int col, int piece_count) {
        int eval = 0;

        //eval += PiecesTableEval(k, 5, col, piece_count);

        return (short)eval;
    }
}