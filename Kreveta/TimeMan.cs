//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.consts;

using System;

namespace Kreveta;

internal static class TimeMan {

    // when time arguments are missing or incomplete,
    // we can go with these default values
    private const int  DefaultMovestogo  = 40;
    private const long DefaultTimeBudget = 8000;

    // the total time left on each side's clocks
    private static long _whiteTime;
    private static long _blackTime;

    // the number of moves left until a time reset/addition
    private static int _movesToGo;

    // if movetime is set, it means we either received 
    // the "movetime" argument or should perform an infinite
    // search. when this is set, we should avoid using book
    // moves or ending the search early, since we have the
    // exact amount of time we must spend on the search
    internal static long MoveTime;
    
    // this is used when movetime isn't set. time budget
    // sets a boundary when the search should be aborted,
    // but it can be ended prematurely
    internal static long TimeBudget;

    internal static void ProcessTimeTokens(ReadOnlySpan<string> tokens) {
        _whiteTime = _blackTime = MoveTime = _movesToGo = 0;

        // tokens aren't filtered before being passed to
        // this method, so they might contain anything.
        // for this reason we don't print any errors when
        // we receive an unknown token, we just use the
        // default time budget
        for (int i = 1; i < tokens.Length; ) {
            bool success = false;

            switch (tokens[i]) {
                
                // we have no time limit for our search, the only thing
                // capable of terminating it is the "stop" command :((
                case "infinite": {
                    MoveTime = long.MaxValue;
                    success = true;

                    // "infinite" doesn't come with any parameters
                    i++;
                    break;
                }
                
                // there is a specific time budget for this move, which
                // we must not exceed, but we also shouldn't end early
                case "movetime": {
                    if (i != tokens.Length - 1 && long.TryParse(tokens[i + 1], out MoveTime)) {
                        success = true;

                        // "movetime" and other arguments come together
                        // with a number, which we have already parsed
                        // above, so we skip the argument, which would
                        // just be the number
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
                // pretend to use this to not throw any errors
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

            // we still have more arguments to process
            if (i != tokens.Length) 
                continue;
            
            // in this case we probably have our time budget, but we
            // didn't receive any information regarding the number of
            // moves left, so we use the default
            if (_movesToGo == 0 && MoveTime == 0) {
                UCI.Log($"info string using default movestogo {DefaultMovestogo}", UCI.LogLevel.WARNING);
                _movesToGo = DefaultMovestogo;
            }

            // now we try to use the info we got to set a rational time budget
            CalculateTimeBudget();
            return;
        }

        // if anything went wrong during token parsing,
        // we don't care at all and just use the default
        // time budget
        arg_fail: 
        TimeBudget = DefaultTimeBudget;
    }

    private static void CalculateTimeBudget() {

        // we have a strictly set time for our search,
        // or are in an infinite search, so we don't
        // care abount setting a time budget
        if (MoveTime != 0L) {
            TimeBudget = MoveTime;
            return;
        }

        // otherwise the time budget is simply our total
        // time left divided by a little bit more than
        // movestogo, to keep a short time buffer
        TimeBudget = (int)((Game.EngineColor == Color.WHITE 
            ? (float)_whiteTime : _blackTime) / _movesToGo / 1.1f);
    }
}