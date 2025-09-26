//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

// Remove unnecessary suppression
#pragma warning disable IDE0079

// Initialize reference type static fields inline
#pragma warning disable CA1810

// Do not use insecure randomness
#pragma warning disable CA5394

using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace Kreveta.openingbook;

internal static class OpeningBook {
    internal static string BookMove { get; private set; }

    static OpeningBook() {
        OpeningsSplit = new string[Openings.Length][];
        BookMove      = string.Empty;

        for (int i = 0; i < Openings.Length; i++) {
            OpeningsSplit[i] = Openings[i].Split(' ');
        }
    }

    internal static void RegisterSequence(string[] sequence) {
        // reset the previous book move
        BookMove = string.Empty;

        // we have different lines for different colors
        Span<string[]> openings = OpeningsSplit;
        
#region STARTPOS

        // we are at the starting position, so we choose a random move to start with
        if (sequence.Length == 0) {

            // choose a random first move from the book
            BookMove = openings[
                new Random(Guid.NewGuid().GetHashCode()).Next(0, openings.Length)
            ][0];

            return;
        }
        
#endregion
#region OTHER

        List<string> possible = [];
        
        for (int i = 0; i < openings.Length; i++) {
            for (int j = 0; j < sequence.Length; j++) {

                // we found the move in the book
                if (sequence[j] == openings[i][j]) {

                    // our sequence is longer than the one in the book
                    if (j == openings[i].Length - 1)
                        break;

                    // we are at the end of our sequence but not
                    // past the end of the sequence saved in the book
                    if (j == sequence.Length - 1) {

                        // add the next move as a possibility. each
                        // move only gets added once to prevent similar
                        // lines from making some moves more frequent
                        if (!possible.Contains(openings[i][j + 1]))
                            possible.Add(openings[i][j + 1]);
                        
                        break;
                    } 
                } 
                
                // the book sequence isn't the same as our sequence
                else break;
            }
        }
        
#endregion        

        // we have at least one book move
        if (possible.Count != 0) {
            
            // choose randomly from our options
            BookMove = possible[
                new Random(Guid.NewGuid().GetHashCode()).Next(0, possible.Count)
            ];
        }
    }

    [ReadOnly(true)]
    private static readonly string[][] OpeningsSplit;

