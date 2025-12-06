import os
import time
import queue
import multiprocessing as mp
import chess
import chess.engine
import pyarrow.dataset as ds

# --- CONFIG ---
ENGINE_PATH = "C:\\Users\\michn\\Downloads\\Stockfish.exe"
SEARCH_DEPTH = 12
PARQUET_FILES = [
    f"C:\\Users\\michn\\Downloads\\archive\\train-{i:05d}-of-00016.parquet"
    for i in range(16)
]
NUM_WORKERS = 12      # number of stockfish engines
CHUNK_FLUSH = 5000    # write to disk every N positions
# ----------------------------------------------


# ========== ENGINE WORKER ==========

def start_engine():
    """Start Stockfish instance."""
    return chess.engine.SimpleEngine.popen_uci(ENGINE_PATH)


def safe_eval(engine, fen, depth):
    """Evaluate a FEN while handling crashes & multipv weirdness."""
    board = chess.Board(fen)

    try:
        info = engine.analyse(board, chess.engine.Limit(depth=depth))
    except Exception:
        # restart engine
        try:
            engine.quit()
        except:
            pass
        time.sleep(0.2)
        engine = start_engine()
        # retry once
        try:
            info = engine.analyse(board, chess.engine.Limit(depth=depth))
        except Exception:
            return engine, None

    # normalize multipv output
    if isinstance(info, list):
        info = info[0]

    score = info["score"].pov(board.turn).score(mate_score=100000)

    if board.turn == chess.BLACK:
        score = -score

    return engine, score


def worker_fn(task_queue, result_queue):
    engine = start_engine()

    while True:
        try:
            fen = task_queue.get(timeout=1)  # get FEN
        except queue.Empty:
            continue

        if fen == "STOP":
            break

        engine, score = safe_eval(engine, fen, SEARCH_DEPTH)
        if score is not None:
            result_queue.put((fen, score))

    engine.quit()


# ========== MAIN PROCESS ==========

def process_single_file(parquet_path, output_path):
    print(f"\nProcessing {os.path.basename(parquet_path)} → {os.path.basename(output_path)}")

    dataset = ds.dataset(parquet_path, format="parquet")
    scanner = dataset.scanner(columns=["fen"])

    seen = set()   # dedup within this file only

    # queues
    task_queue = mp.Queue(maxsize=50000)
    result_queue = mp.Queue()

    # start workers
    workers = [
        mp.Process(target=worker_fn, args=(task_queue, result_queue))
        for _ in range(NUM_WORKERS)
    ]
    for w in workers:
        w.start()

    buffer = []
    written = 0

    with open(output_path, "w") as out:

        # iterate parquet in batches
        for batch in scanner.to_batches():
            fens = batch.column("fen").to_pylist()

            for fen in fens:
                if fen in seen:
                    continue
                seen.add(fen)
                task_queue.put(fen)

            # pull results
            while True:
                try:
                    fen, score = result_queue.get_nowait()
                except queue.Empty:
                    break
                buffer.append(f"{fen};{score}\n")

            # flush periodically
            if len(buffer) >= CHUNK_FLUSH:
                out.writelines(buffer)
                written += len(buffer)
                buffer.clear()
                print(f"   Flushed {written} positions…")

        # drain final results
        time.sleep(1)
        while True:
            try:
                fen, score = result_queue.get_nowait()
            except queue.Empty:
                break
            buffer.append(f"{fen};{score}\n")

        out.writelines(buffer)
        written += len(buffer)
        print(f"   Final flush, total {written} positions.")

    # stop workers
    for _ in range(NUM_WORKERS):
        task_queue.put("STOP")
    for w in workers:
        w.join()


def main():
    for parquet_path in PARQUET_FILES:
        if not os.path.exists(parquet_path):
            print("Missing:", parquet_path)
            continue

        output_path = parquet_path.replace(".parquet", ".txt")
        process_single_file(parquet_path, output_path)


if __name__ == "__main__":
    mp.freeze_support()
    main()
