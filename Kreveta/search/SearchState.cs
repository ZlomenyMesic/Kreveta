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
    
    // the current alpha-beta score bounds
    internal Window Window;
    
    // the last played move that got us into this position
    internal Move Previous;
    
    // is this a PV node from the previous search iteration?
    internal bool IsPVNode;

    internal SearchState(sbyte ply, sbyte depth, Window window, Move previous, bool isPVNode) {
        Ply      = ply;
        Depth    = depth;
        Window   = window;
        Previous = previous;
        IsPVNode = isPVNode;
    }
}