//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.consts;
using Kreveta.movegen;

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

        // tokens aren't filtered before being passed to this method, so they might
        // contain anything. for this reason we don't print any errors when we receive
        // an unknown token, we just use the default time budget
        for (int i = 1; i < tokens.Length; i++) {
            switch (tokens[i]) {
                
                // we have no time limit for our search, the only thing
                // capable of terminating it is the "stop" command :((
                case "infinite": {
                    MoveTime = long.MaxValue;
                    break;
                }
                
                // there is a specific time budget for this move, which
                // we must not exceed, but we also shouldn't end early
                case "movetime": {
                    if (i != tokens.Length - 1)
                        _ = long.TryParse(tokens[i + 1], out MoveTime);
                    break;
                }
                
                // white's total time left on the clock
                case "wtime": {
                    if (i != tokens.Length - 1)
                        _ = long.TryParse(tokens[i + 1], out _whiteTime);
                    break;
                }
                
                // black's total time left on the clock
                case "btime": {
                    if (i != tokens.Length - 1)
                        _ = long.TryParse(tokens[i + 1], out _blackTime);
                    break;
                }
                
                // the number of moves we have yet to play until a time reset/addition
                case "movestogo": {
                    if (i != tokens.Length - 1)
                        _ = int.TryParse(tokens[i + 1], out _movesToGo);
                    break;
                }

                // white's time increment after each move played
                case "winc": {
                    if (i != tokens.Length - 1)
                        _ = long.TryParse(tokens[i + 1], out _whiteInc);
                    break;
                }
                
                case "binc": {
                    if (i != tokens.Length - 1)
                        _ = long.TryParse(tokens[i + 1], out _blackInc);
                    break;
                }
            }
        }

        // if we haven't received time arguments, or failed to parse them, default time budget is used
        if (MoveTime == 0 && (Game.EngineColor == Color.WHITE ? _whiteTime == 0 : _blackTime == 0)) {
            TimeBudget = DefaultTimeBudget;
            MoveTime   = DefaultTimeBudget;
            return;
        }
        
        // try to use the info we got to set a rational time budget
        CalculateTimeBudget();
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
        
        // if movestogo isn't present, estimate the remaining move count
        // based on the game phase (endgame => fewer moves left expected)
        int movesToGo = _movesToGo == 0 
            ? EstimateMovesToGo(Game.Board)
            : _movesToGo;

        long timeLeft    = Game.EngineColor == Color.WHITE ? _whiteTime : _blackTime;
        long oppTimeLeft = Game.EngineColor == Color.WHITE ? _blackTime : _whiteTime;
        long inc         = Game.EngineColor == Color.WHITE ? _whiteInc  : _blackInc;
        
        // calculate how many times more time do we have left than the opponent, and
        // based on that potentially reduce movestogo, which makes us think longer
        float timeAdvantage = Math.Clamp((float)timeLeft / oppTimeLeft, 1f, 5f);
        movesToGo -= (int)((timeAdvantage - 1) * (movesToGo / 6.5f));
        movesToGo  = Math.Max(6, movesToGo);
        
        // taking time increments in low remaining time scenarios is dangerous
        bool lowTime = timeLeft < 3 * inc + 2 * moveOverhead;
        long effectiveInc = (long)(lowTime ? 0 : inc * 0.8f);

        // base time per move
        long baseTime = (timeLeft + effectiveInc) / (movesToGo + 1);

        // never allow zero search time
        long budget = Math.Max(moveOverhead / 2, baseTime - moveOverhead);

        // cap extremely long thinks
        budget = Math.Min(budget, (long)(timeLeft * 0.4f));
        TimeBudget = Math.Max(20, budget);
    }
    
    private static int EstimateMovesToGo(Board board) {
        float p = board.GamePhase() / 150f;

        // smooth base expected moves interpolation
        float expected = p          * 38f  // middlegame
                         + (1f - p) * 12f; // endgame
        
        // add a level of complexity into the result - positions with more
        // available legal moves are more complex, and thus searched longer
        int moveCount = Movegen.GetLegalMoves(ref board, stackalloc Move[Consts.MoveBufferSize]);
        float complexityMult = 1f + (moveCount - 19) * 0.025f;
        complexityMult = Math.Clamp(complexityMult, 0.75f, 1.35f);
        
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

        if (timeLeft < 250) return;

        long bonus = (long)(instability < 0
            ? instability * depth / 3
            : instability * depth / 9);

        TimeBudget += Math.Clamp(bonus, -1 - timeLeft / 400, 1 + timeLeft / 400);
    }
}