import subprocess
import chess
import random
from multiprocessing import Process, Queue
import multiprocessing as mp
import math
import time

class Engine:
    def __init__(self, cmd, tune=None):
        self.p = subprocess.Popen(
            cmd,
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            stderr=subprocess.DEVNULL,
            text=True,
            bufsize=1
        )
        self.send("uci")
        self.wait("uciok")

        if tune:
            self.send(f"tune {' '.join(map(str, tune))}")

        self.send("isready")
        self.wait("readyok")

    def send(self, cmd):
        self.p.stdin.write(cmd + "\n")
        self.p.stdin.flush()

    def wait(self, token):
        while True:
            line = self.p.stdout.readline()
            if token in line:
                break

    def get_tuning(self):
        self.send("gettuning")
        self.send("isready")

        score = cutoffs = 0
        while True:
            line = self.p.stdout.readline()
            if "TUNE score" in line:
                score = int(line.split()[-1])
            elif "TUNE cutoffs" in line:
                cutoffs = int(line.split()[-1])
            elif "readyok" in line:
                break
        return score, cutoffs

    def quit(self):
        self.send("quit")
        self.p.wait()

def play_game(engine):
    board = chess.Board()
    ply = 0

    while not board.is_game_over(claim_draw = True) and ply < 110:
        engine.send(f"position fen {board.fen()}")
        engine.send(f"go movetime {random.randint(200, 650)}")

        ply += 1

        while True:
            line = engine.p.stdout.readline()
            if line.startswith("bestmove"):
                move = chess.Move.from_uci(line.split()[1])
                board.push(move)
                break


def evaluate_worker_queue(cmd, params, games_per_worker, queue):
    engine = Engine(cmd, params)
    for _ in range(games_per_worker):
        play_game(engine)
    score, cutoffs = engine.get_tuning()
    engine.quit()
    queue.put((score, cutoffs))


def evaluate_parallel(cmd, params, workers = 10, games_per_worker = 2):
    queue = Queue()
    processes = []

    for _ in range(workers):
        p = Process(target=evaluate_worker_queue, args=(cmd, params, games_per_worker, queue))
        p.start()
        processes.append(p)

    results = []
    for _ in range(workers):
        results.append(queue.get())

    for p in processes:
        p.join()

    # Compute average fitness
    fitness_values = [s / c if c != 0 else 0 for s, c in results]
    return sum(fitness_values) / len(fitness_values)

def mutate(params, iteration, max_iter):
    new = params.copy()

    # Relative mutation range (annealed)
    start = 0.01    # 1 %
    end   = 0.005   # 0.5 %

    # Exponential decay
    frac = iteration / max_iter
    max_rel = start * ((end / start) ** frac)

    # Mostly single-parameter changes
    num_changes = 1 if random.random() < 0.75 else 2
    indices = random.sample(range(len(params)), num_changes)

    for i in indices:
        base = params[i]
        scale = abs(base) if base != 0 else 1

        delta = int(scale * random.uniform(-max_rel, max_rel))
        while delta == 0:
            delta += random.randint(-1, 1)

        new[i] += delta

    return new

def accept(new_score, old_score):
    return new_score >= old_score

def tune():
    cmd = ["C:\\Users\\michn\\Desktop\\Kreveta\\Kreveta\\Kreveta\\bin\\Release\\net10.0\\Kreveta.exe"]

    params = [119, 896, 103, 55, -96, -209, 277, 155, 82, 3011, 1542, 54, 15, 18, 284, 201, 84]

    best_score = evaluate_parallel(cmd, params)
    print("Initial score:", best_score)

    max_iter = 800

    for it in range(max_iter):
        if (it % 8 == 0 and it != 0):
            best_score = evaluate_parallel(cmd, params)
            print(f"REEVALUATED score: {best_score}")

        candidate = mutate(params, it, max_iter)
        score = evaluate_parallel(cmd, candidate)

        if accept(score, best_score):
            params = candidate
            best_score = score
            print(f"[{it}] ACCEPT score: {best_score:.3f} params: {params}")
        else:
            print(f"[{it}] REJECT score: {score:.3f}")

    print("Final params:", params)

if __name__ == "__main__":
    mp.set_start_method("spawn")
    tune()