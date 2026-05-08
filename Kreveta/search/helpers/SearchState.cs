//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.movegen;

using System.Runtime.InteropServices;

// ReSharper disable InconsistentNaming

namespace Kreveta.search.helpers;

// some search information that gets passed down the search tree
[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal ref struct SearchState(int ply, int depth, int priorReduction, Move lastMove, Move excludedMove, bool followPv, bool ignore3fold) {
    internal int  Ply            = ply;            // ply from the root node, stays precise regardless of reductions
    internal int  Depth          = depth;          // depth yet to be searched, may be reduced/extended
    internal int  PriorReduction = priorReduction; // the total reduction/extension on the previous ply
    internal Move LastMove       = lastMove;       // previous move (ply - 1)
    internal Move ExcludedMove   = excludedMove;   // tt move excluded by singular extensions
    internal bool FollowPV       = followPv;       // are we following the previous principal variation?
    internal bool Ignore3Fold    = ignore3fold;    // do we care about 3-fold repetition draws?
}