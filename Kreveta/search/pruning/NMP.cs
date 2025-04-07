//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.evaluation;
using System.Runtime.CompilerServices;

namespace Kreveta.search.pruning;

// NULL MOVE PRUNING:
// we assume that there is at least one move that improves our position,
// so we play a "null move", which is essentially no move at all (we just
// flip the side to move and erase the en passant square). we then search
// this null child at a reduced depth (depth reduce R). if we still fail
// high despite skipping a move, we can expect that playing a move would
// also fail high, and thus, we can prune this branch.
//
// NMP for this reason failes in zugzwangs
internal static class NMP {

    // minimum depth and ply required for nmp
    internal const  int MinDepth = 0;
    internal static int MinPly   = 3;

    // depth reduce base within nmp
    private const int RBase = 3;

    // with fewer pieces on the board, we want to prune less
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void UpdateMinPly(int pieceCount) {
        MinPly = Math.Max(3, (32 - pieceCount) / 7);
    }

    // try null move pruning
    internal static bool TryPrune(Board board, int depth, int ply, Window window, Color col, out short score) {

        // null window around beta
        Window nullWindowBeta = col == Color.WHITE 
            ? new((short)(window.beta - 1), window.beta) 
            : new(window.alpha, (short)(window.alpha + 1));

        // child with no move played
        Board nullChild = board.GetNullChild();

        int R = ply <= 4 ? RBase - 1 : RBase;

        // do the reduced search
        score = PVSearch.ProbeTT(nullChild, ply + 1, depth - R - 1, nullWindowBeta).Score;

        // if we are TOO good (like really good) we don't prune, it might be worth
        // to actually check what's happening
        //int MARGIN = 395 * col == 0 ? 1 : -1;
        //if (window.FailsHigh((short)(score - MARGIN), col)) return false;

        // if we failed high, that means the score is above beta and is "too good" to be
        // allowed by the opponent. if we don't fail high, we just continue the expansion
        //
        // currently we are returning the null search score, but returning beta
        // may also work. this needs some testing
        return col == Color.WHITE
            ? (score >= window.beta)
            : (score <= window.alpha);
    }
}
