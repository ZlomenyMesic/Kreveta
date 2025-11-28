//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using System;
using System.Runtime.CompilerServices;
// ReSharper disable InconsistentNaming

namespace Kreveta.utils;

internal static partial class MathLUT {
    private const int   SigmCount = 1024; // number of samples
    private const float SigmStep  = 8f / (SigmCount - 1);

    private static readonly float[] SigmTable = InitSigmTable();

    private static float[] InitSigmTable() {
        var t = new float[SigmCount];

        for (int i = 0; i < SigmCount; i++) {
            float x = -4f + i * SigmStep;
            
            // sigmoid(x)
            t[i] = 1f / (1f + MathF.Exp(-x));
        }

        return t;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float FastSigmoid(float x) {
        switch (x) {
            // clamp to LUT range
            case <= -4f: return SigmTable[0];
            case >=  4f: return SigmTable[SigmCount - 1];
        }

        // convert x into LUT index
        float fx = (x + 4f) * (SigmCount - 1) / 8f;
        int   i  = (int)fx;

        float frac = fx - i;

        // linear interpolation
        return SigmTable[i] + frac * (SigmTable[i + 1] - SigmTable[i]);
    }
}