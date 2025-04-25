//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.evaluation;

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Kreveta.search.moveorder;

// this class is based on the idea of pawn correction history, but the uses are
// slightly different. static eval correction history records the differences
// between the static eval and the search scores of different positions. the
// boards are indexed by a feature, which in our case is the pawn structure
// (we hash the pawns). the values stored are then usually used to slightly
// adjust the static eval of future positions with the same feature. however,
// i figured that modifying the static eval directly doesn't really work well,
// so we use the stored values for stuff such as modifying the futility margin.
internal static class PawnCorrectionHistory {

    // the size of the hash table
    private const int CorrTableSize = 1048576;

    // maximum correction that can be stored. this needs
    // to stay in range of "short", as the whole table
    // is a short array
    private const short MaxCorrection = 2048;

    // a scale which lowers the corrections when retrieving
    private const short CorrScale     = 128;

    // the table itself
    [ReadOnly(true), DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private static readonly short[][] CorrectionTable = new short[2][];

    static PawnCorrectionHistory() => InitArrays();

    internal static void InitArrays() {
        CorrectionTable[(byte)Color.WHITE] = new short[CorrTableSize];
        CorrectionTable[(byte)Color.BLACK] = new short[CorrTableSize];
    }

    // clear the table
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Clear() {
        Array.Clear(CorrectionTable[(byte)Color.WHITE]);
        Array.Clear(CorrectionTable[(byte)Color.BLACK]);
    }

    // update the pawn correction - takes a board with its score evaluated
    // by an actual search and the depth at which the search was performed.
    internal static void Update([In, ReadOnly(true)] in Board board, int score, int depth) {
        if (depth <= DepthOffset) return;

        // hash the pawns on the current position.
        // each side has its own pawn hash
        ulong wHash = Zobrist.GetPawnHash(board, Color.WHITE);
        ulong bHash = Zobrist.GetPawnHash(board, Color.BLACK);

        // get the indices for both sides
        int wIndex = (int)(wHash % CorrTableSize);
        int bIndex = (int)(bHash % CorrTableSize);

        // get the static eval of the current position and the
        // absolute difference between it and the search score
        short staticEval = Eval.StaticEval(board);
        short diff       = (short)Math.Abs(score - staticEval);

        // compute the shift depending on the depth
        // of the search and the size of the difference
        short shift = Shift(diff, depth);

        // don't bother wasting time with a zero shift
        if (shift == 0) return;

        // first we add or subtract the shift depending
        // on the color and whether the search score
        // was higher or lower than the static eval
        CorrectionTable[(byte)Color.WHITE][wIndex] += (short)(score > staticEval ? shift : -shift);
        CorrectionTable[(byte)Color.BLACK][bIndex] += (short)(score > staticEval ? -shift : shift);

        // only after we added the shift we check whether
        // the new stored value is outside the bounds. we
        // limit this using min and max functions
        CorrectionTable[(byte)Color.WHITE][wIndex] 
            = Math.Min(MaxCorrection,
                Math.Max(CorrectionTable[(byte)Color.WHITE][wIndex],
                    (short)-MaxCorrection
                )
              );

        // and for black the same
        CorrectionTable[(byte)Color.BLACK][bIndex] 
            = Math.Min(MaxCorrection,
                Math.Max(CorrectionTable[(byte)Color.BLACK][bIndex],
                    (short)-MaxCorrection
                )
              );
    }

    // try to retrieve a correction of the static eval of a position
    internal static int GetCorrection([In, ReadOnly(true)] in Board board) {

        //if (CorrectionTable[0] == null) InitArrays();

        // once again the same stuff, hash the pawns
        // and get the indices for both sides
        ulong wHash = Zobrist.GetPawnHash(board, Color.WHITE);
        ulong bHash = Zobrist.GetPawnHash(board, Color.BLACK);

        int wIndex = (int)(wHash % CorrTableSize);
        int bIndex = (int)(bHash % CorrTableSize);

        // the resulting correction is the white correction
        // minus the black correction (each color has its own)
        return (CorrectionTable[(byte)Color.WHITE][wIndex] + CorrectionTable[(byte)Color.BLACK][bIndex]) / CorrScale;
    }

    // values used when calculating shifts
    private const int DepthOffset  = 2;
    private const int MaxShift     = 12;
    private const int ShiftDivisor = 256;

    // the shift that should be used to adjust the corrections.
    // higher depths and higher diffs obviously have stronger impact
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static short Shift(int diff, int depth)
        => (short)Math.Min(MaxShift, diff * (depth - DepthOffset) / ShiftDivisor);
}
