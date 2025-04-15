//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.evaluation;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Kreveta.search.moveorder;

internal static class PawnCorrectionHistory {

    private const int CorrTableSize = 1048576;

    private const int DepthOffset   = 2;
    private const int MaxShift      = 12;
    private const int ShiftDivisor  = 256;

    private const int MaxCorrection = 2048;
    private const int CorrScale     = 128;

    [ReadOnly(true)]
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private static readonly int[,] PawnCorrHist = new int[2, CorrTableSize];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Clear() => Array.Clear(PawnCorrHist);

    internal static void Update([NotNull] in Board board, int score, int depth) {
        if (depth <= DepthOffset) return;

        ulong wHash = Zobrist.GetPawnHash(board, Color.WHITE);
        ulong bHash = Zobrist.GetPawnHash(board, Color.BLACK);

        int wIndex = (int)(wHash % CorrTableSize);
        int bIndex = (int)(bHash % CorrTableSize);

        short staticEval = Eval.StaticEval(board);
        int diff = Math.Abs(score - staticEval);
        int shift = Shift(diff, depth);

        if (shift == 0) return;

        PawnCorrHist[(byte)Color.WHITE, wIndex] += score > staticEval ? shift : -shift;
        PawnCorrHist[(byte)Color.BLACK, bIndex] += score > staticEval ? -shift : shift;

        PawnCorrHist[(byte)Color.WHITE, wIndex] 
            = Math.Min(MaxCorrection, Math.Max(PawnCorrHist[(byte)Color.WHITE, wIndex], -MaxCorrection));

        PawnCorrHist[(byte)Color.BLACK, bIndex] 
            = Math.Min(MaxCorrection, Math.Max(PawnCorrHist[(byte)Color.BLACK, bIndex], -MaxCorrection));
    }

    internal static int GetCorrection([NotNull] in Board board) {
        ulong wHash = Zobrist.GetPawnHash(board, Color.WHITE);
        ulong bHash = Zobrist.GetPawnHash(board, Color.BLACK);

        int wIndex = (int)(wHash % CorrTableSize);
        int bIndex = (int)(bHash % CorrTableSize);

        int correction = (PawnCorrHist[(byte)Color.WHITE, wIndex] + PawnCorrHist[(byte)Color.BLACK, bIndex]) / CorrScale;
        return correction;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Shift(int diff, int depth) {
        return Math.Min(MaxShift, diff * (depth - DepthOffset) / ShiftDivisor);
    }
}
