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
    internal byte PriorReductions;
    
    // the current alpha-beta score bounds
    internal Window Window;
    
    // the previous move played
    internal Move LastMove;
    
    // is this a PV node from the previous search iteration?
    internal bool IsPV;

    internal SearchState(sbyte ply, sbyte depth, byte priorReductions, Window window, Move lastMove, bool isPv) {
        Ply             = ply;
        Depth           = depth;
        PriorReductions = priorReductions;
        Window          = window;
        LastMove        = lastMove;
        IsPV            = isPv;
    }
}