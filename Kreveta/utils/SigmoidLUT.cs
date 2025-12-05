//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.nnue;

using System;
using System.Runtime.CompilerServices;
// ReSharper disable InconsistentNaming

namespace Kreveta.utils;

internal static partial class MathLUT {
    private const int SigmHalfTable = 5 * NNUEEvaluator.QScale;
    private static readonly short[] SigmTable = InitSigmTable();

    private static short[] InitSigmTable() {
        var t = new short[SigmHalfTable * 2 + 1];
        for (int i = -SigmHalfTable; i <= SigmHalfTable; i++) {
            
            // 1000 * sigmoid(x / scale)
            t[i + SigmHalfTable] = (short)(1000 * (1f / (1f + MathF.Exp((float)-i / NNUEEvaluator.QScale))));
        }
        
        return t;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static short FastSigmoid(int x) {
        return x switch {
            // clamp to LUT range
            <= -SigmHalfTable => 5,
            >= SigmHalfTable  => 994,
            _                 => SigmTable[x + SigmHalfTable]
        };
    }
}