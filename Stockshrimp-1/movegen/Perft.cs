/*
 *  Stockshrimp chess engine 1.0
 *  developed by ZlomenyMesic
 */

namespace Stockshrimp_1.movegen {
    internal static class Performace {
        internal static long Perft(Board b, int depth, int col) {

            if (depth == 1) {
                return Movegen.GetLegalMoves(b, col).Length;
            }

            long nodes = 0;

            Board[] children = b.GenerateChildren(col);

            for (int i = 0; i < children.Length; i++) {
                nodes += Perft(children[i], depth - 1, col == 0 ? 1 : 0);
            }

            return nodes;
        }
    }
}
