//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.evaluation;
using Kreveta.search;
using Kreveta.search.pruning;
using System.Diagnostics;

namespace Kreveta;

internal static class SpeedTest {

    private const int Repetitions = 20;
    private const long IterationsPerRep = 500000;

    //
    // run using the "test" command
    //
    internal static void Run() {

        long elapsed = 0;

        for (int i = 0; i < Repetitions; i++) {

            Stopwatch sw = Stopwatch.StartNew();

            long auxCounter = 0;

            for (int j = 0; j < IterationsPerRep; j++) {
                auxCounter += Eval.StaticEval(Game.board);
            }

            sw.Stop();
            elapsed += sw.ElapsedMilliseconds;
        }

        long average = elapsed / Repetitions;
        UCI.Log($"average time spent: {average} ms", UCI.LogLevel.INFO);
    }
}
