//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using System.Runtime.CompilerServices;

// ReSharper disable InconsistentNaming

namespace Kreveta.search.helpers;

internal static class ThreeFold {
    private static readonly ulong[] _hashes = new ulong[128];
    private static readonly byte[]  _counts = new byte[128];
    private static int _size;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool AddAndCheck(ulong hash) {
        // scan the stack backwards, and try to find this hash
        for (int i = _size - 1; i >= 0; i--) {
            if (_hashes[i] == hash) {
                byte count = ++_counts[i];
                
                _hashes[_size] = hash;
                _counts[_size] = count;
                _size++;
                
                return count >= 3;
            }
        }

        // hash not found - first occurence
        _hashes[_size] = hash;
        _counts[_size] = 1;
        _size++;
        
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Remove(ulong hash) {
        _size--;

        // find previous occurrence and decrement
        for (int i = _size - 1; i >= 0; i--) {
            if (_hashes[i] == hash) {
                _counts[i]--;
                return;
            }
        }
    }

    // clear the stack when a new position is set
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Clear()
        => _size = 0;
}