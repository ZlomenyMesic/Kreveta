//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.tuning;

using System;
using System.Runtime.CompilerServices;

namespace Kreveta.moveorder.history.corrections;

internal static class Corrections {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Update(in Board board, short score, int depth) {
        if (depth <= 2) return;
        
        // get the static eval of the current position and the
        // absolute difference between it and the search score
        short diff = (short)(score - board.StaticEval);

        // compute the shift depending on the depth
        // of the search, and the size of the difference
        short shift = (short)Math.Clamp(diff * (depth - 2) / 256, -12, 12);

        // don't bother wasting time with a zero shift
        if (shift == 0) return;
        
        PawnCorrections.Update(in board, shift);
        KingCorrections.Update(in board, shift);
        MinorPieceCorrections.Update(in board, shift);
        MajorPieceCorrections.Update(in board, shift);
    }
    
    // 69 15  9  8
    // 73  6 12 10
    // 76  7 10  9
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static short Get(in Board board) {
        int pawn  = 76 * PawnCorrections.Get(in board);
        int king  =  7 * KingCorrections.Get(in board);
        int minor = 10 * MinorPieceCorrections.Get(in board);
        int major =  9 * MajorPieceCorrections.Get(in board);

        return (short)((pawn + king + minor + major) / 100);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Clear() {
        KingCorrections.Clear();
        PawnCorrections.Realloc();
        MinorPieceCorrections.Clear();
        MajorPieceCorrections.Clear();
    }
}