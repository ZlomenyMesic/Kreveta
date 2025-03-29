/*
 * |============================|
 * |                            |
 * |    Kreveta chess engine    |
 * | engineered by ZlomenyMesic |
 * | -------------------------- |
 * |      started 4-3-2025      |
 * | -------------------------- |
 * |                            |
 * | read README for additional |
 * | information about the code |
 * |    and usage that isn't    |
 * |  included in the comments  |
 * |                            |
 * |============================|
 */

using System.Runtime.CompilerServices;

namespace Kreveta.movegen.pieces;

internal static class Knight {

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ulong GetKnightMoves(ulong knight, ulong free) {
        ulong moves = LookupTables.KnightMoves[BB.LS1B(knight)];
        return moves & free;
    }
}