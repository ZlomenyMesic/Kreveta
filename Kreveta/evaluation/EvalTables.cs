//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

using Kreveta.consts;

using System.Runtime.CompilerServices;

namespace Kreveta.evaluation;

internal static class EvalTables {

    // this method uses the value tables in EvalTables.cs, and is used to evaluate the position of a piece.
    // there are two tables - midgame and endgame, which is important, because different pieces should be
    // in different positions as the game progresses (e.g. a king in the midgame should be in the corner,
    // but should move towards the center in the endgame)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static unsafe short GetTableValue(byte type, Color col, byte sq, int phase) {

        // we have to index the piece type and position correctly. white
        // pieces are straightforward, but black piece have to be mirrored
        short index = (short)(type * 64 + (col == Color.WHITE
            ? sq ^ 56   // flip the square to the opposite side
            : sq ^ 7)); // flip only the file

        // we grab both the midgame and endgame table values
        fixed (short* midgame = &Middlegame[0],
                      endgame = &Endgame[0]) {

            // a very rough attempt for tapering evaluation - instead of
            // just switching straight from midgame into endgame, the table
            // value of the piece is always somewhere in between, based on
            // the number of pieces left on the board.
            return (short)(*(midgame + index) * phase / 100
                         + *(endgame + index) * (100 - phase) / 100);
        }
    }

    // rough estimate of piece values. used in MVV-LVA capture ordering
    // to determine good captures, and also in delta pruning to create
    // a safe margin using the captured piece
    internal static readonly short[] PieceValues = [100, 315, 330, 520, 930, 10000, -1];

    #region MIDDLEGAME

    private static readonly short[] Middlegame =
    [ // pawns
      100,  100,  100,  100,  100,  100,  100,  100,
      104,  109,  105,  90,   90,   101,  102,  104,
      106,  108,  97,   106,  104,  104,  101,  110,
      90,   96,   90,   124,  124,  115,  98,   95,
      90,   93,   95,   112,  111,  102,  98,   94,
      90,   95,   97,   106,  105,  100,  99,   96,
      100,  105,  104,  103,  103,  104,  105,  100,
      100,  100,  100,  100,  100,  100,  100,  100,
      // knights
      275,  290,  290,  285,  285,  290,  290,  275,
      281,  295,  307,  310,  310,  307,  295,  281,
      283,  309,  312,  311,  311,  310,  307,  283,
      295,  308,  310,  310,  310,  310,  308,  295,
      295,  301,  308,  304,  304,  308,  301,  295,
      295,  295,  303,  300,  300,  303,  295,  295,
      285,  295,  295,  300,  300,  295,  295,  285,
      275,  285,  295,  295,  295,  295,  285,  275,
      // bishops
      299,  300,  302,  305,  305,  302,  300,  298,
      313,  344,  315,  313,  313,  315,  333,  313,
      321,  331,  328,  302,  300,  328,  331,  321,
      325,  332,  334,  327,  327,  338,  333,  327,
      318,  322,  321,  322,  322,  321,  322,  318,
      315,  315,  316,  320,  320,  316,  310,  315,
      310,  312,  311,  313,  313,  311,  312,  310,
      305,  305,  307,  308,  308,  307,  305,  305,
      // rooks
      495,  504,  521,  529,  529,  521,  504,  495,
      496,  502,  507,  515,  515,  507,  502,  496,
      485,  510,  505,  507,  507,  505,  510,  485,
      485,  475,  475,  495,  495,  475,  475,  485,
      480,  475,  470,  475,  475,  470,  475,  480,
      475,  475,  480,  480,  480,  480,  475,  475,
      485,  485,  485,  480,  480,  485,  485,  485,
      500,  495,  490,  495,  495,  490,  495,  500,
      // queens
      895,  900,  920,  929,  929,  920,  900,  895,
      910,  905,  913,  924,  924,  913,  905,  910,
      900,  901,  903,  910,  910,  903,  901,  900,
      898,  905,  905,  905,  905,  905,  905,  898,
      900,  900,  900,  903,  903,  900,  900,  900,
      920,  925,  910,  900,  900,  910,  925,  903,
      905,  910,  915,  915,  915,  915,  910,  905,
      905,  900,  910,  915,  915,  910,  900,  905,
      // kings
       25,   70,   29,   13,   8,    17,   58,   21,
       0,   -3,   -2,   -29,  -15,   0,    5,    0,
      -10,  -5,   -15,  -25,  -25,  -15,  -5,   -10,
      -30,  -25,  -30,  -35,  -35,  -30,  -25,  -30,
      -45,  -30,  -45,  -60,  -60,  -45,  -30,  -45,
      -25,  -25,  -60,  -75,  -75,  -60,  -25,  -25,
      -15,  -20,  -25,  -70,  -70,  -25,  -20,  -15,
      -5,   -15,  -20,  -35,  -35,  -20,  -15,   -5
    ];
