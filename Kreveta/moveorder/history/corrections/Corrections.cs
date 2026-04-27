//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using System;
using System.Runtime.CompilerServices;
using Kreveta.consts;

namespace Kreveta.moveorder.history.corrections;

// correction histories map differences between static evaluations and search
// scores of positions. the inconsistencies are mapped to certain patterns on
// the board, and when a different position with same patterns appears in the
// future, its evaluation may be corrected.

// here we use 4 correction histories. pawn corrections map pawn structure, and
// are generally the most reliable correction type. minor and major pieces are
// also mapped. king corrections work quite good as well, but instead of mapping
// patterns, they just generally measure where evaluation is heading.
internal static class Corrections {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Update(in Board board, short score, int depth) {
        if (depth <= 0) return;

        // make both the static eval AND the score white-relative
        int col = board.SideToMove == Color.WHITE ? 1 : -1;
        int se  = board.StaticEval * col;
        score  *= (short)col;
        
        // get the static eval of the current position and the
        // absolute difference between it and the search score
        short diff = (short)(score - se);

        // compute the shift depending on the depth
        // of the search, and the size of the difference
        short shift = (short)Math.Clamp(diff * depth / 300, -15, 15);

        // don't bother wasting time with a zero shift
        if (shift == 0) return;
        
        PawnCorrections.Update(in board, shift);
        MinorPieceCorrections.Update(in board, shift);
        MajorPieceCorrections.Update(in board, shift);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static short Get(in Board board) {
        
        // these weights show, which corrections are most reliable
        int pawn  = 73 * PawnCorrections.Get(in board);
        int minor = 12 * MinorPieceCorrections.Get(in board);
        int major = 10 * MajorPieceCorrections.Get(in board);
        
        // make corrections side-to-move-relative again
        return (short)((pawn + minor + major) / 100
               * (board.SideToMove == Color.WHITE ? 1 : -1));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Clear() {
        PawnCorrections.Realloc();
        MinorPieceCorrections.Clear();
        MajorPieceCorrections.Clear();
    }
}