﻿//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.consts;
using Kreveta.search.transpositions;

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Kreveta.moveorder.historyheuristics;

// this class is based on the idea of pawn correction history, but the uses are
// slightly different. static eval correction history records the differences
// between the static eval, and the search scores of different positions. the
// boards are indexed by a feature, which in our case is the pawn structure
// (we hash the pawns). the values stored are then usually used to slightly
// adjust the static eval of future positions with the same feature. however,
// i figured that modifying the static eval directly doesn't really work well,
// so we use the stored values for stuff such as modifying the futility margin.
internal static unsafe class PawnCorrectionHistory {

    // size of the hash table; MUST be a power of 2
    // in order to allow & instead of modulo indexing
    private const int CorrTableSize = 65_536;

    // maximum correction that can be stored. this needs
    // to stay in range of "short", as the whole table
    // is a short array
    private const short MaxCorrection = 2048;

    // a scale, which lowers the corrections when retrieving
    private const short CorrScale     = 64;

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

        for (int i = 0; i < CorrTableSize; i++) {
            _whiteCorrections[i] = 0;
            _blackCorrections[i] = 0;
        }
    }
    
    // update the pawn correction - takes a board with its score evaluated
    // by an actual search, and the depth at which the search was performed.
    internal static void Update(in Board board, int score, int depth) {
        // get the static eval of the current position and the
        // absolute difference between it and the search score
        short diff = (short)(score - board.StaticEval);

        // compute the shift depending on the depth
        // of the search, and the size of the difference
        short shift = (short)(Math.Min(12, Math.Abs(diff) * depth / 128)
                              * Math.Sign(diff));

        // don't bother wasting time with a zero shift
        if (shift == 0) return;
        
        // hash the pawns on the current position.
        // each side has its own pawn hash
        ulong wHash = ZobristHash.GetPawnHash(board, Color.WHITE);
        ulong bHash = ZobristHash.GetPawnHash(board, Color.BLACK);

        // get the indices for both sides
        int wIndex = (int)(wHash & CorrTableSize - 1);
        int bIndex = (int)(bHash & CorrTableSize - 1);

        // add the shift based on whichever side the real score was better
        _whiteCorrections[wIndex] += shift;
        _blackCorrections[bIndex] += shift;
        
        // make sure the total shift doesn't exceed the max correction value
        _whiteCorrections[wIndex] = (short)Math.Clamp((int)_whiteCorrections[wIndex], -MaxCorrection, MaxCorrection);
        _blackCorrections[bIndex] = (short)Math.Clamp((int)_blackCorrections[bIndex], -MaxCorrection, MaxCorrection);
    }

    // try to retrieve a correction of the static eval of a position
    internal static short GetCorrection(in Board board) {

        // once again the same stuff, hash the pawns and get the indices for both sides
        ulong wHash = ZobristHash.GetPawnHash(board, Color.WHITE);
        ulong bHash = ZobristHash.GetPawnHash(board, Color.BLACK);
        
        int wIndex = (int)(wHash & CorrTableSize - 1);
        int bIndex = (int)(bHash & CorrTableSize - 1);

        // the resulting correction is based on both sides' pawns
        return (short)((_whiteCorrections[wIndex] + _blackCorrections[bIndex]) / CorrScale);
    }
}