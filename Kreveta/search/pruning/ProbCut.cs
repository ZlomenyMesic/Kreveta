//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.consts;

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Kreveta.search.pruning;

internal static class ProbCut {

    private const int InternalR       = 4;
    private const int Margin          = 100;

    internal const int MinIterDepth   = 9;
    internal const int ReductionDepth = 6;
    internal const int R              = 2;

    internal static bool TryReduce([In, ReadOnly(true)] in Board board, int ply, int depth, Window window) {

        // null window around alpha
        Window nullWindowAlpha = board.Color == Color.BLACK
            ? new((short)(window.Beta - 1), window.Beta) 
            : new(window.Alpha, (short)(window.Alpha + 1));
            
        // do the reduced search
        short probCutScore = PVSearch.ProbeTT(board, ply + 1, depth - InternalR - 1, nullWindowAlpha).Score;

        return board.Color == Color.WHITE
            ? probCutScore + Margin <= window.Alpha
            : probCutScore - Margin >= window.Beta;
    }
}