    [ReadOnly(true)]
    private static readonly string[] Openings = [
        
        // king's indian defense
        "d2d4 g8f6 c2c4 g7g6 b1c3 f8g7",
        "d2d4 g8f6 c2c4 g7g6 h2h4 f8g7",
        "d2d4 g8f6 c2c4 g7g6 g1f3 f8g7",
        
        "d2d4 g8f6 c2c4 c7c6 b1c3 d7d5",
        "d2d4 g8f6 c2c4 c7c6 g1f3 d7d5",
        "d2d4 g8f6 c2c4 c7c6 e2e3 d7d5",
        
        "d2d4 g8f6 c2c4 e7e6 g2g3 d7d5",
        "d2d4 g8f6 c2c4 e7e6 g1f3 d7d5",
        
        "d2d4 g8f6 g1f3 d7d5 c2c4 c7c6",
        "d2d4 g8f6 g1f3 d7d5 c2c4 e7e6",
        "d2d4 g8f6 g1f3 d7d5 c2c4 d5c4",
        
        "d2d4 g8f6 g1f3 c7c6 c2c4 d7d5",
        "d2d4 g8f6 g1f3 c7c6 e2e3 d7d5",
        
        // sicilian defense
        "e2e4 c7c5 g1f3 b8c6 d2d4 c5d4",
        "e2e4 c7c5 g1f3 b8c6 b1c3 e7e5",
        "e2e4 c7c5 g1f3 b8c6 f1b5 e7e6",
        
        "e2e4 c7c5 b1c3 b8c6 g1f3 g7g6",
        "e2e4 c7c5 b1c3 g7g6 d2d4 c5d4",
        "e2e4 c7c5 b1c3 e7e6 g1f3 b8c6",
        
        // french defense
        "e2e4 e7e6 d2d4 d7d5 e4d5 e6d5",
        "e2e4 e7e6 d2d4 d7d5 e4e5 c7c5",
        
        "e2e4 e7e6 d2d4 d7d5 b1c3 g8f6",
        "e2e4 e7e6 d2d4 d7d5 b1c3 f8b4",
        
        "e2e4 e7e6 d2d4 d7d5 b1d2 g8f6",
        "e2e4 e7e6 d2d4 d7d5 b1d2 c7c5",
        
        "e2e4 e7e6 b1c3 d7d5 g1f3 g8f6",
        "e2e4 e7e6 b1c3 d7d5 d2d4 f8b4",
        
        // italian game
        "e2e4 e7e5 g1f3 b8c6 f1c4 g8f6",
        "e2e4 e7e5 g1f3 b8c6 f1c4 f8c5",
        
        // scotch game
        "e2e4 e7e5 g1f3 b8c6 d2d4 e5d4",
        
        // berlin defense
        "e2e4 e7e5 f1c4 g8f6 d2d3 b8c6",
        "e2e4 e7e5 f1c4 g8f6 d2d3 c7c6",
        
        // ruy lopez
        "e2e4 e7e5 g1f3 b8c6 f1b5 a7a6",
        "e2e4 e7e5 g1f3 b8c6 f1b5 g8f6",
        "e2e4 e7e5 g1f3 b8c6 f1b5 g7g6",
        
        // king's gambit
        "e2e4 e7e5 f2f4 e5f4 g1f3 g7g5",
        "e2e4 e7e5 f2f4 d7d5 e4d5 e5f4",
        "e2e4 e7e5 f2f4 f8c5 g1f3 d7d6",
        
        // scandinavian defense
        "e2e4 d7d5 e4d5 d8d5 b1c3 d5d6",
        "e2e4 d7d5 e4d5 d8d5 b1c3 d5a5",
        
        "e2e4 d7d5 e4d5 g8f6 d2d4 f6d5",
        "e2e4 d7d5 e4d5 g8f6 f1b5 c8d7",
        
        // pirc defense
        "e2e4 d7d6 d2d4 g8f6 b1c3 g7g6",
        "e2e4 d7d6 d2d4 g8f6 b1c3 e7e5",
        "e2e4 d7d6 d2d4 g8f6 b1c3 c7c6",
        
        // vienna system
        "e2e4 e7e5 b1c3 b8c6 h2h3 f8c5",
        "e2e4 e7e5 b1c3 b8c6 h2h3 g8f6",
        "e2e4 e7e5 b1c3 b8c6 h2h3 g8e7",
        
        "e2e4 e7e5 b1c3 b8c6 g1f3 g8f6",
        
        "e2e4 e7e5 b1c3 b8c6 f1c4 g8f6",
        
        "e2e4 e7e5 b1c3 b8c6 g1e2 f8c5",
        "e2e4 e7e5 b1c3 b8c6 g1e2 g8f6",
        
        "e2e4 e7e5 b1c3 g8f6 g1f3 b8c6",
        
        "e2e4 e7e5 b1c3 g8f6 g2g3 d7d5",
        "e2e4 e7e5 b1c3 g8f6 g2g3 f8c5",
        
        // caro-kann defense
        "e2e4 c7c6 d2d4 d7d5 e4e5 c8f5",
        "e2e4 c7c6 d2d4 d7d5 e4d5 c6d5",
        "e2e4 c7c6 d2d4 d7d5 b1c3 d5e4",
        "e2e4 c7c6 d2d4 d7d5 b1d2 d5e4",
        
        "e2e4 c7c6 c2c4 d7d5 e4d5 g8f6",
        "e2e4 c7c6 c2c4 d7d5 e4d5 c6d5",
        
        "e2e4 c7c6 g1f3 d7d5 b1c3 c8g4",
        "e2e4 c7c6 g1f3 d7d5 d2d3 d5e4",
        
        "e2e4 c7c6 b1c3 d7d5 g1f3 c8g4",
        "e2e4 c7c6 b1c3 d7d5 g1f3 d5e4",
        
        // queen's gambit
        "d2d4 d7d5 c2c4 c7c6 g1f3 g8f6",
        "d2d4 d7d5 c2c4 c7c6 b1c3 g8f6",
        
        "d2d4 d7d5 c2c4 e7e6 b1c3 g8f6",
        "d2d4 d7d5 c2c4 e7e6 g1f3 g8f6",
        
        "d2d4 d7d5 c2c4 d5c4 g1f3 g8f6",
        "d2d4 d7d5 c2c4 d5c4 e2e4 e7e5",
        "d2d4 d7d5 c2c4 d5c4 e2e4 g8f6",
        
        "d2d4 d7d5 c2c4 d5c4 e2e3 g8f6",
        
        "d2d4 d7d5 g1f3 g8f6 c2c4 e7e6",
        
        // london system
        "d2d4 d7d5 g1f3 g8f6 c1f4 c7c5",
        
        "d2d4 d7d5 g1f3 g8f6 c1f4 e7e6",
        "d2d4 d7d5 g1f3 c7c6 c1f4 c8f5",
        
        "d2d4 d7d5 c1f4 g8f6 e2e3 c7c5",
        "d2d4 d7d5 c1f4 c7c5 e2e3 b8c6",
        
        "d2d4 d7d5 c1f4 e7e6 e2e3 f8d6",
        
        // english defense
        "d2d4 e7e6 c2c4 b7b6 e2e4 c8b7",
        "d2d4 e7e6 c2c4 b7b6 b1c3 c8b7",
        
        // wade defense
        "d2d4 d7d6 g1f3 c8g4 c2c4 b8d7",
        "d2d4 d7d6 g1f3 c8g4 e2e4 g8f6",
        
        // english opening
        "c2c4 g8f6 b1c3 g7g6 g2g3 f8g7",
        "c2c4 g8f6 b1c3 g7g6 e2e4 d7d6",
        
        "c2c4 g8f6 b1c3 e7e6 e2e4 d7d5",
        "c2c4 g8f6 b1c3 e7e6 g1f3 d7d5",
        "c2c4 g8f6 b1c3 e7e5 g1f3 b8c6",
        
        "c2c4 e7e5 b1c3 g8f6 g1f3 b8c6",
        
        "c2c4 e7e5 g2g3 g8f6 f1g2 d7d5",
        "c2c4 e7e5 g2g3 g8f6 f1g2 c7c6",
        "c2c4 e7e5 g2g3 b8c6 f1g2 g7g6",
        
        "c2c4 e7e6 g1f3 d7d5 g2g3 g8f6",
        "c2c4 e7e6 g1f3 g8f6 b1c3 d7d5",
        "c2c4 e7e6 b1c3 d7d5 c4d5 e6d5",
        
        "c2c4 c7c5 g1f3 g8f6 b1c3 b8c6",
        "c2c4 c7c5 g2g3 g7g6 f1g2 f8g7",
        
        // dutch defense
        "d2d4 f7f5 g2g3 g8f6 f1g2 g7g6",
        "d2d4 f7f5 c2c4 g8f6 b1c3 g7g6",
        "d2d4 f7f5 c2c4 g8f6 g2g3 g7g6",
        "d2d4 f7f5 g1f3 g8f6 g2g3 e7e6",
        
        // king's indian attack
        "g1f3 g8f6 g2g3 g7g6 f1g2 f8g7",
        "g1f3 g8f6 g2g3 d7d5 f1g2 c7c6",
        
        // van geet opening
        "b1c3 d7d5 d2d4 g8f6 c1f4 a7a6",
        "b1c3 c7c5 g1f3 b8c6 e2e4 g7g6",
        "b1c3 c7c6 e2e4 d7d5 g1f3 c8g4",
        "b1c3 g8f6 e2e4 e7e5 g2g3 d7d5",
        "b1c3 b8c6 e2e4 e7e5 f1c4 g8f6",
        
        // nimzowitsch-larsen attack
        "b2b3 e7e5 c1b2 b8c6 e2e3 g8f6",
        "b2b3 e7e5 c1b2 b8c6 e2e3 d7d5",
        
        "b2b3 d7d5 c1b2 g8f6 g1f3 c8f5",
        "b2b3 d7d5 c1b2 g8f6 g1f3 e7e6",
        
        "b2b3 g8f6 c1b2 g7g6 g1f3 f8g7",
        
        "b2b3 c7c5 c1b2 b8c6 c2c4 e7e5",
    ];
}

#pragma warning disable CA5394
#pragma warning restore CA1810

#pragma warning restore IDE0079