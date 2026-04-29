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
internal ref struct SearchState(sbyte ply, sbyte depth, sbyte priorReductions, Move lastMove, Move excludedMove, bool followPv) {
    internal sbyte Ply             = ply;             // ply from the root node, stays precise regardless of reductions
    internal sbyte Depth           = depth;           // depth yet to be searched, may be reduced/extended
    internal sbyte PriorReductions = priorReductions; // the sum of total reductions in the current line
    internal Move  LastMove        = lastMove;        // previous move (ply - 1)
    internal Move  ExcludedMove    = excludedMove;    // tt move excluded by singular extensions
    internal bool  FollowPV        = followPv;        // are we following the previous principal variation?
}