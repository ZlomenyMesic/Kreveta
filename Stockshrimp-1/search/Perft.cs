/*
 *  Stockshrimp chess engine 1.0
 *  developed by ZlomenyMesic
 */

using Stockshrimp_1.movegen;

namespace Stockshrimp_1.search;

internal static class Perft {
    internal static long Run(Board b, int depth) {

        if (depth == 1) {
            return Movegen.GetLegalMoves(b).Count;
        }

        long nodes = 0;

        List<Board> children = b.GetChildren();

        for (int i = 0; i < children.Count; i++) {
            nodes += Run(children[i], depth - 1);
        }

        return nodes;
    }
}
