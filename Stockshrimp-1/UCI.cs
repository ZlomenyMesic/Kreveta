/*
 *  Stockshrimp chess engine 1.0
 *  developed by ZlomenyMesic
 */

using Stockshrimp_1.movegen;
using System.Diagnostics;

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
                case "ischeck": CmdIsCheck(); break;
                case "showallmoves": CmdShowAllMoves(); break;
                case "print": CmdPrint(); break;
                default: Console.WriteLine($"unknown command: {toks[0]}"); break;
            }

            Console.WriteLine();
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

        TT.Clear();

        if (toks.Length > 1 && toks[1] == "perft") {

            int depth;
            try {
                depth = int.Parse(toks[2]);
            } catch {
                Console.WriteLine($"invalid perft command syntax");
                return;
            }
            Stopwatch sw = Stopwatch.StartNew();

            Console.WriteLine($"nodes: {Performace.Perft(Game.board, depth, Game.col_to_play)}");

            sw.Stop();
            Console.WriteLine($"time spent: {sw.Elapsed}");
            return;
        }

        Game.SearchBestMove();

        //byte cur_e = Game.board.enPassantSquare;
        //byte cur_c = Game.board.castlingFlags;

        //Game.board.DoMove(new(62, 47, 1, 6, 6));
        //Game.board.UndoMove(new(62, 47, 1, 6, 6), cur_e, cur_c);
        //Game.board.Print();

        //Move[] moves = Movegen.GetLegalMoves(Game.board, Game.col_to_play);
        //Console.WriteLine(moves.Length);
        //foreach (Move move in moves) {
        //    Console.WriteLine(move);
        //}
    }

    private static void CmdIsCheck() {
        Console.WriteLine($"{Movegen.IsKingInCheck(Game.board, Game.col_to_play)}");
    }

    private static void CmdShowAllMoves() {
        foreach (Move m in Movegen.GetLegalMoves(Game.board, Game.col_to_play)) {
            Console.WriteLine(m.ToString());
        }
    }

    private static void CmdPrint() {
        Game.board.Print();
    }
}