/*
 *  Stockshrimp chess engine 1.0
 *  developed by ZlomenyMesic
 */

using Stockshrimp_1.movegen;

namespace Stockshrimp_1;

internal class Search {
    internal static Move MMSearchBestMove(Board b, int col, int depth) {
        Move[] options = Movegen.GetLegalMoves(b, col);
        return new();
    }
}
