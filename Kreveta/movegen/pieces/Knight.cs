//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using System.Runtime.CompilerServices;

namespace Kreveta.movegen.pieces;

internal static class Knight {

    // returns a bitboard of all move targets (ending squares) of
    // a certain knight. the free parameter is a combination of empty
    // and enemy-occupied squares
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static unsafe ulong GetKnightTargets(byte sq, ulong free) {
        // since knight moves aren't really based on pieces around (knights
        // can jump over them), we simply index the move bitboard directly
        // by the square index
        ulong targets = LookupTables.KnightTargets[sq];
        
        // we now just & the targets with both empty
        // and enemy squares to avoid friendly captures
        return targets & free;
    }
}