//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.consts;
using System.Runtime.CompilerServices;

// ReSharper disable InconsistentNaming

namespace Kreveta.search.helpers;

internal sealed class ImprovingStack {
    private short[] _stack;
    private int     _len;

    internal ImprovingStack() => _stack = [];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Expand(int depth) {
        // leave some space for potential extensions
        _len   = depth + 8;
        _stack = new short[_len];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void UpdateStaticEval(short se, int ply, Color col) {
        if (ply >= _stack.Length)
            return;

        _stack[ply] = (short)(se * (col == Color.WHITE ? 1 : -1));
    }

    internal bool IsImproving(int ply, Color col) {
        if (ply <= 1 || ply >= _stack.Length) 
            return false;
        
        short prevSE = _stack[ply - 2];
        short curSE  = _stack[ply    ];

        // is the current se better than the one from 2 plies ago?
        return col == Color.WHITE
            ? curSE > prevSE : curSE < prevSE;
    }
}
