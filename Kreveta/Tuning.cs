//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

#pragma warning disable CA1031
#pragma warning disable CA1305

using System;

namespace Kreveta;

internal static class Tuning {
    internal static short x1 = 0;
    internal static short x2 = 0;
    internal static short x3 = 0;
    internal static short x4 = 0;
    internal static short x5 = 0;
    
    internal static short x6 = 0;
    internal static short x7 = 0;
    internal static short x8 = 0;
    internal static short x9 = 0;
    internal static short x10 = 0;
    
    internal static void TuneParams(ReadOnlySpan<string> tokens) {
        x1 = short.Parse(tokens[1]);
        x2 = short.Parse(tokens[2]);
        x3 = short.Parse(tokens[3]);
        x4 = short.Parse(tokens[4]);
        x5 = short.Parse(tokens[5]);
        x6 = short.Parse(tokens[6]);
        x7 = short.Parse(tokens[7]);
        x8 = short.Parse(tokens[8]);
        x9 = short.Parse(tokens[9]);
        x10 = short.Parse(tokens[10]);
    }
}

#pragma warning restore CA1031
#pragma warning restore CA1305