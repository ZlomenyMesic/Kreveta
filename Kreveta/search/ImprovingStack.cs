//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using System.Runtime.CompilerServices;

namespace Kreveta.search;

internal class ImprovingStack {
    private readonly short[] _stack = new short[64];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Clear() => Array.Clear(_stack);

    internal void AddStaticEval(short staticEval, int ply) {
        for (int i = ply; i < _stack.Length; i++) {
            _stack[i] = default;
        }

        _stack[ply] = staticEval;
    }

    internal bool IsImproving(int ply, Color col) {
        if (ply <= 1) return false;

        short prevSE = _stack[ply - 2];
        short curSE  = _stack[ply];

        if (curSE == default || prevSE == default) 
            return false;

        return col == Color.WHITE 
            ? curSE > prevSE 
            : curSE < prevSE;
    }
}
