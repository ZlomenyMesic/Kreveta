//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.consts;
using Kreveta.movegen;

using System;

namespace Kreveta;

internal static class TimeManager {

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
    internal static bool TimeBudgetIsDefault;

    internal static void ProcessTimeTokens(ReadOnlySpan<string> tokens) {
        TimeBudget = _whiteTime = _blackTime = _whiteInc = _blackInc = MoveTime = _movesToGo = 0;
        TimeBudgetIsDefault = false;

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
            TimeBudgetIsDefault = true;
            TimeBudget          = DefaultTimeBudget;
            MoveTime            = DefaultTimeBudget;

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
        
        (long ourTime, long oppTime, long inc) = Game.EngineColor == Color.WHITE
            ? (_whiteTime, _blackTime, _whiteInc)
            : (_blackTime, _whiteTime, _blackInc);
        
        // if movestogo isn't present, estimate the remaining move count
        // based on the game phase (endgame => fewer moves left expected)
        double movesToGo = _movesToGo == 0 
            ? EstimateMovesToGo(Game.Board, ourTime, oppTime)
            : _movesToGo;
        
        // taking time increments in low remaining time scenarios is dangerous
        bool lowTime   = ourTime < 3 * inc + 2 * moveOverhead;
        long increment = (long)(lowTime ? 0 : inc * 0.8f);

        // divide time left by moves to go to get time per move
        long budget = (long)(
            (ourTime + increment) / (1 + movesToGo)
        );

        // clamp extremely short or long searches
        budget = Math.Max(moveOverhead / 2, budget - moveOverhead);
        budget = Math.Min(budget, (long)(ourTime * 0.4f));
        
        TimeBudget = budget;
    }
    
    private static double EstimateMovesToGo(Board board, long ourTime, long oppTime) {
        double p = board.GamePhase() / 70.0;
        double s = Math.Abs(Game.PreviousScore);
        double m = Movegen.GetLegalMoves(ref board, stackalloc Move[Consts.MoveBufferSize]);
        
        // smooth base expected moves interpolation
        double material =        p  * 37.2  // middlegame
                        + (1.0 - p) * 11.8; // endgame
        
        // use the game ply to further approximate remaining moves
        double ply      = Math.Max(13.1, (150.0 - Game.Ply) / 5.0);
        double expected = (5.0 * material + 3.0 * ply) / 8.0;
        
        /* add layers of complexity into the result:
         *  1. pawns can promote and prolong the game (add)
         *  2. larger scores generally mean the game will end soon (add)
         *  3. when there are many legal moves, search deeper (mult)
         *  4. spend more time if we have more time than the opponent (mult)
         */
        double pawns    = 0.13 * ulong.PopCount(board.Pieces[0] | board.Pieces[6]);
        double score    = -s * Math.Pow(1.04, s / 100.0) / 320.0;
        double mobility = Math.Clamp(1.03 + (m - 19.7) * 0.022, 0.76, 1.34);
        double time     = 1.0 + (1.0 - Math.Clamp((double)ourTime / oppTime, 1.0, 5.0)) / 6.5;
        
        // multiply the base remaining moves with all multipliers
        double result = expected * mobility * time + pawns + score;

        // clamp to reasonable range
        return Math.Clamp(result, 11.0, 45.0);
    }

    // depending on whether the position seems to be stable or unstable,
    // the time budget may be altered. instability is based on score
    // differences and best move changes between iterations
    internal static void AccountForInstability(double instability, int depth) {
        // don't touch the user-set budgets
        if (MoveTime != 0) return;

        long timeLeft = Game.EngineColor == Color.WHITE
            ? _whiteTime : _blackTime;

        if (timeLeft < 247) return;

        // for some reason it works well to have the shifts assymmetric.
        // if the instability is negative, we want to terminate the search
        // sooner, but if it's positive, we want to make the search longer
        long bonus = (long)(instability < 0
            ? instability * depth / 3.2d
            : instability * depth / 8.9d);

        TimeBudget += Math.Clamp(bonus, -1 - timeLeft / 403, 1 + timeLeft / 397);
    }
}