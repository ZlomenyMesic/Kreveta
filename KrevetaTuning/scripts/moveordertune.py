import subprocess
import random
from multiprocessing import Process, Queue
import multiprocessing as mp

# ---------------- Engine wrapper ----------------

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

    def search_fen(self, fen, movetime):
        self.send(f"position fen {fen}")
        self.send(f"go movetime {movetime}")

        while True:
            line = self.p.stdout.readline()
            if line.startswith("bestmove"):
                break

    def get_tuning(self):
        self.send("gettuning")
        self.send("isready")

        score = count = 0
        while True:
            line = self.p.stdout.readline()
            if "TUNE score" in line:
                score = int(line.split()[-1])
            elif "TUNE count" in line:
                count = int(line.split()[-1])
            elif "readyok" in line:
                break
        return score, count

    def quit(self):
        self.send("quit")
        self.p.wait()

# ---------------- Evaluation ----------------

def evaluate_worker_queue(cmd, params, fens, queue):
    engine = Engine(cmd, params)

    for fen in fens:
        # Small random jitter avoids pathological synchronization
        movetime = random.randint(200, 500)
        engine.search_fen(fen, movetime)

    score, count = engine.get_tuning()
    engine.quit()
    queue.put((score, count))


def evaluate_parallel(cmd, params, fens, workers=8):
    queue = Queue()
    processes = []

    # Split positions evenly
    chunks = [fens[i::workers] for i in range(workers)]

    for i in range(workers):
        p = Process(
            target=evaluate_worker_queue,
            args=(cmd, params, chunks[i], queue)
        )
        p.start()
        processes.append(p)

    results = [queue.get() for _ in processes]

    for p in processes:
        p.join()

    fitness_values = [
        s / c if c > 0 else 0
        for s, c in results
    ]

    return sum(fitness_values) / len(fitness_values)

# ---------------- Mutation logic (unchanged) ----------------

def mutate(params, iteration, max_iter):
    new = params.copy()

    start = 0.01   # 1 %
    end   = 0.005  # 0.5 %

    frac = iteration / max_iter
    max_rel = start * ((end / start) ** frac)

    num_changes = 1 if random.random() < 0.75 else 2
    indices = random.sample(range(len(params)), num_changes)

    for i in indices:
        base = params[i]
        scale = abs(base) if base != 0 else 1

        delta = int(scale * random.uniform(-max_rel, max_rel))
        while delta == 0:
            delta = random.randint(-1, 1)

        new[i] += delta

    return new


def accept(new_score, old_score):
    return new_score >= old_score

# ---------------- Main tuning loop ----------------

def load_fens(path):
    with open(path, "r") as f:
        return [line.strip() for line in f if line.strip()]


def tune():
    cmd = [
        "C:\\Users\\michn\\Desktop\\Kreveta\\Kreveta\\Kreveta\\bin\\Release\\net10.0\\Kreveta.exe"
    ]

    fens = load_fens("positions.txt")
    random.shuffle(fens)

    params = [1435, 1032, 1079, 58, 664, -238, 94, -695, 130, 110, -31, 49, -43, 33, 222, 216, 108, 180]

    best_score = evaluate_parallel(cmd, params, fens)
    print("Initial score:", best_score)

    max_iter = 800

    for it in range(max_iter):
        if it % 8 == 0 and it != 0:
            best_score = evaluate_parallel(cmd, params, fens)
            print(f"REEVALUATED score: {best_score:.4f}")

        candidate = mutate(params, it, max_iter)
        score = evaluate_parallel(cmd, candidate, fens)

        if accept(score, best_score):
            params = candidate
            best_score = score
            print(f"[{it}] ACCEPT {best_score:.4f} params: {params}")
        else:
            print(f"[{it}] REJECT {score:.4f}")

    print("Final params:", params)


if __name__ == "__main__":
    mp.set_start_method("spawn")
    tune()
