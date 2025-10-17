#pragma warning disable CA1304
#pragma warning disable CA1311

using System;
using System.Text;
using Kreveta.consts;

namespace Kreveta.syzygy;

internal static class Syzygy {
    internal static bool TryGetScore(Board board, out short score) {
        score = 0;

        if (!TryGetFileName(board, out string fileName))
            return false;

        ulong index = SyzygyIndexer.ComputeIndex(board);
        
        Console.WriteLine(fileName);
        Console.WriteLine(index);
        
        return true;
    }

    // returns the name of the board's respective file
    internal static bool TryGetFileName(Board board, out string fileName) {
        fileName = string.Empty;
        
        //
        // RISKY AS FUCK!!!!!!!!
        //
        // this might work as an optimization IF WE KNOW how large
        // our tablebase is, otherwise we would overlook larger files
        if (ulong.PopCount(board.Occupied) > 5)
            return false;
        //
        //
        //
        
        var white = new StringBuilder();
        var black = new StringBuilder();
        
        // loop over all pieces
        ReadOnlySpan<ulong> pieces = board.Pieces;
        for (int i = 5; i >= 0; i--) {
            
            char c = Consts.Pieces[i];
            white.Append(new string(c, (int)ulong.PopCount(board.Pieces[i])));
            black.Append(new string(c, (int)ulong.PopCount(board.Pieces[6 + i])));
        }
        
        fileName = white.ToString().ToUpper() + 'v' + black.ToString().ToUpper();
        
        // TODO - CHECK IF THE FILE EXISTS IN SYZYGY PATH (OPTIONS)
        return true;
    }
}

#pragma warning restore CA1311
#pragma warning restore CA1304