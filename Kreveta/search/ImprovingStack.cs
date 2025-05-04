//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.consts;
using System.Diagnostics;
using System.Runtime.CompilerServices;
// ReSharper disable InconsistentNaming

namespace Kreveta.search;

internal class ImprovingStack {
    private short[] _stack;

    internal ImprovingStack() => _stack = [];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Expand(int depth) 
        => _stack = new short[depth];

    internal void AddStaticEval(short staticEval, int ply) {
        if (ply >= _stack.Length)
            return;

        _stack[ply] = staticEval;

        for (int i = ply + 1; i < _stack.Length; i++) {
            _stack[i] = 0;
        }
    }

    internal bool IsImproving(int ply, Color col) {
        if (ply <= 1 || ply >= _stack.Length) 
            return false;

        short prevSE = _stack[ply - 2];
        short curSE  = _stack[ply    ];

        // the static eval might actually be zero in both cases,
        // but it'd be very rare, and we need this to prevent false
        // improving returns when the eval hasn't been set yet
        if (curSE == 0 || prevSE == 0) 
            return false;

        // is the current se better than the one from 2 plies ago?
        return col == Color.WHITE 
            ? curSE > prevSE 
            : curSE < prevSE;
    }
}
