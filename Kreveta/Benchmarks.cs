﻿//
// Kreveta chess engine by ZlomenyMesic
// started 4-3-2025
//

// Remove unnecessary suppression
#pragma warning disable IDE0079

// Consider making public types internal
#pragma warning disable CA1515

// Mark members as static
#pragma warning disable CA1822

using System;
using System.Collections.Generic;
using System.Linq;
using BenchmarkDotNet.Attributes;

using Kreveta.consts;
using Kreveta.evaluation;
using Kreveta.movegen;
using Kreveta.moveorder;

namespace Kreveta;

/*
 * CURRENT BENCHMARKS:
 * Board.PlayMove             23.17 ns
 * Board.PlayReversibleMove   21.02 ns
 * Eval.StaticEval            96.31 ns
 * ZobristHash.GetHash        23.47 ns
 * Movegen.GetLegalMoves      383.5 ns
 *
 *
 * 
 * FASTEST PERFT 6            1.443 s
 * FASTEST PERFT 7            24.83 s
 */

[MemoryDiagnoser]
[RankColumn, Orderer(BenchmarkDotNet.Order.SummaryOrderPolicy.FastestToSlowest)]
public class Benchmarks {
    private Board _position;
    private Move  _move;

    private Move[] _legal;
    
    [GlobalSetup]
    public void Setup() {
        _position = Board.CreateStartpos();
        _move     = new Move(53, 37, PType.PAWN, PType.NONE, PType.NONE);
        
        Span<Move> buffer = stackalloc Move[128];
        int c = Movegen.GetLegalMoves(ref _position, buffer);
        
        _legal = new Move[c];
        for (int i = 0; i < c; i++)
            _legal[i] = buffer[i];
    }

    /*[Benchmark]
    public void ListTest() {
        List<(Move, int)> quiets = [];

        for (int i = 0; i < _legal.Length; i++) {
            if (_legal[i].Capture == PType.NONE)
                quiets.Add((_legal[i], QuietHistory.GetRep(_position, _legal[i])));
        }
    }

    [Benchmark]
    public void IEnumTest() {
        (Move, int)[] quiets = [.._legal
            .Select(move => (move, QuietHistory.GetRep(_position, move)))
            .Where(item => item.move.Capture == PType.NONE)
        ];
    }*/

    // [Benchmark]
    // public void CloneBoard() {
    //     var clone = _position.Clone();
    // }

    // [Benchmark]
    // public void GetLegalMoves() {
    //     _ = Movegen.GetLegalMoves(ref _position, stackalloc Move[128]);
    // }

    /*
    [Benchmark]
    public void ZobristGetHash() {
        ulong hash = ZobristHash.GetHash(_position);
    }

    [Benchmark]
    public void StaticEval() {
        int x = Eval.StaticEval(_position);
    }*/

    // [Benchmark]
    // public void PlayMove() {
    //     var clone = _position.Clone();
    //     clone.PlayMove(_move);
    // }

    /*
    [Benchmark]
    public void PlayMoveReversible() {
        var clone = _position.Clone();
        clone.PlayReversibleMove(_move, Color.WHITE);
    }*/
}

#pragma warning restore CA1822
#pragma warning restore CA1515

#pragma warning restore IDE0079