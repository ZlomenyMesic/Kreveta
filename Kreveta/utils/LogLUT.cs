//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using System;
// ReSharper disable InconsistentNaming

namespace Kreveta.utils;

internal static partial class MathLUT {
    private const int LnHighResCount = 1024;
    private const int LnLowResCount  = 256;

    // total entries = 1280
    private static readonly float[] LnTable = InitLnTable();

    private static float[] InitLnTable() {
        float[] t = new float[LnHighResCount + LnLowResCount];

        // range (0; 2) - high resolution
        for (int i = 0; i < LnHighResCount; i++) {
            float x = i / (float)(LnHighResCount - 1) * 2f;

            t[i] = x <= 0 
                ? float.NegativeInfinity 
                : MathF.Log(x);
        }

        // range (2; 10) - lower resolution
        for (int i = 0; i < LnLowResCount; i++) {
            float x = 2f + i / (float)(LnLowResCount - 1) * 8f;
            t[LnHighResCount + i] = MathF.Log(x);
        }

        return t;
    }
    
    public static float FastLn(float x) {
        switch (x) {
            case <= 2f: {
                // high resolution region
                float fx   = x * (LnHighResCount - 1) / 2f;
                int   i    = (int)fx;
                float frac = fx - i;

                // linear interpolation
                return LnTable[i] + frac * (LnTable[i + 1] - LnTable[i]);
            }

            case <= 10f: {
                // low resolution region
                float fx   = (x - 2f) * (LnLowResCount - 1) / 8f;
                int   i    = (int)fx + LnHighResCount;
                float frac = fx - (int)fx;

                return LnTable[i] + frac * (LnTable[i + 1] - LnTable[i]);
            }

            // clamp high end
            default: return LnTable[LnHighResCount + LnLowResCount - 1];
        }
    }
}