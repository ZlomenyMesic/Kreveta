//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using System.Runtime.CompilerServices;

namespace Kreveta.movegen.pieces;

internal static class Knight {

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static unsafe ulong GetKnightTargets(ulong knight, ulong free) {
        ulong targets = LookupTables.KnightTargets[BB.LS1B(knight)];
        return targets & free;
    }
}