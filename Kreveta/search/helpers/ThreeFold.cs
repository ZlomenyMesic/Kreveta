//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using System.Runtime.CompilerServices;

// ReSharper disable InconsistentNaming

namespace Kreveta.search.helpers;

internal static class ThreeFold {
    
    private static ulong[] _hashes = null!;
    private static byte[]  _counts = null!;
    private static int     _size;

    // the stack must fit all positions from the game itself, e.g. positions from
    // the move list given by "position ... moves ...", and also leave enough room
    // for all plies searched. the search depth is capped at 100, so 128 should do
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Init(int gamePlies) {
        int len = gamePlies + 128;

        _hashes = new ulong[len];
        _counts = new byte[len];
    }

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