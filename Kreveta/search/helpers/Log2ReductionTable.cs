//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using System;

namespace Kreveta.search.helpers;

internal sealed class Log2ReductionTable {
    
    // the size matches both the move count limit, and the maximum search ply
    private const int Size  = 128;
    private const int Scale = 32;
    
    private readonly short[] _log2;

    internal Log2ReductionTable() {
        _log2 = new short[Size];
        
        // by using the scale 32, we eliminate the necessity of later dequantizing the
        // product of two logarithms, as the fractional depth we use uses scale 1024
        for (int i = 0; i < Size; i++) {
            _log2[i] = (short)(
                Math.Log2(i + 1) * Scale
            );
        }
    }

    // we allow easy indexing of this table; the logarithms of depth and move number are multiplied,
    // and then divided by a few units. however, since both logarithm values are quantized, we must
    // dequantize both of them as well, so that's why such a large number is used
    internal int this[int depth, int moveIndex, int delta] {
        get {
            Assert.True(depth > 0 && moveIndex > 0, "zero or less values in log2 reduction table");
            
            int r = _log2[depth] * _log2[moveIndex] / 5;
            return r - 512 * delta / PVS.RootDelta;
        }
    }
}