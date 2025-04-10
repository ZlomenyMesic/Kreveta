//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.evaluation;
using System.Diagnostics;

namespace Kreveta;

#if DEBUG

internal static class SpeedTest {

    private const int Iterations = 3000000;

    [Conditional("DEBUG")]
    internal static void Run() {
        Stopwatch sw = Stopwatch.StartNew();

// Variable is assigned but its value is never used
#pragma warning disable CS0219

// Unnecessary assignment of a value
#pragma warning disable IDE0059

        long auxCounter = 0;

#pragma warning restore IDE0059
#pragma warning restore CS0219

        for (int i = 0; i < Iterations; i++) {
            // logic here

            ulong something = 0xFFFFFFFFFFFFFFFF;
            while (something != 0) {
                int index = BB.LS1BReset(ref something);
                auxCounter += index;
            }
        }

        UCI.Log($"time spent: {sw.ElapsedMilliseconds} ms", UCI.LogLevel.INFO);
    }
}

#endif // DEBUG