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

namespace Kreveta.movegen;

internal static class LookupTables {
    // Move generators in modern engines generate moves with lookup tables, which
    // are indexed by the square of the slider and a bitboard representing
    // occupied (impassable) squares that might block the movement of the piece.

    // To compress the huge set of possible occupancy bitboards into a reasonable
    // size, Magic Bitboards are often used. They involve multiplying the occupancy
    // bitboard by 'magic' numbers, which are chosen because they empirically
    // map the set of possible occupancy bitboards into a dense range.

    internal static readonly ulong[] KingMoves = new ulong[64];
    internal static readonly ulong[] KnightMoves = new ulong[64];
    internal static readonly ulong[][] RankMoves = new ulong[64][];
    internal static readonly ulong[][] FileMoves = new ulong[64][];
    internal static readonly ulong[][] AntidiagMoves = new ulong[64][];
    internal static readonly ulong[][] DiagMoves = new ulong[64][];

    // initializes all lookup tables when starting the engine
    static LookupTables() {
        InitKingMoves();
        InitKnightMoves();
        InitRankMoves();
        InitFileMoves();
        InitAntidiagMoves();
        InitDiagMoves();
    }

    private static void InitKingMoves() {
        for (int i = 0; i < 64; i++) {
            ulong king = Consts.SqMask[i];

            // starting, right and left square
            ulong sides = (king << 1 & 0xFEFEFEFEFEFEFEFE) | (king >> 1 & 0x7F7F7F7F7F7F7F7F);
            king |= sides;

            // also move these up and down and remove the king from the center
            ulong all = sides | (king >> 8) | (king << 8);
            KingMoves[i] = all;
        }
    }

    private static void InitKnightMoves() {
        for (int i = 0; i < 64; i++) {
            ulong knight = Consts.SqMask[i];

            // right and left sqaures
            // again make sure we're not jumping across the whole board
            ulong r = knight << 1 & 0xFEFEFEFEFEFEFEFE;
            ulong l = knight >> 1 & 0x7F7F7F7F7F7F7F7F;

            // shift the side squares up and down to generate "vertical" moves
            ulong vert = ((r | l) >> 16) | ((r | l) << 16);

            // shift the side squares to the side again
            r = r << 1 & 0xFEFEFEFEFEFEFEFE;
            l = l >> 1 & 0x7F7F7F7F7F7F7F7F;

            // move these up and down to generate "horizontal" moves
            ulong horiz = ((r | l) >> 8) | ((r | l) << 8);

            KnightMoves[i] = vert | horiz;
        }
    }

    private static void InitRankMoves() {
        for (int i = 0; i < 64; i++) {
            RankMoves[i] = new ulong[64];

            for (int o = 0; o < 64; o++) {
                ulong occ = (ulong)o << 1;
                ulong moves = 0;

                // shifting blockers
                int r_bl = (i & 7) + 1;
                while (r_bl <= 7) {
                    moves |= Consts.SqMask[r_bl];
                    if (BB.IsBitSet(occ, r_bl)) break;
                    r_bl++;
                }

                int l_bl = (i & 7) - 1;
                while (l_bl >= 0) {
                    moves |= Consts.SqMask[l_bl];
                    if (BB.IsBitSet(occ, l_bl)) break;
                    l_bl--;
                }

                // move to correct rank
                moves <<= 8 * (i >> 3);
                RankMoves[i][o] = moves;
            }
        }
    }
    private static void InitFileMoves() {
        for (int i = 0; i < 64; i++) {
            FileMoves[i] = new ulong[64];

            for (int o = 0; o < 64; o++) {
                ulong moves = 0;
                ulong rank_moves = RankMoves[7 - (i / 8)][o];

                // rotate rank moves
                for (int b = 0; b < 8; b++) {
                    if (BB.IsBitSet(rank_moves, b)) {
                        moves |= Consts.SqMask[(i & 7) + 8 * (7 - b)];
                    }
                }
                FileMoves[i][o] = moves;
            }
        }
    }

    private static void InitAntidiagMoves() {
        for (int i = 0; i < 64; i++) {
            AntidiagMoves[i] = new ulong[64];

            for (int o = 0; o < 64; o++) {
                int diag = (i >> 3) - (i & 7);

                ulong moves = 0;
                ulong rankMoves = diag > 0 
                    ? RankMoves[i % 8][o] 
                    : RankMoves[i / 8][o];

                for (int b = 0; b < 8; b++) {
                    int rank, file;

                    // rotate rank moves
                    if (BB.IsBitSet(rankMoves, b)) {
                        if (diag >= 0) {
                            rank = diag + b;
                            file = b;
                        } else {
                            file = b - diag;
                            rank = b;
                        }

                        if ((file >= 0) && (file <= 7) && (rank >= 0) && (rank <= 7)) {
                            moves |= Consts.SqMask[file + 8 * rank];
                        }
                    }
                }

                AntidiagMoves[i][o] = moves;
            }
        }
    }

    private static void InitDiagMoves() {
        for (int i = 0; i < 64; i++) {
            DiagMoves[i] = new ulong[64];

            for (int o = 0; o < 64; o++) {
                int diag = (i >> 3) + (i & 7);

                ulong moves = 0;
                ulong rankMoves = diag > 7 
                    ? RankMoves[7 - i / 8][o] 
                    : RankMoves[i % 8][o];

                for (int b = 0; b < 8; b++) {
                    int rank; int file;

                    // rotate rank moves
                    if (BB.IsBitSet(rankMoves, b)) {
                        if (diag >= 7) {
                            rank = 7 - b;
                            file = diag - 7 + b;
                        } else {
                            rank = diag - b;
                            file = b;
                        }

                        if ((file >= 0) && (file <= 7) && (rank >= 0) && (rank <= 7)) {
                            moves |= Consts.SqMask[file + 8 * rank];
                        }
                    }
                }

                DiagMoves[i][o] = moves;
            }
        }
    }
}
