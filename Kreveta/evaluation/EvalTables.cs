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
    internal static unsafe short GetTableValue(byte type, Color col, byte sq, int pieceCount) {

        // we have to index the piece type and position correctly. white
        // pieces are straightforward, but black piece have to be mirrored
        short index = (short)(type * 64 + (col == Color.WHITE
            ? sq ^ 7 ^ 56 // flip the square to the opposite side
            : sq ^ 7));   // flip only the file

        // we grab both the midgame and endgame table values
        fixed (short* midgame = &Middlegame[0],
                      endgame = &Endgame[0]) {

            // a very rough attempt for tapering evaluation - instead of
            // just switching straight from midgame into endgame, the table
            // value of the piece is always somewhere in between, based on
            // the number of pieces left on the board.
            return (short)(*(midgame + index) * pieceCount        / 32
                         + *(endgame + index) * (32 - pieceCount) / 32);
        }
    }

    // rough estimate of piece values. used in MVV-LVA capture ordering
    // to determine good captures, and also in delta pruning to create
    // a safe margin using the captured piece
    internal static readonly short[] PieceValues = [108, 319, 330, 520, 924, 10000, -1];

#region MIDDLEGAME
    internal static readonly short[] Middlegame = 
    [ // pawns
    100,  100,  100,  100,  100,  100,  100,  100,
    104,  109,  106,   90,   90,  101,  102,  104,
    106,  109,   97,  106,  104,  103,  101,  110,
     90,   96,   90,  124,  125,  115,   98,   95,
     89,   93,   95,  112,  111,  102,   97,   93,
     91,   95,   98,  106,  105,  100,   99,   96,
    100,  104,  105,  103,  102,  104,  105,  100,
    100,  100,  100,  100,  100,  100,  100,  100,
    // knights
    275,  290,  291,  285,  284,  290,  290,  275,
    280,  295,  308,  310,  310,  306,  295,  282,
    283,  309,  312,  312,  310,  310,  306,  282,
    295,  308,  310,  311,  310,  310,  308,  296,
    295,  300,  309,  305,  305,  309,  301,  295,
    295,  296,  303,  300,  300,  304,  296,  294,
    285,  295,  295,  300,  300,  295,  295,  285,
    275,  285,  296,  295,  296,  295,  285,  275,
    // bishops
    299,  300,  302,  304,  305,  302,  300,  298,
    313,  344,  315,  313,  314,  316,  332,  313,
    321,  331,  327,  302,  299,  327,  331,  322,
    325,  332,  333,  327,  327,  338,  333,  327,
    319,  322,  322,  322,  321,  321,  322,  317,
    314,  315,  315,  320,  320,  316,  310,  315,
    310,  311,  311,  313,  313,  311,  312,  311,
    304,  305,  307,  309,  308,  307,  305,  304,
    // rooks
    494,  504,  521,  529,  529,  521,  504,  495,
    496,  502,  508,  515,  515,  507,  502,  496,
    484,  510,  505,  507,  507,  505,  510,  485,
    485,  475,  476,  495,  496,  475,  475,  485,
    479,  475,  470,  475,  475,  470,  475,  479,
    476,  475,  479,  480,  480,  479,  474,  475,
    485,  485,  485,  480,  480,  486,  485,  485,
    500,  494,  490,  496,  495,  490,  495,  500,
    // queens
    896,  900,  920,  929,  930,  919,  900,  895,
    911,  906,  913,  925,  924,  913,  906,  910,
    899,  901,  902,  910,  911,  904,  901,  901,
    897,  904,  904,  905,  905,  904,  905,  898,
    901,  901,  900,  903,  903,  901,  899,  901,
    919,  925,  911,  900,  900,  910,  925,  903,
    905,  911,  914,  915,  915,  915,  911,  906,
    904,  901,  910,  915,  915,  910,  900,  906,
    // kings
     25,   71,   29,   13,    8,   17,   58,   20,
      1,   -3,   -2,  -30,  -16,    0,    6,    1,
    -11,   -5,  -15,  -25,  -26,  -15,   -5,  -10,
    -31,  -26,  -30,  -35,  -35,  -30,  -24,  -30,
    -45,  -30,  -45,  -60,  -60,  -44,  -30,  -45,
    -25,  -25,  -60,  -75,  -75,  -60,  -25,  -24,
    -15,  -20,  -26,  -70,  -70,  -25,  -20,  -15,
     -5,  -15,  -20,  -35,  -35,  -19,  -15,   -5,
];
#endregion
    
