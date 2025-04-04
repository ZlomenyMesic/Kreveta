//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

namespace Kreveta;

internal static class TimeMan {
    private static long wtime;
    private static long btime;

    private static int winc;
    private static int binc;

    private static int movestogo;

    private static long movetime = 0;

    internal static long TimeBudget;

    private static readonly int DefaultMovestogo = 40;
    private static readonly long DefaultTimeBudget = 8000;

    internal static void ProcessTime(string[] toks) {
        wtime = btime = movetime = winc = binc = movestogo = 0;

        for (int i = 1; i < toks.Length; ) {

            bool success = false;

            if (toks[i] == "infinite") {
                movetime = long.MaxValue;
                success = true;

                i++;
            } 

            else if (toks[i] == "movetime") {
                if (i != toks.Length - 1 && long.TryParse(toks[i + 1], out movetime)) {
                    success = true;

                    i += 2;
                }
            }
            else if (toks[i] == "wtime") {
                if (i != toks.Length - 1 && long.TryParse(toks[i + 1], out wtime)) {
                    success = true;
                    i += 2;
                }
            }

            else if (toks[i] == "btime") {
                if (i != toks.Length - 1 && long.TryParse(toks[i + 1], out btime)) {
                    success = true;
                    i += 2;
                }
            }

            else if (toks[i] == "movestogo") {
                if (i != toks.Length - 1 && int.TryParse(toks[i + 1], out movestogo)) {
                    success = true;
                    i += 2;
                }
            }

            // failed to parse numbers
            if (!success) 
                goto arg_fail;

            // parsed numbers successfully and got to the last argument
            if (success && i == toks.Length) {

                if (movestogo == 0 && movetime == 0) {
                    Console.WriteLine($"info string using default movestogo {DefaultMovestogo}");
                    movestogo = DefaultMovestogo;
                }

                CalculateTimeBudget();
                return;
            }
        }

        arg_fail: {
            TimeBudget = DefaultTimeBudget;
            return;
        }
    }

    internal static void CalculateTimeBudget() {

        // either infinite time or strictly set time per move
        if (movetime != 0) {
            TimeBudget = movetime;
            return;
        }

        TimeBudget = (int)((Game.color == Color.WHITE ? wtime : btime) / movestogo / 1.1f);
    }
}