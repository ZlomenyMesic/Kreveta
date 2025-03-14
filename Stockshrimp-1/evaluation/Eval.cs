/*
 *  Stockshrimp chess engine 1.0
 *  developed by ZlomenyMesic
 */

namespace Stockshrimp_1.evaluation;

internal static class Eval {

    const int MATE_BASE = 9000;
    const int MATE_SCORE = 9999;

    const int DOUBLED_PAWN_PENALTY = 15;
        
    private static readonly Random r = new();

    internal static short GetMateScore(int col, int ply)
    => (short)((col == 0 ? -1 : 1) * (MATE_SCORE - ply));
    internal static bool IsMateScore(int s)
        => Math.Abs(s) > MATE_BASE;

    internal static short StaticEval(Board b) {

        int eval = 0;

        int piece_count = BB.Popcount(b.Occupied());

        for (int i = 0; i < 2; i++) {
            for (int j = 0; j < 6; j++) {

                if (j == 0) {
                    //eval += GetPawnStructureEval(b.pieces[i, j]) * (i == 0 ? 1 : -1);
                }

                ulong copy = b.pieces[i, j];

                while (copy != 0) {
                    (copy, int sq) = BB.LS1BReset(copy);

                    eval += GetTableValue(j, i, sq, piece_count) * (i == 0 ? 1 : -1);
                }
            }
        }

        //eval += b.side_to_move == 0 ? 15 : -15;

        return (short)eval;// + r.Next(-3, 3);
    }

    internal static int GetTableValue(int p, int col, int pos, int piece_count) {
        // this method uses the value tables in EvalTables.cs, and is used to evaluate a piece position
        // there are two tables - midgame and endgame, this is important, because the pieces should be
        // in different positions as the game progresses (e.g. a king in the midgame should be in the corner,
        // but in the endgame in the center)

        pos = col == 0 ? 63 - pos : pos;
        int mg_value = EvalTables.Midgame[(p * 64) + pos];
        int eg_value = EvalTables.Endgame[(p * 64) + pos];
        //int egValue = EvalTables.Endgame[((p + 1) * 64) - pos];

        // TODO: Gradual evaluation

        return (int)(float)(mg_value * piece_count / 32 + eg_value * (32 - piece_count) / 32);

        //float eval = (mg_value * piece_count / 32) 
        //    + (eg_value * (32 - piece_count) / 32);

        //return mg_value;
    }

    private static int GetPawnStructureEval(ulong p) {

        int eval = 0;

        for (int i = 0; i < 8; i++) {
            int file_occ = BB.Popcount(Consts.FileMask[i] & p);
            eval += (file_occ - 1) * -DOUBLED_PAWN_PENALTY;
        }

        return eval;
    }
}
