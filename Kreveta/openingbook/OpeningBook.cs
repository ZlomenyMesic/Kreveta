//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

// Remove unnecessary suppression
#pragma warning disable IDE0079

// Do not use insecure randomness
#pragma warning disable CA5394

using Kreveta.consts;

using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace Kreveta.openingbook;

internal static class OpeningBook {

    internal static string BookMove = string.Empty;

// Initialize reference type static fields inline
#pragma warning disable CA1810

    static OpeningBook() {

#pragma warning restore CA1810

        WhiteOpeningsSplit = new string[WhiteOpenings.Length][];
        BlackOpeningsSplit = new string[BlackOpenings.Length][];

        for (int i = 0; i < WhiteOpenings.Length; i++) {
            WhiteOpeningsSplit[i] = WhiteOpenings[i].Split(' ');
        }
        
        for (int i = 0; i < BlackOpenings.Length; i++) {
            BlackOpeningsSplit[i] = BlackOpenings[i].Split(' ');
        }
    }

    internal static void SaveSequence(string[] sequence) {

        // reset the previous book move
        BookMove = string.Empty;

        // we have different lines for different colors
        Span<string[]> openings = Game.EngineColor == Color.WHITE 
            ? WhiteOpeningsSplit 
            : BlackOpeningsSplit;
        
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
        if (possible.Count > 0) {
            
            // choose randomly from our options
            BookMove = possible[
                new Random(Guid.NewGuid().GetHashCode()).Next(0, possible.Count)
            ];
        }
    }

    [ReadOnly(true)]
    private static readonly string[][] WhiteOpeningsSplit;
    
    [ReadOnly(true)]
    private static readonly string[][] BlackOpeningsSplit;
    
    [ReadOnly(true)]
    private static readonly string[] WhiteOpenings = [
        
        // king's indian defense
        "d2d4 g8f6 c2c4 g7g6 b1c3 f8g7 e2e4 d7d6 g1f3 e8g8 f1e2",
        "d2d4 g8f6 c2c4 g7g6 b1c3 f8g7 e2e4 d7d6 f1e2 e8g8 c1g5",
        "d2d4 g8f6 c2c4 g7g6 b1c3 f8g7 e2e4 d7d6 f2f3 e8g8 c1e3",
        
        "d2d4 g8f6 c2c4 g7g6 b1c3 f8g7 g2g3 e8g8 f1g2 d7d6 g1f3",
        "d2d4 g8f6 c2c4 g7g6 g2g3 f8g7 f1g2 e8g8 g1f3 d7d6 e1g1",
        "d2d4 g8f6 c2c4 g7g6 b1c3 f8g7 g1f3 e8g8 g2g3 d7d6 f1g2",
        
        "d2d4 g8f6 c2c4 g7g6 b1c3 f8g7 g1f3 e8g8 c1g5 d7d6 e2e3",
        
        // sicilian defense
        "e2e4 c7c5 g1f3 b8c6 d2d4 c5d4 f3d4 e7e6 b1c3 g8f6 a2a3",
        "e2e4 c7c5 g1f3 b8c6 b1c3 e7e5 f1c4 d7d6 d2d3 h7h6 c3d5",
        "e2e4 c7c5 g1f3 b8c6 f1b5 e7e6 e1g1 g8e7 f1e1 b7b6 d2d4",
        "e2e4 c7c5 g1f3 b8c6 f1b5 e7e6 e1g1 g8e7 d2d4 c5d4 f3d4",
        
        // french defense
        "e2e4 e7e6 d2d4 d7d5 e4d5 e6d5 g1f3 g8f6 f1d3 f8d6 e1g1",
        "e2e4 e7e6 d2d4 d7d5 e4d5 e6d5 g1f3 f8d6 f1d3 g8e7 e1g1",

        "e2e4 e7e6 d2d4 d7d5 e4e5 c7c5 c2c3 b8c6 g1f3 d8b6 a2a3",
        "e2e4 e7e6 d2d4 d7d5 e4e5 c7c5 c2c3 b8c6 g1f3 d8b6 f1d3",

        "e2e4 e7e6 d2d4 d7d5 b1c3 g8f6 e4e5 f6d7 f2f4 c7c5 g1f3",
        "e2e4 e7e6 d2d4 d7d5 b1c3 g8f6 c1g5 d5e4 c3e4 f8e7 g5f6",

        "e2e4 e7e6 d2d4 d7d5 b1c3 f8b4 e4e5 c7c5 a2a3 b4c3 b2c3",

        "e2e4 e7e6 d2d4 d7d5 b1d2 g8f6 e4e5 f6d7 f1d3 c7c5 c2c3",
        "e2e4 e7e6 d2d4 d7d5 b1d2 c7c5 e4d5 e6d5 g1f3 b8c6 f1b5",
        
        // italian game
        "e2e4 e7e5 g1f3 b8c6 f1c4 g8f6 f3g5 d7d5 e4d5 c6a5 c4b5",
        "e2e4 e7e5 g1f3 b8c6 f1c4 g8f6 d2d3 a7a6 b1c3 f8c5 e1g1",
        "e2e4 e7e5 g1f3 b8c6 f1c4 g8f6 d2d3 h7h6 c2c3 f8c5 b2b4",

        "e2e4 e7e5 g1f3 b8c6 f1c4 f8c5 c2c3 g8f6 d2d3 a7a5 e1g1",
        
        // ruy lopez
        "e2e4 e7e5 g1f3 b8c6 f1b5 g8f6 e1g1 f6e4 f1e1 e4d6 f3e5",
        "e2e4 e7e5 g1f3 b8c6 f1b5 g8f6 d2d3 f8c5 c2c3 e8g8 e1g1",

        // scandinavian
        "e2e4 d7d5 e4d5 d8d5 b1c3 d5d6 g1f3 g8f6 d2d4 c7c6 g2g3",

        // pirc defense
        "e2e4 d7d6 d2d4 e7e5 g1f3 e5d4 f3d4 g8f6 b1c3 f8e7 c1f4",

        // vienna
        "e2e4 e7e5 b1c3 b8c6 h2h3",
        "e2e4 e7e5 b1c3 b8c6 g1f3",
        "e2e4 e7e5 b1c3 b8c6 f1c4",
        "e2e4 e7e5 b1c3 b8c6 g1e2",

        "e2e4 e7e5 b1c3 g8f6 g1f3",
        "e2e4 e7e5 b1c3 g8f6 g2g3",

        // caro-kann
        "e2e4 c7c6 d2d4 d7d5 e4e5 c1f5",
        "e2e4 c7c6 d2d4 d7d5 e4d5 c6d5",
        "e2e4 c7c6 d2d4 d7d5 b1c3 d5e4",
        "e2e4 c7c6 d2d4 d7d5 b1d2 d5e4",

        "e2e4 c7c6 c2c4 d7d5 e4d5 g8f6",
        "e2e4 c7c6 c2c4 d7d5 e4d5 c6d5",

        // van t kruijs opening
        "e2e3 g8f6 g1f3",

        // saragossa
        "c2c3",

        // english
        "c2c4",
    ];
    
