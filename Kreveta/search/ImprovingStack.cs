//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using System.Diagnostics;
using System.Runtime.CompilerServices;
// ReSharper disable InconsistentNaming

namespace Kreveta.search;

internal class ImprovingStack {

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private short[] _stack;

    internal ImprovingStack() => _stack = [];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Expand(int depth) 
        => _stack = new short[depth];

    internal void AddStaticEval(short staticEval, int ply) {
        if (ply >= _stack.Length) return;

        _stack[ply] = staticEval;

        for (int i = ply + 1; i < _stack.Length; i++) {
            _stack[i] = default;
        }
    }

    internal bool IsImproving(int ply, Color col) {
        if (ply <= 1 || ply >= _stack.Length) 
            return false;

        short prevSE = _stack[ply - 2];
        short curSE  = _stack[ply    ];

        if (curSE == default || prevSE == default) 
            return false;

        return col == Color.WHITE 
            ? curSE > prevSE 
            : curSE < prevSE;
    }
}
