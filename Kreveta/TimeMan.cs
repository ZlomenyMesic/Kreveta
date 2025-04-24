//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

namespace Kreveta;

internal static class TimeMan {

    private const int  DefaultMovestogo  = 40;
    private const long DefaultTimeBudget = 8000;

    private static long WhiteTime;
    private static long BlackTime;

    private static int WhiteIncrement;
    private static int BlackIncrement;

    private static int MovesToGo;

    internal static long MoveTime;

    internal static long TimeBudget;

    internal static void ProcessTimeTokens(string[] toks) {
        WhiteTime = BlackTime = MoveTime = WhiteIncrement = BlackIncrement = MovesToGo = 0;

        for (int i = 1; i < toks.Length; ) {

            bool success = false;

            if (toks[i] == "infinite") {
                MoveTime = long.MaxValue;
                success = true;

                i++;
            } 

            else if (toks[i] == "movetime") {
                if (i != toks.Length - 1 && long.TryParse(toks[i + 1], out MoveTime)) {
                    success = true;

                    i += 2;
                }
            }
            else if (toks[i] == "wtime") {
                if (i != toks.Length - 1 && long.TryParse(toks[i + 1], out WhiteTime)) {
                    success = true;
                    i += 2;
                }
            }

            else if (toks[i] == "btime") {
                if (i != toks.Length - 1 && long.TryParse(toks[i + 1], out BlackTime)) {
                    success = true;
                    i += 2;
                }
            }

            else if (toks[i] == "movestogo") {
                if (i != toks.Length - 1 && int.TryParse(toks[i + 1], out MovesToGo)) {
                    success = true;
                    i += 2;
                }
            }

            // failed to parse numbers
            if (!success) 
                goto arg_fail;

            // parsed numbers successfully and got to the last argument
            if (i != toks.Length) 
                continue;
            
            if (MovesToGo == 0 && MoveTime == 0) {
                UCI.Log($"info string using default movestogo {DefaultMovestogo}", UCI.LogLevel.WARNING);
                MovesToGo = DefaultMovestogo;
            }

            CalculateTimeBudget();
            return;
        }

        arg_fail: 
        TimeBudget = DefaultTimeBudget;
    }

    private static void CalculateTimeBudget() {

        // either infinite time or strictly set time per move
        if (MoveTime != 0) {
            TimeBudget = MoveTime;
            return;
        }

        TimeBudget = (int)((Game.color == Color.WHITE 
            ? (float)WhiteTime : BlackTime) / MovesToGo / 1.1f);
    }
}