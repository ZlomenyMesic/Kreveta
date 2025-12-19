//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//


using Kreveta.consts;

namespace Kreveta.search.pruning;

/*internal static class ProbCut {

    private const int InternalR       = 4;
    private const int Margin          = 50;

    internal const int MinIterDepth   = 3;
    internal const int ReductionDepth = 5;
    internal const int R              = 2;

    internal static bool TryReduce(ref Board board, int ply, int depth, Window window) {

        // null window around alpha
        Window nullWindowAlpha = board.Color == Color.BLACK
            ? new((short)(window.Beta - 1), window.Beta) 
            : new(window.Alpha, (short)(window.Alpha + 1));
            
        // do the reduced search
        short probCutScore = PVSearch.ProbeTT(
            ref board,
            new SearchState(
                ply:         (sbyte)(ply + 1),
                depth:       (sbyte)(depth - InternalR - 1),
                window:      nullWindowAlpha,
                //penultimate: default,
                previous:    default,
                isPVNode:    false
            ),
            false
        ).Score;

        return board.Color == Color.WHITE
            ? probCutScore + Margin <= window.Alpha
            : probCutScore - Margin >= window.Beta;
    }
}*/