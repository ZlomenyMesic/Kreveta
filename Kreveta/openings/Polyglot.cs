//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

#pragma warning disable CA5394

using Kreveta.consts;
using Kreveta.movegen;
using Kreveta.uci;
using Kreveta.uci.options;

using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Kreveta.openings;

// polyglot is one of the most popular formats for opening books.
// these books usually have a .bin (binary) file extension, and
// are composed simply of entries stacked on top of each other
internal static partial class Polyglot {

    // yup this is truly one of the sizes in the world
    private const int EntrySize = 16;

    // the risk option is supposed to be a percentage
    private static float Risk => (float)Options.PolyglotRisk / 100;
    
    // tries to find a book move for the specified position
    internal static Move GetBookMove(in Board board) {
        ulong hash     = PolyglotZobristHash.Hash(in board);
        var   book     = LoadBook();
        var   possible = GetMovesForPosition(hash, [..book]);

        // no possible moves have been found
        if (possible.Length == 0) 
            return default;
        
        PolyglotEntry selection = SelectMove(possible);
        
        // this probably shouldn't happen but better safe than sorry
        if (selection == default)
            return default;
        
        UCI.Log($"info string selected move's weight: {selection.Weight}");
        return DecodeMove(board, selection);
    }

    // taken from:
    // https://chess.stackexchange.com/questions/28874/how-to-use-polyglot-opening-book-bin-file
    /*
     * A PolyGlot book move is encoded as follows:
     *
     * bit  0- 5: destination square (from 0 to 63)
     * bit  6-11: origin square (from 0 to 63)
     * bit 12-13-14: promotion piece (from KNIGHT == 1 to QUEEN == 4)
     *
     * Castling moves follow "king captures rook" representation. So in case
     * book move is a promotion we have to convert to our representation, in
     * all other cases we can directly compare with a Move after having
     * masked out special Move's flags that are not supported by PolyGlot.
     */
    private static Move DecodeMove(Board board, PolyglotEntry selection) {
        // extract the values as per the manual
        int end   = selection.Move       & 0x3F;
        int start = selection.Move >> 6  & 0x3F;
        int prom  = selection.Move >> 12 & 0xF;
        
        // polyglot squares are mirrored to what we use here,
        // so we keep the files and mirror the ranks to fix it
        start = (start & 7) + 8 * (7 - (start >> 3));
        end   = (end   & 7) + 8 * (7 - (end   >> 3));
        
        PType piece = board.PieceAt(start);
        PType capt  = board.PieceAt(end);

        // add en passant flag if the start and end files don't match
        if (piece == PType.PAWN && (start & 7) != (end & 7))
            prom = (int)PType.PAWN;

        // castling is encoded as "king capturing the rook",
        // so first, we must add the castling flag, and then
        // we must shift the destination square
        if (piece == PType.KING && start is 4 or 60) {
            switch (end) {
                // castling short/kingside
                case 7 or 63:
                    end--; prom = (int)PType.KING;
                    break;

                // castling long/queenside
                case 0 or 56:
                    end++; prom = (int)PType.KING;
                    break;
            }
        }
        
        return new Move(start, end, piece, capt, (PType)prom);
    }

    // this function chooses one of the possible moves based on risk and weights.
    // the idea is - with a higher risk value, we allow the engine to play "worse"
    // moves, but the probability of playing a lower-weight move is still smaller
    // compared to better moves. so increasing the risk only allows the engine to
    // play worse, but by no means is it forced
    private static PolyglotEntry SelectMove(ReadOnlySpan<PolyglotEntry> possibleMoves) {
        // first, the weights are normalized - this might not be necessary,
        // but it makes things easier to visualize. simultaneously, as described
        // above - moves with weights too low get immediately discarded
        var normalized = NormalizeWeights([..possibleMoves], out int max).Where(i => i.Weight >= 1 - Risk)
            .OrderByDescending(i => i.Weight).ToArray();
        
        /*
         * next, all normalized weights are summed, and a random number is generated.
         * based on "where it lands" between the summed weights, the move is selected:
         * |       W1       |       W2       |   W3   | W4 |W5|W6|
         * |                    ^ (random number)                |
         * so moves with higher weights forever keep a higher probability of being chosen
         */
        float sum        = normalized.Select(i => i.Weight).Sum();
        float random     = new Random().NextSingle() * sum;
        float sumCounter = 0f;

        for (int i = 0; i < normalized.Length; i++) {
            sumCounter += normalized[i].Weight;
            
            if (sumCounter > random)
                return normalized[i] 
                    // un-normalize the selected move's weight,
                    // so we can then print its origin state
                    with {Weight = max * normalized[i].Weight};
        }
        
        return default;
    }
    
    // normalizes all weights
    private static PolyglotEntry[] NormalizeWeights(PolyglotEntry[] possible, out int max) {
        float fmax = possible.Select(entry => entry.Weight).Prepend(0).Max();
        max = (int)fmax;

        // find the largest weight and divide all weights with it
        return possible.Select(i => i 
            with {Weight = i.Weight / fmax}).ToArray();
    }
    
    // returns all entries with keys matching to the zobrist hash
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ReadOnlySpan<PolyglotEntry> GetMovesForPosition(ulong hash, PolyglotEntry[] book)
        => book.Where(entry => entry.Key == hash).ToArray();

    // load all entries from the specified polyglot book
    private static ReadOnlySpan<PolyglotEntry> LoadBook() {
        if (!File.Exists(Options.PolyglotBook)) {
            UCI.Log($"Polyglot file not found: {Options.PolyglotBook}");
            return [];
        }
        
        // the data is just a continuous stream of bytes
        byte[] data    = File.ReadAllBytes(Options.PolyglotBook);
        int entryCount = data.Length / EntrySize;
        var entries    = new PolyglotEntry[entryCount];

        for (int i = 0; i < entryCount; i++) {
            int offset = i * EntrySize;
            
            // add the important data to the entry
            entries[i].Key    = BitConverter.ToUInt64(data.Skip(offset).Take(8).Reverse().ToArray(), 0);
            entries[i].Move   = BitConverter.ToUInt16(data.Skip(offset + 8).Take(2).Reverse().ToArray(), 0);
            entries[i].Weight = BitConverter.ToUInt16(data.Skip(offset + 10).Take(2).Reverse().ToArray(), 0);
            
            // what we skip here is book learning, thus the 16 byte entry size.
            // each entry in the book has extra 4 bytes for book learning
        }

        return entries;
    }
}

#pragma warning restore CA5394