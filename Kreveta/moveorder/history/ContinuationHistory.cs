//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.movegen;

using System;
using System.Runtime.CompilerServices;

namespace Kreveta.moveorder.history;

internal static class ContinuationHistory {
    private static short[] _table = null!;

    internal static void Init()
        => _table = new short[6 * 64 * 6 * 64];
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Clear()
        => Array.Clear(_table, 0, _table.Length);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Age() {
        for (int i = 0; i < 6 * 64 * 6 * 64; i++)
            _table[i] /= 2;
    }

    // store a new continuation - same as with counters, there
    // is no priority measure, old continuations get overwritten
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Add(Move previous, Move current, int depth, bool isGood) {
        if (depth <= 0) return;
        
        int i = Index((int)previous.Piece, previous.End, (int)current.Piece, current.End);
        int v = _table[i] + (isGood ? depth : -depth) * depth / 8;

        if ((uint)(v + 2048) > 4096)
            v = v > 0 ? 2048 : -2048;

        _table[i] = (short)v;
    }

    // try to retrieve the continuation
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int GetScore(Move previous, Move current) {
        return _table[Index((int)previous.Piece, previous.End, (int)current.Piece, current.End)];
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Index(int p1, int to1, int p2, int to2)
        => ((p1 * 64 + to1) * 6 + p2) * 64 + to2;
}