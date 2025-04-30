//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using System;
using System.Runtime.CompilerServices;

namespace Kreveta.consts;

internal enum PType : byte {
    PAWN   = 0,
    KNIGHT = 1,
    BISHOP = 2,
    ROOK   = 3,
    QUEEN  = 4,
    KING   = 5,
    NONE   = 6
}

internal static class PTypeExtensions {

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PType ToPType(this char c)
        => (PType)Consts.Pieces.IndexOf(c, StringComparison.Ordinal);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static char ToChar(this PType type)
        => Consts.Pieces[(byte)type];
}