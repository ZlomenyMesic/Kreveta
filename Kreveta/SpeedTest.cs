//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.evaluation;
using Kreveta.movegen;
using Kreveta.search.moveorder;
using System.Diagnostics;

namespace Kreveta;

internal static class SpeedTest {

    private const int REPETITIONS = 20;
    private const long ITERATIONS_PER_REP = 50000;

    internal static void Run() {

        long elapsed_ms = 0;

        for (int i = 0; i < REPETITIONS; i++) {
            Stopwatch sw = Stopwatch.StartNew();

            long counter = 0;

            for (int j = 0; j < ITERATIONS_PER_REP; j++) {
                counter += MoveOrder.GetSortedMoves(Game.board, 3).Length;
            }

            sw.Stop();
            elapsed_ms += sw.ElapsedMilliseconds;
        }

        long average = elapsed_ms / REPETITIONS;
        Console.WriteLine($"average time spent: {average} ms");
    }
}
