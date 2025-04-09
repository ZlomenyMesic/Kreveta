//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

namespace Kreveta.movegen;

internal static class LookupTables {
    // Move generators in modern engines generate moves with lookup tables, which
    // are indexed by the square of the slider and a bitboard representing
    // occupied (impassable) squares that might block the movement of the piece.

    // To compress the huge set of possible occupancy bitboards into a reasonable
    // size, Magic Bitboards are often used. They involve multiplying the occupancy
    // bitboard by 'magic' numbers, which are chosen because they empirically
    // map the set of possible occupancy bitboards into a dense range.

    internal static readonly ulong[]   KingTargets     = new ulong[64];
    internal static readonly ulong[]   KnightTargets   = new ulong[64];
    internal static readonly ulong[][] RankTargets     = new ulong[64][];
    internal static readonly ulong[][] FileTargets     = new ulong[64][];
    internal static readonly ulong[][] AntidiagTargets = new ulong[64][];
    internal static readonly ulong[][] DiagTargets     = new ulong[64][];

    // initializes all lookup tables when starting the engine
    static LookupTables() {
        InitKingTargets();
        InitKnightTargets();
        InitRankTargets();
        InitFileTargets();
        InitAntidiagTargets();
        InitDiagTargets();
    }

    private static void InitKingTargets() {
        for (int i = 0; i < 64; i++) {
            ulong king = Consts.SqMask[i];

            // starting, right and left square
            ulong sides = (king << 1 & 0xFEFEFEFEFEFEFEFE) | (king >> 1 & 0x7F7F7F7F7F7F7F7F);
            king |= sides;

            // also move these up and down and remove the king from the center
            ulong all = sides | (king >> 8) | (king << 8);
            KingTargets[i] = all;
        }
    }

    private static void InitKnightTargets() {
        for (int i = 0; i < 64; i++) {
            ulong knight = Consts.SqMask[i];

            // right and left sqaures
            // again make sure we're not jumping across the whole board
            ulong right = knight << 1 & 0xFEFEFEFEFEFEFEFE;
            ulong left  = knight >> 1 & 0x7F7F7F7F7F7F7F7F;

            // shift the side squares up and down to generate "vertical" moves
            ulong vertical = ((right | left) >> 16) 
                           | ((right | left) << 16);

            // shift the side squares to the side again
            right = right << 1 & 0xFEFEFEFEFEFEFEFE;
            left  = left  >> 1 & 0x7F7F7F7F7F7F7F7F;

            // move these up and down to generate "horizontal" moves
            ulong horizontal = ((right | left) >> 8) 
                             | ((right | left) << 8);

            KnightTargets[i] = vertical | horizontal;
        }
    }

    private static void InitRankTargets() {
        for (int i = 0; i < 64; i++) {
            RankTargets[i] = new ulong[64];

            for (int o = 0; o < 64; o++) {
                ulong occ = (ulong)o << 1;
                ulong targets = 0;

                // sliding to the right until we hit a blocker
                int slider = (i & 7) + 1;
                while (slider <= 7) {
                    targets |= Consts.SqMask[slider];
                    if (BB.IsBitSet(occ, slider)) break;
                    slider++;
                }

                // sliding to the left
                slider = (i & 7) - 1;
                while (slider >= 0) {
                    targets |= Consts.SqMask[slider];
                    if (BB.IsBitSet(occ, slider)) break;
                    slider--;
                }

                // move to correct rank
                targets <<= 8 * (i >> 3);
                RankTargets[i][o] = targets;
            }
        }
    }
    private static void InitFileTargets() {
        for (int i = 0; i < 64; i++) {
            FileTargets[i] = new ulong[64];

            for (int o = 0; o < 64; o++) {
                ulong targets = 0;
                ulong rankTargets = RankTargets[7 - (i / 8)][o];

                // rotate rank targets
                for (int bit = 0; bit < 8; bit++) {
                    if (BB.IsBitSet(rankTargets, bit)) {
                        targets |= Consts.SqMask[(i & 7) + 8 * (7 - bit)];
                    }
                }

                FileTargets[i][o] = targets;
            }
        }
    }

    private static void InitAntidiagTargets() {
        for (int i = 0; i < 64; i++) {
            AntidiagTargets[i] = new ulong[64];

            for (int o = 0; o < 64; o++) {
                int diag = (i >> 3) - (i & 7);

                ulong targets = 0;
                ulong rankTargets = diag > 0 
                    ? RankTargets[i % 8][o] 
                    : RankTargets[i / 8][o];

                for (int bit = 0; bit < 8; bit++) {
                    int rank, file;

                    // rotate rank moves
                    if (BB.IsBitSet(rankTargets, bit)) {

                        if (diag >= 0) {
                            rank = diag + bit;
                            file = bit;
                        } 
                        else {
                            file = bit - diag;
                            rank = bit;
                        }

                        if ((file >= 0) && (file <= 7) && (rank >= 0) && (rank <= 7)) {
                            targets |= Consts.SqMask[file + 8 * rank];
                        }
                    }
                }

                AntidiagTargets[i][o] = targets;
            }
        }
    }

    private static void InitDiagTargets() {
        for (int i = 0; i < 64; i++) {
            DiagTargets[i] = new ulong[64];

            for (int o = 0; o < 64; o++) {
                int diag = (i >> 3) + (i & 7);

                ulong targets = 0;
                ulong rankTargets = diag > 7 
                    ? RankTargets[7 - i / 8][o] 
                    : RankTargets[i % 8][o];

                for (int bit = 0; bit < 8; bit++) {
                    int rank; int file;

                    // rotate rank moves
                    if (BB.IsBitSet(rankTargets, bit)) {

                        if (diag >= 7) {
                            rank = 7 - bit;
                            file = diag - 7 + bit;
                        } 
                        else {
                            rank = diag - bit;
                            file = bit;
                        }

                        if ((file >= 0) && (file <= 7) && (rank >= 0) && (rank <= 7)) {
                            targets |= Consts.SqMask[file + 8 * rank];
                        }
                    }
                }

                DiagTargets[i][o] = targets;
            }
        }
    }
}
