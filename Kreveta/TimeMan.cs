//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.consts;

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

    internal static void ProcessTimeTokens(ReadOnlySpan<string> tokens) {
        WhiteTime = BlackTime = MoveTime = WhiteIncrement = BlackIncrement = MovesToGo = 0;

        for (int i = 1; i < tokens.Length; ) {

            bool success = false;

            if (tokens[i] == "infinite") {
                MoveTime = long.MaxValue;
                success = true;

                i++;
            } 

            else if (tokens[i] == "movetime") {
                if (i != tokens.Length - 1 && long.TryParse(tokens[i + 1], out MoveTime)) {
                    success = true;

                    i += 2;
                }
            }
            else if (tokens[i] == "wtime") {
                if (i != tokens.Length - 1 && long.TryParse(tokens[i + 1], out WhiteTime)) {
                    success = true;
                    i += 2;
                }
            }

            else if (tokens[i] == "btime") {
                if (i != tokens.Length - 1 && long.TryParse(tokens[i + 1], out BlackTime)) {
                    success = true;
                    i += 2;
                }
            }

            else if (tokens[i] == "movestogo") {
                if (i != tokens.Length - 1 && int.TryParse(tokens[i + 1], out MovesToGo)) {
                    success = true;
                    i += 2;
                }
            }

            // failed to parse numbers
            if (!success) 
                goto arg_fail;

            // parsed numbers successfully and got to the last argument
            if (i != tokens.Length) 
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

        TimeBudget = (int)((Game.EngineColor == Color.WHITE 
            ? (float)WhiteTime : BlackTime) / MovesToGo / 1.1f);
    }
}