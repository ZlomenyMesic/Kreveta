//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.consts;
using System.Runtime.CompilerServices;

// ReSharper disable InconsistentNaming

namespace Kreveta.search.helpers;

// improving stack holds static evaluations indexed by search ply. all evaluations
// are stored white-relative, which allows us to compare them easily. when the eval
// has improved for the side to move in the past two plies, search more carefully
internal sealed class ImprovingStack {
    private short[] _stack = null!;
    private int     _len;
    
    // before each new search iteration, the improving stack is expanded
    // to fit all search tree plies including some potential extensions
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Expand(int depth) {
        // leave some space for potential extensions
        _len   = depth + 8;
        _stack = new short[_len];
    }

    // add a static evaluation at the specified ply
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void UpdateStaticEval(short se, int ply, Color col) {
        if (ply >= _len)
            return;

        // store the evaluation white-relative
        _stack[ply] = (short)(se * (col == Color.WHITE ? 1 : -1));
    }

    internal bool IsImproving(int ply, Color col) {
        if (ply <= 1 || ply >= _len) 
            return false;
        
        short prevSE = _stack[ply - 2];
        short curSE  = _stack[ply    ];

        // is the current se better than the one from 2 plies ago?
        return col == Color.WHITE
            ? curSE > prevSE : curSE < prevSE;
    }
}
