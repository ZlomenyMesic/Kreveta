/*
*  Stockshrimp chess engine 1.0
*  developed by ZlomenyMesic
*/

using System.Runtime.CompilerServices;

namespace Stockshrimp_1.evaluation;

internal static class Eval {

    const int MATE_BASE = 9000;
    const int MATE_SCORE = 9999;

    const int SIDE_TO_MOVE_BONUS = 5;

    const int DOUBLED_PAWN_PENALTY = -8;
    const int ISOLATED_PAWN_PENALTY = -10;

    const int BISHOP_PAIR_BONUS = 7;

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
        => Math.Abs(s) > MATE_BASE;

    internal static short StaticEval(Board b) {

        int piece_count = BB.Popcount(b.Occupied());

        int eval = 0;

        for (int i = 0; i < 2; i++) {
            for (int j = 0; j < 6; j++) {

                ulong copy = b.pieces[i, j];

                if (copy == 0) continue;

                eval += j switch {
                    0 => PawnEval(copy, i, piece_count),
                    1 => KnightEval(copy, i, piece_count),
                    2 => BishopEval(copy, i, piece_count),
                    3 => RookEval(copy, i, piece_count),
                    4 => QueenEval(copy, i, piece_count),
                    5 => KingEval(copy, i, piece_count),
                    _ => 0
                };
            }
        }

        eval += b.side_to_move == 0 ? SIDE_TO_MOVE_BONUS : -SIDE_TO_MOVE_BONUS;

        return (short)(eval/* + r.Next(-12, 12)*/);
    }

    internal static int GetTableValue(int p, int col, int pos, int piece_count) {
        // this method uses the value tables in EvalTables.cs, and is used to evaluate a piece position
        // there are two tables - midgame and endgame, this is important, because the pieces should be
        // in different positions as the game progresses (e.g. a king in the midgame should be in the corner,
        // but in the endgame in the center)

        pos = col == 0 ? 63 - pos : pos;
        int mg_value = EvalTables.Midgame[(p * 64) + pos];
        int eg_value = EvalTables.Endgame[(p * 64) + pos];

        return (short)(mg_value * piece_count / 32 + eg_value * (32 - piece_count) / 32);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int PiecesTableEval(ulong pieces, int piece_type, int col, int piece_count) {
        int eval = 0;

        ulong copy = pieces;
        int sq;
        while (copy != 0) {
            (copy, sq) = BB.LS1BReset(copy);

            eval += GetTableValue(piece_type, col, sq, piece_count) * (col == 0 ? 1 : -1);
        }

        return eval;
    }

    private static short PawnEval(ulong p, int col, int piece_count) {

        int eval = 0;

        eval += PiecesTableEval(p, 0, col, piece_count);

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

    private static short KnightEval(ulong n, int col, int piece_count) {
        int eval = 0;

        eval += PiecesTableEval(n, 1, col, piece_count);

        col = col == 0 ? 1 : -1;

        // knights are less valuable if be have fewer pieces on the board
        eval -= (32 - piece_count) / 4 * col;

        return (short)eval;
    }

    private static short BishopEval(ulong b, int col, int piece_count) {
        int eval = 0;

        eval += PiecesTableEval(b, 2, col, piece_count);

        col = col == 0 ? 1 : -1;

        // add a potential bonus for a bishop pair (still have both bishops)
        // 0 bishops can not happen because of check above
        eval += (BB.Popcount(b) - 1) * BISHOP_PAIR_BONUS * col;

        return (short)eval;
    }

    private static short RookEval(ulong r, int col, int piece_count) {
        int eval = 0;

        eval += PiecesTableEval(r, 3, col, piece_count);

        col = col == 0 ? 1 : -1;

        // similar to knights, but rooks actually gain value as pieces disappear
        eval += (32 - piece_count) / 3 * col;

        return (short)eval;
    }

    private static short QueenEval(ulong q, int col, int piece_count) {
        int eval = 0;

        eval += PiecesTableEval(q, 4, col, piece_count);

        return (short)eval;
    }

    private static short KingEval(ulong k, int col, int piece_count) {
        int eval = 0;

        eval += PiecesTableEval(k, 5, col, piece_count);

        return (short)eval;
    }
}