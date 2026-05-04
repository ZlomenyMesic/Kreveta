//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using System;
using System.Runtime.CompilerServices;
using Kreveta.consts;
using Kreveta.search.transpositions;

namespace Kreveta.moveorder.history.corrections;

// correction histories map differences between static evaluations and search
// scores of positions. the inconsistencies are mapped to certain patterns on
// the board, and when a different position with same patterns appears in the
// future, its evaluation may be corrected.
internal static class Corrections {
    // here we use 3 correction histories. pawn corrections map pawn structure, and
    // are generally the most reliable correction type. minor and major pieces are
    // also mapped, but are not taken as seriously as pawn corrections are
    
    // each correction table requires different table size, based on
    // how many unique patterns we can expect to encounter. the max
    // amplitude of corrections is chosen just intuitively
    private static readonly CorrectionTable PawnCorrHist  = new(131_072, 2048);
    private static readonly CorrectionTable MinorCorrHist = new(32_768,  1024);
    private static readonly CorrectionTable MajorCorrHist = new(16_384,  1024);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Update(in Board board, short score, int depth, bool bestMove) {
        if (depth <= 0) return;
        
        // get the difference between static eval, and the search score, while keeping
        // everything white-relative. the shift is also increased if the best move exists
        short diff = (short)((score - board.StaticEval)
                           * (board.SideToMove == Color.WHITE ? 1  : -1)
                           * (bestMove                        ? 17 : 12) / 15);

        // the same exact shift is applied uniformly to all tables. it is calculated
        // based on the depth of the search, and the amount of evaluation inconsistency
        short shift = (short)Math.Clamp(
            (diff * depth * depth) >> 11,
            -25, 25
        );

        // don't bother wasting time with a zero shift
        if (shift == 0) return;
        
        // now we reuse zobrist hashing to only include the specific features
        // of the board. white and black side are stored as separate
        ulong wPawnHash = ZobristHash.GetPawnHash(in board, Color.WHITE);
        ulong bPawnHash = ZobristHash.GetPawnHash(in board, Color.BLACK);
        PawnCorrHist.Update(wPawnHash, bPawnHash, shift);

        ulong wMinorHash = ZobristHash.GetMinorPieceHash(in board, Color.WHITE);
        ulong bMinorHash = ZobristHash.GetMinorPieceHash(in board, Color.BLACK);
        MinorCorrHist.Update(wMinorHash, bMinorHash, shift);

        ulong wMajorHash = ZobristHash.GetMajorPieceHash(in board, Color.WHITE);
        ulong bMajorHash = ZobristHash.GetMajorPieceHash(in board, Color.BLACK);
        MajorCorrHist.Update(wMajorHash, bMajorHash, shift);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static short Get(in Board board) {
        // these weights show, which corrections are most reliable
        ulong wPawnHash  = ZobristHash.GetPawnHash(in board, Color.WHITE);
        ulong bPawnHash  = ZobristHash.GetPawnHash(in board, Color.BLACK);
        int   pawn       = 73 * (PawnCorrHist.Get(wPawnHash, bPawnHash) >> 5);

        ulong wMinorHash = ZobristHash.GetMinorPieceHash(in board, Color.WHITE);
        ulong bMinorHash = ZobristHash.GetMinorPieceHash(in board, Color.BLACK);
        int   minor      = 12 * (MinorCorrHist.Get(wMinorHash, bMinorHash) >> 5);

        ulong wMajorHash = ZobristHash.GetMajorPieceHash(in board, Color.WHITE);
        ulong bMajorHash = ZobristHash.GetMajorPieceHash(in board, Color.BLACK);
        int   major      = 10 * (MajorCorrHist.Get(wMajorHash, bMajorHash) >> 5);
        
        // make corrections side-to-move-relative again
        return (short)((pawn + minor + major) / 100
               * (board.SideToMove == Color.WHITE ? 1 : -1));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Clear() {
        PawnCorrHist.Clear();
        MinorCorrHist.Clear();
        MajorCorrHist.Clear();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Free() {
        PawnCorrHist.Free();
        MinorCorrHist.Free();
        MajorCorrHist.Free();
    }
}