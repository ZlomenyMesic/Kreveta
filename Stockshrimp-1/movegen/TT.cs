/*
 *  Stockshrimp chess engine 1.0
 *  developed by ZlomenyMesic
 */

namespace Stockshrimp_1.movegen;

// transposition table
internal static class TT {
    private readonly static Dictionary<(ulong, int), long> table = [];

    internal static void Add(Board board, int depth, long val) {
        table[(Zobrist.GetBoardHash(board), depth)] = val;
    }

    internal static bool TryRetrieveVal(Board board, int depth, out long eval) {
        return table.TryGetValue((Zobrist.GetBoardHash(board), depth), out eval);
    }

    internal static void Clear() {
        table.Clear();
    }

    internal static class Zobrist {
        internal static ulong[,] pcs_hash = new ulong[12, 64];
        internal static ulong[] en_p_hash = new ulong[8];
        internal static ulong[] cast_hash = new ulong[4];

        static Zobrist() {
            for (int i = 0; i < 12; i++) {
                for (int j = 0; j < 64; j++) {
                    pcs_hash[i, j] = RandomUInt64();
                }
            }

            for (int i = 0; i < 8; i++) {
                en_p_hash[i] = RandomUInt64();
            }

            for (int i = 0; i < 4; i++) {
                cast_hash[i] = RandomUInt64();
            }
        }

        internal static ulong GetBoardHash(Board b) {
            ulong hash = 0;

            for (int i = 0; i < 2; i++) {
                for (int j = 0; j < 6; j++) {
                    ulong p = b.pieces[i, j];
                    int index;

                    while (p != 0) {
                        (p, index) = BB.LS1BReset(p);
                        hash ^= pcs_hash[j + i == 0 ? 6 : 0, index];
                    }
                }
            }

            int en_p_file = b.enPassantSquare & 7;
            hash ^= en_p_hash[en_p_file];

            ulong cast_flags = b.castlingFlags;
            int flag;

            while (cast_flags != 0) {
                (cast_flags, flag) = BB.LS1BReset(cast_flags);
                hash ^= cast_hash[flag];
            }

            return hash;
        }

        private static ulong RandomUInt64() {
            Random rand = new();

            uint l = (uint)rand.Next(int.MinValue, int.MaxValue);
            uint r = (uint)rand.Next(int.MinValue, int.MaxValue);

            return r | (l << 32);
        }
    }
}
