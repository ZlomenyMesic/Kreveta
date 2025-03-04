/*
 *  Stockshrimp chess engine 1.0
 *  developed by ZlomenyMesic
 */

using Stockshrimp_1.movegen;

namespace Stockshrimp_1;

internal static class UCI {
    internal static void Main() {
        LookupTables.Initialize();

        Game.TestingFunction();

        while (true) {
            string cmd = Console.ReadLine() ?? string.Empty;
            string[] toks = cmd.Split(' ');

            switch (toks[0]) {
                case "uci": CmdUCI(); break;
                case "isready": CmdIsReady(); break;
                case "position": CmdPosition(toks); break;
                case "go": CmdGo(toks); break;
                default: Console.WriteLine($"unknown command: {toks[0]}"); break;
            }
        }
    }

    private static void CmdUCI() {
        Console.WriteLine("id name Stockshrimp-1\nid author ZlomenyMesic\nuciok");
    }

    private static void CmdIsReady() {
        Console.WriteLine("readyok");
    }

    private static void CmdPosition(string[] toks) {
        switch (toks[1]) {
            case "startpos": Game.SetPosFEN(["", "", ..Consts.STARTPOS_FEN.Split(' '), ..toks]); break;
            case "fen": Game.SetPosFEN(toks); break;
            default: Console.WriteLine($"invalid argument: {toks[1]}"); return;
        }
    }

    private static void CmdGo(string[] toks) {
        // TODO - OTHER ARGS

        if (toks.Length > 1 && toks[1] == "perft") {

            int depth = 2;
            try {
                depth = int.Parse(toks[2]);
            } catch {
                Console.WriteLine($"invalid perft command syntax");
                return;
            }
            
            Console.WriteLine($"{Performace.Perft(Game.board, depth, Game.col_to_play)}");
            return;
        }

        Game.SearchBestMove();

        //Move[] moves = Movegen.GetLegalMoves(Game.board, Game.col_to_play);
        //Console.WriteLine(moves.Length);
        //foreach (Move move in moves) {
        //    Console.WriteLine(move);
        //}
    }
}