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
    private const long DefaultTimeBudget = 8000;

    // the total time left on each side's clocks
    private static long _whiteTime;
    private static long _blackTime;
    
    // each side's time increment after a move
    private static long _whiteInc;
    private static long _blackInc;

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
    internal static bool TimeBudgetAdjusted;

    internal static void ProcessTimeTokens(ReadOnlySpan<string> tokens) {
        _whiteTime = _blackTime = _whiteInc = _blackInc = MoveTime = _movesToGo = 0;

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

                // white's time increment after each move played
                case "winc": {
                    if (i != tokens.Length - 1 && long.TryParse(tokens[i + 1], out _whiteInc)) {
                        success = true;
                        i += 2;
                    } break;
                }
                
                case "binc": {
                    if (i != tokens.Length - 1 && long.TryParse(tokens[i + 1], out _blackInc)) {
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

            // now we try to use the info we got to set a rational time budget
            CalculateTimeBudget();
            return;
        }

        // if anything went wrong during token parsing,
        // we don't care at all and just use the default
        // time budget
        arg_fail:
        TimeBudget = DefaultTimeBudget;
        MoveTime   = DefaultTimeBudget;
    }

    private static void CalculateTimeBudget() {
        const int moveOverhead = 30;
        
        // we have a strictly set time for our search,
        // or are in an infinite search, so we don't
        // care abount setting a time budget
        if (MoveTime != 0L) {
            TimeBudget = MoveTime;
            return;
        }
        
        int movesToGo = _movesToGo == 0 
            ? EstimateMovesToGo(Game.Board) 
            : _movesToGo;

        long timeLeft = Game.EngineColor == Color.WHITE ? _whiteTime : _blackTime;
        long inc      = Game.EngineColor == Color.WHITE ? _whiteInc  : _blackInc;
        
        // base time per move
        long baseTime = (long)((timeLeft + inc * 0.8) / (movesToGo + 1));

        // never allow zero search time
        long budget = Math.Max(15, baseTime - moveOverhead);

        // cap extremely long thinks
        long maxBudget = (long)(timeLeft * 0.40);
        budget = Math.Min(budget, maxBudget);

        TimeBudget = Math.Max(10, budget);
    }
    
    private static int EstimateMovesToGo(Board board) {
        float p = board.GamePhase() / 150f;

        // smooth base expected moves interpolation
        float expected = p * 36f           // middlegame
                         + (1f - p) * 12f; // endgame
        
        // total piece count excluding kings (which are always present)
        int pieceCount = (int)ulong.PopCount(board.Occupied) - 2;

        // map piece count into a multiplier roughly 0.75–1.35
        float complexityMult = 1f + (pieceCount - 10) * 0.025f;
        complexityMult = Math.Clamp(complexityMult, 0.75f, 1.35f);
        
        // check for ultra low material positions
        if (p <= 0.12f) {
            expected        = Math.Min(expected, 10f);
            complexityMult *= 0.85f;
        }
        
        int result = (int)(expected * complexityMult);

        // clamp to reasonable range
        return Math.Clamp(result, 8, 45);
    }

    // depending on whether the position seems to be stable or unstable,
    // the time budget may be altered. instability is based on score
    // differences and best move changes between iterations
    internal static void AccountForInstability(float instability, int depth) {
        // if we have a precise time the search has to
        // take, the time budget obviously won't be touched
        if (MoveTime != 0) return;
        
        long timeLeft = Game.EngineColor == Color.WHITE 
            ? _whiteTime : _blackTime;

        long bonus = (long)(instability < 0
            ? instability * depth / 4
            : instability * depth / 8);
        
        TimeBudget += Math.Clamp(bonus, -1 - timeLeft / 400, 1 + timeLeft / 400);
    }
    
    // when the score suddenly changes from the previous turn (both drops
    // and rises), we can try to increase our time budget to search this
    // turn a bit deeper
    internal static void TryIncreaseTimeBudget() {
        /*if (!Game.FullGame || TimeBudgetAdjusted) 
            return;

        TimeBudgetAdjusted = true;
        
        int scoreChange = Math.Abs(Game.PreviousScore - PVSearch.PVScore);
        switch (scoreChange) {
            case >= 150 and < 300: TimeBudget = TimeBudget * 3 / 2; break;
            case >= 300:           TimeBudget *= 3;                 break;
        }

        var timeCap = (Game.Board.Color == Color.WHITE
            ? _whiteTime : _blackTime) * 2 / 5;
        
        TimeBudget = Math.Min(TimeBudget, timeCap);*/
    }
}