#region ENDGAME
    internal static readonly short[] Endgame = 
    [ // pawns
     100,  100,  100,  100,  100,  100,  100,  100,
      75,   75,   75,   71,   69,   75,   75,   75,
      85,   86,   81,   75,   75,   79,   84,   84,
     105,  105,  104,  100,  100,  105,  105,  105,
     123,  124,  116,  115,  115,  116,  123,  123,
     170,  157,  146,  144,  145,  146,  158,  168,
     210,  209,  203,  196,  196,  202,  208,  209,
     100,  100,  100,  100,  100,  100,  100,  100,
     // knights
     230,  230,  234,  239,  240,  235,  230,  229,
     229,  240,  245,  245,  244,  244,  239,  229,
     236,  245,  255,  261,  260,  256,  245,  235,
     239,  270,  279,  283,  285,  280,  270,  240,
     246,  270,  280,  286,  285,  280,  270,  244,
     245,  255,  265,  270,  270,  266,  255,  245,
     245,  251,  251,  255,  256,  249,  250,  244,
     245,  246,  250,  244,  245,  250,  246,  245,
     // bishops
     280,  280,  285,  290,  289,  286,  280,  280,
     279,  286,  289,  295,  295,  290,  286,  280,
     290,  285,  285,  291,  290,  285,  285,  290,
     280,  285,  292,  294,  294,  290,  285,  279,
     280,  285,  296,  300,  301,  296,  286,  279,
     280,  286,  284,  294,  294,  284,  286,  280,
     285,  280,  286,  290,  290,  285,  279,  284,
     280,  280,  286,  289,  290,  284,  280,  281,
     // rooks
     510,  514,  515,  510,  511,  516,  515,  511,
     509,  514,  521,  516,  515,  519,  515,  510,
     515,  520,  514,  511,  509,  515,  520,  515,
     509,  515,  511,  505,  504,  510,  515,  511,
     509,  515,  511,  505,  505,  509,  516,  510,
     515,  519,  516,  510,  510,  514,  519,  516,
     510,  514,  519,  515,  515,  519,  515,  511,
     510,  513,  516,  509,  511,  514,  514,  510,
     // queens
     910,  911,  911,  910,  908,  910,  910,  910,
     909,  914,  914,  914,  915,  914,  916,  910,
     909,  914,  920,  925,  926,  920,  915,  911,
     910,  915,  925,  935,  936,  925,  916,  910,
     909,  915,  926,  935,  935,  924,  915,  911,
     912,  915,  918,  924,  925,  919,  915,  910,
     910,  915,  915,  915,  915,  915,  916,  909,
     909,  910,  909,  910,  909,  910,  911,  909,
     // kings
     -70,  -36,  -21,  -11,  -11,  -20,  -36,  -70,
     -35,  -19,  -10,    0,    0,  -10,  -21,  -34,
     -20,  -10,    0,    5,    5,    0,  -11,  -20,
     -10,    0,    5,   26,   25,    4,    0,   -9,
     -10,    0,    4,   25,   25,    4,   -1,  -10,
     -19,  -10,   -1,    5,    5,    0,   -9,  -20,
     -36,  -21,  -10,    0,    0,  -10,  -22,  -35,
     -70,  -34,  -21,  -10,   -8,  -19,  -35,  -69,
    ];
#endregion
}