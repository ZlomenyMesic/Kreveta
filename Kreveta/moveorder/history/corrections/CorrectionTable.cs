//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

#pragma warning disable CA1810

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Kreveta.moveorder.history.corrections;

internal unsafe class CorrectionTable {
    // size of the hash table; MUST be a power of 2
    // in order to allow & instead of modulo indexing
    private readonly ulong _tableSize;
    private readonly short _maxAmplitude;
    
    private readonly short* _whiteCorrections;
    private readonly short* _blackCorrections;

    internal CorrectionTable(ulong tableSize, short maxAmplitude) {
        _tableSize    = tableSize;
        _maxAmplitude = maxAmplitude;
        
        // tables are allocated just once
        _whiteCorrections = (short*)NativeMemory.AlignedAlloc(
            byteCount: (nuint)_tableSize * sizeof(short),
            alignment: 64);
        
        _blackCorrections = (short*)NativeMemory.AlignedAlloc(
            byteCount: (nuint)_tableSize * sizeof(short),
            alignment: 64);

        // i believe it is not guaranteed that the allocated memory is zeroed
        // by default. in my experience, it always has been. but we must make
        // sure no noise is present, so this is a simple safety measure
        NativeMemory.Clear(_whiteCorrections, (nuint)_tableSize * sizeof(short));
        NativeMemory.Clear(_blackCorrections, (nuint)_tableSize * sizeof(short));
    }

    // clear all correction data, usually prior to a new search
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Clear() {
        NativeMemory.Clear(_whiteCorrections, (nuint)_tableSize * sizeof(short));
        NativeMemory.Clear(_blackCorrections, (nuint)_tableSize * sizeof(short));
    }

    // free the manually allocated memory once we quit the program
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Free() {
        NativeMemory.AlignedFree(_whiteCorrections);
        NativeMemory.AlignedFree(_blackCorrections);
    }
    
    internal void Update(ulong wHash, ulong bHash, short shift) {
        if (wHash != 0UL) {
            int wIndex = (int)(wHash & _tableSize - 1);
            
            _whiteCorrections[wIndex] += shift;
            _whiteCorrections[wIndex]  = (short)Math.Clamp(
                _whiteCorrections[wIndex], -_maxAmplitude, _maxAmplitude
            );
        }
        if (bHash != 0UL) {
            int bIndex = (int)(bHash & _tableSize - 1);
            
            _blackCorrections[bIndex] += shift;
            _blackCorrections[bIndex]  = (short)Math.Clamp(
                _blackCorrections[bIndex], -_maxAmplitude, _maxAmplitude
            );
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int Get(ulong wHash, ulong bHash) {
        if (wHash == 0UL || bHash == 0UL) return 0;

        int wIndex = (int)(wHash & _tableSize - 1);
        int bIndex = (int)(bHash & _tableSize - 1);
        
        return _whiteCorrections[wIndex] + _blackCorrections[bIndex];
    }
}

#pragma warning restore CA1810