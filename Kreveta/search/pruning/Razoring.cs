//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using System.Runtime.CompilerServices;

namespace Kreveta.search.pruning;

internal static class Razoring {
    internal const int MinPly = 3;
    internal const int Depth  = 4;

    private const int QSPly  = 2;
    private const int MarginBase = 165;

    internal static bool TryReduce(Board board, int depth, Color col, Window window) {
        short qEval = PVSearch.QSearch(board, QSPly, col == Color.WHITE 
            ? new(window.alpha, (short)(window.alpha + 1)) 
            : new((short)(window.beta - 1), window.beta));

        int margin = MarginBase * depth * (col == Color.WHITE ? 1 : -1);

        int score = qEval + margin;
        return col == Color.WHITE
            ? (score <= window.alpha)
            : (score >= window.beta);
    }
}
