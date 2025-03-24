/*
 *  Stockshrimp chess engine 1.0
 *  developed by ZlomenyMesic
 */

namespace Stockshrimp_1;

internal static class TimeMan {
    private static int wtime;
    private static int btime;

    private static int winc;
    private static int binc;

    private static int movestogo;
    private static int DEF_MOVESTOGO = 40;

    internal static int time_budget_ms;
    private static int DEF_TIME_BUDGET = 8000;

    internal static void ProcessTime(string[] toks) {
        wtime = btime = winc = binc = movestogo = 0;

        for (int i = 1; i < toks.Length; i += 2) {

            bool success = false;

            if (i == toks.Length - 1) {
                goto arg_fail;
            }

            if (toks[i] == "wtime") {
                if (int.TryParse(toks[i + 1], out wtime)) {
                    success = true;
                }
            }

            else if (toks[i] == "btime") {
                if (int.TryParse(toks[i + 1], out btime)) {
                    success = true;
                }
            }

            else if (toks[i] == "movestogo") {
                if (int.TryParse(toks[i + 1], out movestogo)) {
                    success = true;
                }
            }

            // failed to parse numbers
            if (!success) 
                goto arg_fail;

            // parsed numbers successfully and got to the last argument
            if (success && i == toks.Length - 2) {

                if (movestogo == 0) {
                    Console.WriteLine($"using default movestogo: {DEF_MOVESTOGO}");
                    movestogo = DEF_MOVESTOGO;
                }

                CalculateTimeBudget();
                return;
            }
        }

        arg_fail: {
            Console.WriteLine("missing or invalid time format argument/s");
            Console.WriteLine($"using default time budget");

            time_budget_ms = DEF_TIME_BUDGET;
            return;
        }
    }

    internal static void CalculateTimeBudget() {
        time_budget_ms = (int)((Game.engine_col == 0 ? wtime : btime) / movestogo / 1.2f);
    }
}