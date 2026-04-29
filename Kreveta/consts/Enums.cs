//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using System;
using System.Runtime.CompilerServices;

// ReSharper disable InconsistentNaming

namespace Kreveta.consts;

// this enum is NOT flags, however, marking it as flags allows
// usage of bitwise operators free of warnings, which results
// in more efficient color flips
[Flags]
internal enum Color : byte {
    WHITE = 0,
    BLACK = 1,
    NONE  = 2
}

[Flags]
internal enum CastRights : byte {
    NONE  = 0,
    
    K     = 1, // white kingside
    Q     = 2, // white queenside
    k     = 4, // black kingside
    q     = 8, // black queenside
    
    ALL   = K | Q | k | q,
    WHITE = K | Q,
    BLACK = k | q
}

// depending on where the score fell relatively to the
// window when saving to TT, we store the score type
[Flags]
internal enum ScoreType : byte {
    LOWER_BOUND = 1, // the score is at least x (was above beta)
    UPPER_BOUND = 2, // the score is at most x (was below alpha)
    SCORE_EXACT = 4  // the score fell right into the window
}

internal enum TTLookupState : byte {
    NOT_PERFORMED  = 0,
    DOES_NOT_EXIST = 1,
    FOUND          = 2
}

internal enum PType : byte {
    PAWN   = 0,
    KNIGHT = 1,
    BISHOP = 2,
    ROOK   = 3,
    QUEEN  = 4,
    KING   = 5,
    NONE   = 6
}

// some extensions to convert chars to piece types and vice versa
internal static class PTypeExtensions {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PType ToPType(this char c)
        => (PType)Consts.Pieces.IndexOf(c, StringComparison.Ordinal);

    // return the piece type in lowercase (pnbrqk)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static char ToChar(this PType type)
        => Consts.Pieces[(byte)type];
}