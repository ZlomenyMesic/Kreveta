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
    internal sbyte Ply;
    
    internal sbyte Depth;
    
    internal Window Window;
    
    internal Move Previous;
    
    internal bool IsPVNode;

    internal SearchState(sbyte ply, sbyte depth, Window window, Move previous, bool isPVNode) {
        Ply      = ply;
        Depth    = depth;
        Window   = window;
        Previous = previous;
        IsPVNode = isPVNode;
    }
}