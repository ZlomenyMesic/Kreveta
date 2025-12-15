//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.consts;
using System.Runtime.CompilerServices;

// ReSharper disable InconsistentNaming

namespace Kreveta.search.pruning;

// works pretty much on the same idea as futility pruning, but is only used in qsearch.
// since we only evaluate captures in qsearch, we take the static eval of a position
// (stand pat), we add the value of the captured piece, and a margin (called delta). if
// this value fails low, we can assume that there isn't any hope in this branch, and we
// can safely prune it
internal static class DeltaPruning {
    internal const int MinPly = 4;

    //private const int DeltaMarginBase      = 0;
    private const int DeltaDepthMultiplier = 77;
    private const int CapturedMultiplier   = 105;

    internal static bool TryPrune(int ply, int curQSDepth, Color col, Window window, short standPat, int captured) {

        // the delta base is multiplied by depth, but the depth must be calculated
        // in a bit more difficult way (maximum qsearch depth - current ply)
        int delta = (curQSDepth - ply) * DeltaDepthMultiplier
                                       * (col == Color.WHITE ? 1 : -1);

        // add the value of the captured piece and delta
        standPat += (short)(captured * CapturedMultiplier / 100 * (col == Color.WHITE ? 1 : -1));
        standPat += (short)delta;

        // check for failing low
        return col == Color.WHITE
            ? standPat <= window.Alpha
            : standPat >= window.Beta;
    }
}