#endregion
    
    #region ENDGAME
    private static readonly short[] Endgame =
    [ // pawns
      100,  100,  100,  100,  100,  100,  100,  100,
      75,   75,   75,   70,   70,   75,   75,   75,
      85,   85,   80,   75,   75,   80,   85,   85,
      105,  105,  105,  100,  100,  105,  105,  105,
      123,  123,  116,  115,  115,  116,  123,  123,
      169,  158,  146,  144,  144,  146,  158,  169,
      210,  208,  203,  197,  197,  203,  208,  210,
      100,  100,  100,  100,  100,  100,  100,  100,
      // knights
      230,  230,  235,  240,  240,  235,  230,  230,
      230,  240,  245,  245,  245,  245,  240,  230,
      235,  245,  255,  260,  260,  255,  245,  235,
      240,  270,  280,  285,  285,  280,  270,  240,
      245,  270,  280,  285,  285,  280,  270,  245,
      245,  255,  265,  270,  270,  265,  255,  245,
      245,  250,  250,  255,  255,  250,  250,  245,
      245,  245,  250,  245,  245,  250,  245,  245,
      // bishops
      280,  280,  285,  290,  290,  285,  280,  280,
      280,  285,  290,  295,  295,  290,  285,  280,
      290,  285,  285,  290,  290,  285,  285,  290,
      280,  285,  290,  295,  295,  290,  285,  280,
      280,  285,  295,  300,  300,  295,  285,  280,
      280,  285,  285,  295,  295,  285,  285,  280,
      285,  280,  285,  290,  290,  285,  280,  285,
      280,  280,  285,  290,  290,  285,  280,  280,
      // rooks
      505,  510,  510,  505,  505,  510,  510,  505,
      505,  510,  515,  510,  510,  515,  510,  505,
      510,  515,  510,  505,  505,  510,  515,  510,
      505,  510,  505,  500,  500,  505,  510,  505,
      505,  510,  505,  500,  500,  505,  510,  505,
      510,  515,  510,  505,  505,  510,  515,  510,
      505,  510,  515,  510,  510,  515,  510,  505,
      505,  510,  510,  505,  505,  510,  510,  505,
      // queens
      910,  910,  910,  910,  910,  910,  910,  910,
      910,  915,  915,  915,  915,  915,  915,  910,
      910,  915,  920,  925,  925,  920,  915,  910,
      910,  915,  925,  935,  935,  925,  915,  910,
      910,  915,  925,  935,  935,  925,  915,  910,
      910,  915,  920,  925,  925,  920,  915,  910,
      910,  915,  915,  915,  915,  915,  915,  910,
      910,  910,  910,  910,  910,  910,  910,  910,
      // kings
      -70,  -35,  -20,  -10,  -10,  -20,  -35,  -70,
      -35,  -20,  -10,   0,    0,   -10,  -20,  -35,
      -20,  -10,   0,    5,    5,    0,   -10,  -20,
      -10,   0,    5,    25,   25,   5,    0,   -10,
      -10,  -0,    5,    25,   25,   5,    0,   -10,
      -20,  -10,   0,    5,    5,    0,   -10,  -20,
      -35,  -20,  -10,   0,    0,   -10,  -20,  -35,
      -70,  -35,  -20,  -10,  -10,  -20,  -35,  -70
    ];

    #endregion
}
