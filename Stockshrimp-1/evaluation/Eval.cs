/*
*  Stockshrimp chess engine 1.0
*  developed by ZlomenyMesic
*/

namespace Stockshrimp_1.evaluation;

internal static class Eval {

    const int MATE_BASE = 9000;
    const int MATE_SCORE = 9999;

    const int DOUBLED_PAWN_PENALTY = -15;
    const int ISOLATED_PAWN_PENALTY = -22;

    private static readonly Random r = new();

    internal static short GetMateScore(int col, int ply)
    => (short)((col == 0 ? -1 : 1) * (MATE_SCORE - ply));
    internal static bool IsMateScore(int s)
        => Math.Abs(s) > MATE_BASE;

    internal static short StaticEval(Board b) {

        int piece_count = BB.Popcount(b.Occupied());

        int base_eval = 0;

        for (int i = 0; i < 2; i++) {
            for (int j = 0; j < 6; j++) {
                //int c = BB.Popcount(b.pieces[i, j]);
                //int p_val = j switch {
                //    0 => 100,
                //    1 => 290,
                //    2 => 310,
                //    3 => 500,
                //    4 => 900,
                //    _ => 0
                //};

                //base_eval += c * p_val * (i == 0 ? 1 : -1);

                ulong copy = b.pieces[i, j];

                while (copy != 0) {
                    (copy, int sq) = BB.LS1BReset(copy);

                    base_eval += GetTableValue(j, i, sq, piece_count) * (i == 0 ? 1 : -1);
                }
            }
        }

        base_eval += PawnStructureEval(b.pieces[0, 0]);
        base_eval -= PawnStructureEval(b.pieces[1, 0]);

        base_eval += b.side_to_move == 0 ? 12 : -12;

        return (short)(base_eval + r.Next(-12, 12));
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

    private static int PawnStructureEval(ulong p) {

        // no pawns left on the board
        if (p == 0) return 0;

        int eval = 0;

        for (int i = 0; i < 8; i++) {
            ulong file = Consts.FileMask[i];

            // count the number of pawns on the file
            // and maybe penalize doubled pawns
            int file_occ = BB.Popcount(file & p);
            eval += (file_occ - 1) * DOUBLED_PAWN_PENALTY;

            // current file + files on the sides
            ulong sides = Consts.FileMask[i] 
                | (i != 0 ? Consts.FileMask[i - 1] : 0)
                | (i != 7 ? Consts.FileMask[i + 1] : 0);

            int sides_occ = BB.Popcount(sides & p);
            eval += file_occ != sides_occ ? 0 : ISOLATED_PAWN_PENALTY;
        }

        // center of the board bonus
        int imm_center = BB.Popcount(p & 103481868288UL);
        eval += imm_center switch {
            0 => -10,
            1 => -3,
            2 => 12,
            _ => 5,
        };

        // center + something around the center bonus
        int wide_center = BB.Popcount(p & 66229406269440UL);
        eval += wide_center switch {
            0 => -8,
            1 => -4,
            2 => 0,
            3 or 4 => 3,
            _ => 5,
        };

        // pawns on the sides of the board penalty
        int edges = BB.Popcount(p & 142393223479296UL);
        eval += edges switch {
            0 => 3,
            1 => 0,
            2 => -2,
            _ => -4,
        };

        return eval;
    }
}