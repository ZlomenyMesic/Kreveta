//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.movegen;

using System.Runtime.InteropServices;
// ReSharper disable InconsistentNaming

namespace Kreveta.search;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal ref struct SearchState {
    // the ply from the root node that is currently being
    // searched. it is completely independent of depth
    internal sbyte Ply;
    
    // the number of plies that are yet to be searched.
    // this number can be reduced.
    internal sbyte Depth;
    
    // used to restrict certain reductions/extensions
    internal sbyte PriorReductions;
    
    // the current alpha-beta score bounds
    internal Window Window;
    
    // the previous move played
    internal Move LastMove;
    
    // is this position a part of the previous principal variation?
    internal bool FollowPV;

    internal SearchState(sbyte ply, sbyte depth, sbyte priorReductions, Window window, Move lastMove, bool followPv) {
        Ply             = ply;
        Depth           = depth;
        PriorReductions = priorReductions;
        Window          = window;
        LastMove        = lastMove;
        FollowPV        = followPv;
    }
}