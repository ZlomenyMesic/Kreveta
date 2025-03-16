/*
 *  Stockshrimp chess engine 1.0
 *  developed by ZlomenyMesic
 */

using Stockshrimp_1.movegen;

namespace Stockshrimp_1.search.movesort;

internal static class Killers {
    private static Move[] killers = [];

    private static int depth = 0;

    // number of saved killers per ply/depth
    private const int WIDTH = 7;

    internal static void Expand(int depth) {
        Killers.depth = Math.Max(Killers.depth, depth);
        Array.Resize(ref killers, Killers.depth * WIDTH);
    }

    internal static void Clear() {
        depth = 4;
        killers = [];
    }

    internal static void Add(Move move, int depth) {
        int index0 = WIDTH * (Killers.depth - depth);

        lock (killers) {
            //We shift all moves by one slot to make room but overwrite a potential duplicate of 'move' then store the new 'move' at [0] 
            int last = index0;
            for (; last < index0 + WIDTH - 1; last++)
                if (killers[last] == move) //if 'move' is present we want to overwrite it instead of the the one at [_width-1]
                    break;

            //2. start with last slot and 'save' the previous values until the first slot got dublicated
            for (int index = last; index >= index0; index--)
                killers[index] = killers[index - 1];

            //3. store new 'move' in the first slot
            killers[index0] = move;
        }
    }

    internal static Move[] Get(int depth) {

        Move[] line = new Move[WIDTH];
        int index0 = WIDTH * (Killers.depth - depth);
        Array.Copy(killers, index0, line, 0, WIDTH);

        return line;
    }
}
