//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.consts;

using System;

namespace Kreveta;

internal static class TimeMan {

    private const int  DefaultMovestogo  = 40;
    private const long DefaultTimeBudget = 8000;

    private static long _whiteTime;
    private static long _blackTime;

    private static int _movesToGo;

    internal static long MoveTime;

    internal static long TimeBudget;

    internal static void ProcessTimeTokens(ReadOnlySpan<string> tokens) {
        _whiteTime = _blackTime = MoveTime = _movesToGo = 0;

        for (int i = 1; i < tokens.Length; ) {

            bool success = false;

            switch (tokens[i]) {
                
                // we have no time limit for our search, the only thing
                // that terminates it is the "stop" command
                case "infinite": {
                    MoveTime = long.MaxValue;
                    success = true;

                    i++;
                    break;
                }
                
                // there is a specific time budget for this move, which
                // we must not exceed
                case "movetime": {
                    if (i != tokens.Length - 1 && long.TryParse(tokens[i + 1], out MoveTime)) {
                        success = true;

                        i += 2;
                    } break;
                }
                
                // white's total time left on the clock
                case "wtime": {
                    if (i != tokens.Length - 1 && long.TryParse(tokens[i + 1], out _whiteTime)) {
                        success = true;
                        i += 2;
                    } break;
                }
                
                // black's total time left on the clock
                case "btime": {
                    if (i != tokens.Length - 1 && long.TryParse(tokens[i + 1], out _blackTime)) {
                        success = true;
                        i += 2;
                    } break;
                }
                
                // the number of moves we have yet to play until a time reset/addition
                case "movestogo": {
                    if (i != tokens.Length - 1 && int.TryParse(tokens[i + 1], out _movesToGo)) {
                        success = true;
                        i += 2;
                    } break;
                }

                // white's and black's time increment after each move.
                // i believe this isn't any useful, though, so we just
                // parse it to not throw any errors.
                case "winc" or "binc": {
                    if (i != tokens.Length - 1 && int.TryParse(tokens[i + 1], out _)) {
                        success = true;
                        i += 2;
                    } break;
                }
            }

            // failed to parse numbers
            if (!success) 
                goto arg_fail;

            // parsed numbers successfully and got to the last argument
            if (i != tokens.Length) 
                continue;
            
            // in this case we probably have our time budget, but we
            // didn't receive any information regarding the number of
            // moves left, which means we are just playing a different
            // time format
            if (_movesToGo == 0 && MoveTime == 0) {
                UCI.Log($"info string using default movestogo {DefaultMovestogo}", UCI.LogLevel.WARNING);
                _movesToGo = DefaultMovestogo;
            }

            // now we try to use the info we got to set a rational time budget
            CalculateTimeBudget();
            return;
        }

        // if anything went wrong with the token parsing,
        // we don't care at all and just use the default
        // time budget
        arg_fail: 
        TimeBudget = DefaultTimeBudget;
    }

    private static void CalculateTimeBudget() {

        // either infinite time or strictly set time per move
        if (MoveTime != 0L) {
            TimeBudget = MoveTime;
            return;
        }

        TimeBudget = (int)((Game.EngineColor == Color.WHITE 
            ? (float)_whiteTime : _blackTime) / _movesToGo / 1.1f);
    }
}