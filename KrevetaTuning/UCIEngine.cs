//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

// ReSharper disable InconsistentNaming
// ReSharper disable StringIndexOfIsCultureSpecific.1

using System.Diagnostics;

namespace KrevetaTuning;

internal sealed class UCIEngine {
    private readonly Process      _process;
    private readonly StreamWriter _input;
    private readonly StreamReader _output;

    internal UCIEngine(string path) {
        _process = new Process();
        _process.StartInfo.FileName = path;
        _process.StartInfo.RedirectStandardInput = true;
        _process.StartInfo.RedirectStandardOutput = true;
        _process.StartInfo.UseShellExecute = false;
        _process.StartInfo.CreateNoWindow = true;
        _process.Start();

        _input = _process.StandardInput;
        _output = _process.StandardOutput;

        Send("uci");
        ReadUntil("uciok");
    }

    internal void Send(string cmd) {
        _input.WriteLine(cmd);
        _input.Flush();
    }

    private void ReadUntil(string token) {
        while (_output.ReadLine() is { } line) {
            if (line.Contains(token))
                break;
        }
    }

    internal (int eval, string bestMove) EvaluateFEN(string fen, int depth, Program.EvalMode mode) {
        try {
            Send("position fen " + fen);
        } catch (Exception e) {
            Console.Write($"skip fen: {fen}");
        }

        if (mode == Program.EvalMode.FullSearch) {
            Send($"go depth {depth}");
        
            string bestMove = string.Empty;
            int    eval     = 0;
        
            while (_output.ReadLine() is { } line) {
                if (line.StartsWith("info") && line.Contains("score cp")) {
                    var tokens = line.Split(' ');
                    int scoreIndex = Array.IndexOf(tokens, "cp");
                    if (scoreIndex != 0 && int.TryParse(tokens[scoreIndex + 1], out int val))
                        eval = val;
                }
                else if (line.StartsWith("bestmove")) {
                    bestMove = line.Split(' ')[1];
                    break;
                }
            }
            return (eval, bestMove);
        }
        
        Send("gettuning");
            
        while (_output.ReadLine() is { } line) {
            if (line.StartsWith("se")) {
                var tokens = line.Split(' ');
                if (short.TryParse(tokens[1], out short eval))
                    return (eval, string.Empty);
            }
        }
        return (0, string.Empty);
    }

    internal void Quit() {
        Send("quit");
        _process.WaitForExit();
    }
}