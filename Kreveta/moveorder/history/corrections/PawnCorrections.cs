//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.consts;
using Kreveta.search.transpositions;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Kreveta.moveorder.history.corrections;

// this class is based on the idea of pawn correction history, but the uses are
// slightly different. static eval correction history records the differences
// between the static eval, and the search scores of different positions. the
// boards are indexed by a feature, which in our case is the pawn structure
// (we hash the pawns). the values stored are then usually used to slightly
// adjust the static eval of future positions with the same feature. however,
// i figured that modifying the static eval directly doesn't really work well,
// so we use the stored values for stuff such as modifying the futility margin.
internal static unsafe class PawnCorrections {

    // size of the hash table; MUST be a power of 2
    // in order to allow & instead of modulo indexing
    private const int CorrTableSize   = 131_072;

    // maximum correction that can be stored. this needs
    // to stay in range of "short", as the whole table
    // is a short array
    private const short MaxCorrection = 2048;

    // a scale, which lowers the corrections when retrieving
    private const short CorrScale     = 128;

    // the table itself
    private static short* _whiteCorrections;
    private static short* _blackCorrections;
    
        // clear the table
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Clear() {
        if (_whiteCorrections is not null) {
            NativeMemory.AlignedFree(_whiteCorrections);
            _whiteCorrections = null;
        }
        if (_blackCorrections is not null) {
            NativeMemory.AlignedFree(_blackCorrections);
            _blackCorrections = null;
        }
    }
    
    internal static void Realloc() {
        Clear();
        
        _whiteCorrections = (short*)NativeMemory.AlignedAlloc(
            byteCount: CorrTableSize * sizeof(short),
            alignment: 64);
        
        _blackCorrections = (short*)NativeMemory.AlignedAlloc(
            byteCount: CorrTableSize * sizeof(short),
            alignment: 64);

        for (int i = 0; i < CorrTableSize; ++i) {
            _whiteCorrections[i] = 0;
            _blackCorrections[i] = 0;
        }
    }

    // update the pawn correction - takes a board with its score evaluated
    // by an actual search, and the depth at which the search was performed.
    internal static void Update(in Board board, short shift) {
        // hash the pawns on the current position.
        // each side has its own pawn hash
        ulong wHash = ZobristHash.GetPawnHash(in board, Color.WHITE);
        ulong bHash = ZobristHash.GetPawnHash(in board, Color.BLACK);

        // get the indices for both sides
        int wIndex = (int)(wHash & CorrTableSize - 1);
        int bIndex = (int)(bHash & CorrTableSize - 1);

        // first we add or subtract the shift depending
        // on the color and whether the search score
        // was higher or lower than the static eval
        _whiteCorrections[wIndex] += shift;
        _blackCorrections[bIndex] += shift;

        // only after we added the shift we check whether
        // the new stored value is outside the bounds.
        _whiteCorrections[wIndex] = (short)Math.Clamp((int)_whiteCorrections[wIndex], -MaxCorrection, MaxCorrection);
        _blackCorrections[bIndex] = (short)Math.Clamp((int)_blackCorrections[bIndex], -MaxCorrection, MaxCorrection);
    }

    // try to retrieve a correction of the static eval of a position
    internal static short Get(in Board board) {

        // once again the same stuff, hash the pawns
        // and get the indices for both sides
        ulong wHash = ZobristHash.GetPawnHash(in board, Color.WHITE);
        ulong bHash = ZobristHash.GetPawnHash(in board, Color.BLACK);

        int wIndex = (int)(wHash & CorrTableSize - 1);
        int bIndex = (int)(bHash & CorrTableSize - 1);

        // the resulting correction being the difference instead of sum is
        // just plain wrong. nothing about this makes sense. but it works
        return (short)((_whiteCorrections[wIndex] + _blackCorrections[bIndex]) / CorrScale);
    }
}