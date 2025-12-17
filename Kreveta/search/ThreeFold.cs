//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using System.Collections.Generic;
using System.Runtime.CompilerServices;
// ReSharper disable InconsistentNaming

namespace Kreveta.search;

internal static class ThreeFold {
    private static readonly Dictionary<ulong, byte> _positions = [];
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool AddAndCheck(ulong hash) {
        if (!_positions.TryAdd(hash, 1)) {
            _positions[hash]++;
            return _positions[hash] >= 3;
        }
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Remove(ulong hash) {
        _positions[hash]--;
        if (_positions[hash] == 0)
            _positions.Remove(hash);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Clear() 
        => _positions.Clear();
}