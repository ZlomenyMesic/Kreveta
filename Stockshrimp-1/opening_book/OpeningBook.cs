/*
 *  Stockshrimp chess engine 1.0
 *  developed by ZlomenyMesic
 */

namespace Stockshrimp_1.opening_book;

internal static class OpeningBook {
    internal static string book_move = "";

    internal static void SaveSequence(string[] sequence, string fen) {

        // reset the previous book move
        book_move = "";

        // only look for book moves from starting position.
        // (might implement other ones later)
        if (fen != Consts.STARTPOS_FEN.Split()[0])
            return;

        List<string> possible = [];

        // we are currently at the starting position
        if (sequence.Length == 0) {
            foreach (string[] opening in BOOK)
                possible.Add(opening[0]);

            // choose a random first move from the book
            book_move = possible[new Random().Next(0, possible.Count)];
            return;
        }

        for (int i = 0; i < BOOK.Length; i++) {
            for (int j = 0; j < sequence.Length; j++) {

                // we found the move in the book
                if (sequence[j] == BOOK[i][j]) {

                    // we are at the end of our sequence but not
                    // at the end of the sequence saved in the book
                    if (j == sequence.Length - 1 && j < BOOK[i].Length - 1) {

                        // add the next move as a possibility
                        possible.Add(BOOK[i][j + 1]);
                        break;
                    } 
                    
                    // our sequence is longer than the one in the book
                    else if (j == BOOK[i].Length - 1) break;
                } 
                
                // the book sequence isn't the same as our sequence
                else break;
            }
        }

        // we have at least one book move
        if (possible.Count > 0) {
            book_move = possible[new Random().Next(0, possible.Count)];
        }
    }

    private static readonly string[][] BOOK = [
        ["e2e4", "e7e5", "g1f3", "b8c6", "f1c4"],

        ["e2e4", "e7e5", "g1f3", "b8c6", "f1b5"],

        ["d2d4", "d7d5"]
    ];
}