    [ReadOnly(true)]
    private static readonly string[] BlackOpenings = [
        
        // king's indian defense
        "d2d4 g8f6 c2c4 g7g6 b1c3 f8g7 e2e4 d7d6 g1f3 e8g8 f1e2 e7e5",
        
        "d2d4 g8f6 c2c4 g7g6 b1c3 f8g7 e2e4 d7d6 f1e2 e8g8 c1g5 c7c5",
        "d2d4 g8f6 c2c4 g7g6 b1c3 f8g7 e2e4 d7d6 f1e2 e8g8 c1g5 a7a6",
        
        "d2d4 g8f6 c2c4 g7g6 b1c3 f8g7 e2e4 d7d6 f2f3 e8g8 c1e3 a7a6",

        "d2d4 g8f6 c2c4 g7g6 b1c3 f8g7 g2g3 e8g8 f1g2 d7d6 g1f3 b8c6",
        "d2d4 g8f6 c2c4 g7g6 g2g3 f8g7 f1g2 e8g8 g1f3 d7d6 e1g1 b8d7",
        "d2d4 g8f6 c2c4 g7g6 b1c3 f8g7 g1f3 e8g8 g2g3 d7d6 f1g2 c8g4",
        
        "d2d4 g8f6 c2c4 g7g6 b1c3 f8g7 g1f3 e8g8 c1g5 d7d6 e2e3 h7h6",
        
        "d2d4 g8f6 c2c4 g7g6 b1c3 f8g7 e2e4 d7d6 f2f4 e8g8 g1f3 c7c5",
        
        "d2d4 g8f6 c2c4 g7g6 b1c3 f8g7 e2e4 d7d6 f1e2 e8g8 c1e3 a7a6",
        
        "d2d4 g8f6 c2c4 g7g6 b1c3 f8g7 e2e4 e8g8 e4e5 f6e8 f2f4 d7d6",
        
        // sicilian defense
        "e2e4 c7c5 g1f3 b8c6 d2d4 c5d4 f3d4 e7e6 b1c3 g8f6 a2a3 d7d6",
        "e2e4 c7c5 g1f3 b8c6 b1c3 e7e5 f1c4 d7d6 d2d3 h7h6 c3d5 g8f6",
        "e2e4 c7c5 g1f3 b8c6 f1b5 e7e6 e1g1 g8e7 f1e1 b7b6 d2d4 c5d4",
        "e2e4 c7c5 g1f3 b8c6 f1b5 e7e6 e1g1 g8e7 d2d4 c5d4 f3d4 e7g6",

        "e2e4 c7c5 c2c3 d7d5 e4d5 d8d5 d2d4 g8f6 g1f3 b8c6 c1e3 c8g4",
        "e2e4 c7c5 c2c3 g8f6 e4e5 f6d5 d2d4 c5d4 c3d4 d7d6 g1f3 b8c6",

        "e2e4 c7c5 d2d4 c5d4 c2c3 d4c3 b1c3 b8c6 g1f3 d7d6 f1c4 a7a6",

        "e2e4 c7c5 b1c3 b8c6 f2f4 g7g6 a2a4 f8g7 g1f3 b7b6 f1e2 g8h6",
        
        // french defense
        "e2e4 e7e6 d2d4 d7d5 e4d5 e6d5 g1f3 g8f6 f1d3 f8d6 e1g1 e8g8",
        "e2e4 e7e6 d2d4 d7d5 e4d5 e6d5 g1f3 f8d6 f1d3 g8e7 e1g1 e8g8",

        "e2e4 e7e6 d2d4 d7d5 e4e5 c7c5 c2c3 b8c6 g1f3 d8b6 a2a3 c7c5",
        "e2e4 e7e6 d2d4 d7d5 e4e5 c7c5 c2c3 b8c6 g1f3 d8b6 f1d3 c8d7",

        "e2e4 e7e6 d2d4 d7d5 b1c3 g8f6 e4e5 f6d7 f2f4 c7c5 g1f3 b8c6",
        "e2e4 e7e6 d2d4 d7d5 b1c3 g8f6 c1g5 d5e4 c3e4 f8e7 g5f6 e7f6",

        "e2e4 e7e6 d2d4 d7d5 b1c3 f8b4 e4e5 c7c5 a2a3 b4c3 b2c3 g8e7",

        "e2e4 e7e6 d2d4 d7d5 b1d2 g8f6 e4e5 f6d7 f1d3 c7c5 c2c3 b8c6",
        "e2e4 e7e6 d2d4 d7d5 b1d2 c7c5 e4d5 e6d5 g1f3 b8c6 f1b5 f8d6",
        
        // italian game
        "e2e4 e7e5 g1f3 b8c6 f1c4 g8f6 f3g5 d7d5 e4d5 c6a5 c4b5 c7c6",
        "e2e4 e7e5 g1f3 b8c6 f1c4 g8f6 d2d3 a7a6 b1c3 f8c5 e1g1 d7d6",
        "e2e4 e7e5 g1f3 b8c6 f1c4 g8f6 d2d3 h7h6 c2c3 f8c5 b2b4 c5b6",
        "e2e4 e7e5 g1f3 b8c6 f1c4 g8f6 d2d4 e5d4 e4e5 f6e4 e1g1 f8e7",

        "e2e4 e7e5 g1f3 b8c6 f1c4 f8c5 c2c3 g8f6 d2d3 a7a5 e1g1 d7d6",
        "e2e4 e7e5 g1f3 b8c6 f1c4 g8f6 e1g1 f6e4 c4d5 e4f6 d5c6 d7c6",
        
        // ruy lopez
        "e2e4 e7e5 g1f3 b8c6 f1b5 g8f6 e1g1 f6e4 f1e1 e4d6 f3e5 f8e7",
        "e2e4 e7e5 g1f3 b8c6 f1b5 g8f6 d2d3 f8c5 c2c3 e8g8 e1g1 d7d5",

        // vienna
        "e2e4 e7e5 b1c3 b8c6 h2h3",
        "e2e4 e7e5 b1c3 b8c6 g1f3",
        "e2e4 e7e5 b1c3 b8c6 f1c4",
        "e2e4 e7e5 b1c3 b8c6 g1e2",

        "e2e4 e7e5 b1c3 g8f6 g1f3",
        "e2e4 e7e5 b1c3 g8f6 g2g3",

        // caro-kann
        "e2e4 c7c6 d2d4 d7d5 e4e5 c1f5",
        "e2e4 c7c6 d2d4 d7d5 e4d5 c6d5",
        "e2e4 c7c6 d2d4 d7d5 b1c3 d5e4",
        "e2e4 c7c6 d2d4 d7d5 b1d2 d5e4",

        "e2e4 c7c6 c2c4 d7d5 e4d5 g8f6",
        "e2e4 c7c6 c2c4 d7d5 e4d5 c6d5",

        // van t kruijs opening
        "e2e3 g8f6 g1f3",

        // saragossa
        "c2c3",

        // english
        "c2c4",
    ];

    
    // "e2e4 d7d5",
    // "e2e4 e7e6",
    // "e2e4 c7c5",
    // "e2e4 e7e5",
    // "e2e4 b8c6",
    // "e2e4 c7c6",
    // "e2e4 g8f6",
    //
    // "e2e3 g8f6",
    //
    // "g1f3 d7d5",
    //
    // "b1c3 e7e5",
    //
    // "c2c4 g8f6",
    //
    // "d2d4 g8f6",
    // "d2d4 c7c6",
    //
    // "c2c3",
    // "c2c4",
    //
    // "d2d3",
    //
    // "d2d4 d7d5 g1f3",
    // "d2d4 d7d5 c1f4",
    // "d2d4 d7d5 c2c4",
    //
    // "g1f3 g8f6",
    //
    // "g2g3",
    //
    // "d2d4 e7e6",
    // "d2d4 g7g6",
}

#pragma warning restore IDE0079