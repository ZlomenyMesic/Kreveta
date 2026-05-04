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
    
    // clear the stack when a new position is set
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Clear()
        => _size = 0;

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
    
    // to simplify draw detection without having to search any moves, we can look up
    // a certain hash, and find its occurences in the past. we collect the at most
    // 2 upcoming position hashes, to which we can transition from the given hash
    // (there may not be more than two, as that would already be a draw). for each
    // found upcoming hash, we then check whether it is present two times already,
    // which would mean that in the given position exists a drawing move
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static (ulong, ulong) GetUpcomingHashes(ulong hash) {
        ulong first  = 0UL;
        ulong second = 0UL;

        for (int i = 0; i < _size - 1; i++) {
            if (_hashes[i] == hash) {
                ulong next = _hashes[i + 1];

                // choose one of the two slots for the new hash
                if      (first == 0UL)  first  = next;
                else if (first != next) second = next;

                // early exit if we already have two
                if (second != 0UL)
                    break;
            }
        }

        return (first, second);
    }
    
    // counts whether a certain hash is already present twice in
    // the table, which means adding it again would result in a draw 
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool WouldBeDraw(ulong hash) {
        
        // scan backwards to find the latest occurrence
        for (int i = _size - 1; i >= 0; i--)
            if (_hashes[i] == hash)
                return _counts[i] >= 2;

        return false;
    }
}