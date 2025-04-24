//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.movegen;

namespace Kreveta.search.moveorder;

internal static class Killers {
    private static Move[] _killers = [];

    private static int Depth;

    // number of saved killers per ply/depth
    private const int CapacityPerPly = 7;

    internal static void Expand(int depth) {
        Depth = Math.Max(Depth, depth);
        Array.Resize(ref _killers, Depth * CapacityPerPly);
    }

    internal static void Clear() {
        _killers = [];
        Depth   = 4;
    }

    internal static void Add(Move move, int depth) {
        int index0 = CapacityPerPly * (Depth - depth);

        lock (_killers) {
            //We shift all moves by one slot to make room but overwrite a potential duplicate of 'move' then store the new 'move' at [0] 
            int last = index0;
            for (; last < index0 + CapacityPerPly - 1; last++)
                if (_killers[last] == move) //if 'move' is present we want to overwrite it instead of the the one at [_width-1]
                    break;

            //2. start with last slot and 'save' the previous values until the first slot got dublicated
            for (int index = last; index >= index0; index--)
                _killers[index] = _killers[index - 1];

            //3. store new 'move' in the first slot
            _killers[index0] = move;
        }
    }

    internal static Move[] Get(int depth) {

        Move[] line = new Move[CapacityPerPly];
        int index0 = CapacityPerPly * (Depth - depth);
        Array.Copy(_killers, index0, line, 0, CapacityPerPly);

        return line;
    }
}
