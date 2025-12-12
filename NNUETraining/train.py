#
# Kreveta chess engine by ZlomenyMesic
# started 4-3-2025
#

import os
import json
import time
from datetime import datetime
import signal
import random
import multiprocessing as mp
from multiprocessing import Queue

import numpy as np
import tensorflow as tf
from keras import layers, models, optimizers, losses
import keras
import chess
import chess.engine
import chess.polyglot

# only log warnings and errors
os.environ["TF_CPP_MIN_LOG_LEVEL"] = "2"

# the absolute path to where the script is running
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))

# model path
MODEL_DIR = os.path.join(SCRIPT_DIR, "nnue_model.keras")

WEIGHTS_PATH = os.path.join(SCRIPT_DIR, "weights\\nnue_weights.bin")
SHAPES_PATH  = os.path.join(SCRIPT_DIR, "weights\\nnue_shapes.json")
CONFIG_PATH  = os.path.join(SCRIPT_DIR, "config.json")

NUM_WORKERS = 10
ENGINE_CMD  = "C:\\Users\\michn\\Downloads\\Stockfish.exe"
BOOK_PATH   = "C:\\Users\\michn\\Downloads\\polyglot\\rodent.bin"

# total features (shared by accumulators)
FEATURE_COUNT     = 40960

EMBED_DIM         = 256
H1_NEURONS        = 16
H2_NEURONS        = 32
BATCH_SIZE        = 4096

SAMPLES_QUEUE_MAX = 10000
SAVE_EVERY_SEC    = 200
MAX_PLIES         = 250

BUCKET_TABLE = tf.constant([
    0, 0, 0, 0, 0,
    0, 0, 1, 1,
    1, 1, 1, 2,
    2, 2, 2, 3,
    3, 3, 3, 4,
    4, 4, 4, 5,
    5, 5, 6, 6,
    6, 7, 7, 7
], dtype = tf.int32)

CONFIG = {
    "random_move_freq": 0.14,
    "book_moves":       16,
    "mirror_enabled":   False,
    "min_depth":        8,
    "max_depth":        10
}

def load_config():
    global CONFIG
    try:
        with open(CONFIG_PATH) as f:
            data = json.load(f)
            CONFIG.update(data)

    except Exception:
        print("loading config failed")

def feature_index(king_square: int, piece_type: int, is_black: bool, piece_square: int) -> int:

    # map piece_type 1..5 into 0..4
    piece_type_idx = piece_type - 1
    color_bit      = 1 if is_black else 0

    king_offset  = king_square * 640
    piece_offset = (piece_type_idx * 2 + color_bit) * 64 + piece_square

    f = king_offset + piece_offset
    
    return int(f)

def board_features(board: chess.Board):
    w_indices = []
    b_indices = []
    m_w_indices = []
    m_b_indices = []

    w_king_sq = board.king(chess.WHITE)
    b_king_sq = board.king(chess.BLACK)

    # if a king is missing (shouldn't happen in legal positions), we still produce empty lists.
    if w_king_sq is None or b_king_sq is None:
        return []
    
    m_w_king_sq = (7 - (w_king_sq & 7)) + (8 * (w_king_sq >> 3))
    m_b_king_sq = (7 - (b_king_sq & 7)) + (8 * (b_king_sq >> 3))

    for sq in chess.SQUARES:
        piece = board.piece_at(sq)

        # empty square
        if piece is None:
            continue

        # kings are excluded
        if piece.piece_type == chess.KING:
            continue

        # piece_color True if black, False if white
        is_black = piece.color == chess.BLACK

        m_sq = (7 - (sq & 7)) + (8 * (sq >> 3))

        # index into white accumulator (white king as reference)
        idx_w = feature_index(
            king_square  = w_king_sq,
            piece_type   = piece.piece_type,
            is_black     = is_black,
            piece_square = sq
        )
        # index into black accumulator (black king as reference)
        idx_b = feature_index(
            king_square  = b_king_sq ^ 56,
            piece_type   = piece.piece_type,
            is_black     = not is_black,
            piece_square = sq ^ 56
        )

        m_idx_w = feature_index(
            king_square  = m_w_king_sq,
            piece_type   = piece.piece_type,
            is_black     = is_black,
            piece_square = m_sq
        )
        m_idx_b = feature_index(
            king_square  = m_b_king_sq ^ 56,
            piece_type   = piece.piece_type,
            is_black     = not is_black,
            piece_square = m_sq ^ 56
        )

        w_indices.append(idx_w)
        b_indices.append(idx_b)
        m_w_indices.append(m_idx_w)
        m_b_indices.append(m_idx_b)

    return w_indices, b_indices, m_w_indices, m_b_indices

def ClippedReLU(x):
    return keras.activations.relu(x, max_value = 1.0)

def build_model() -> keras.Model:
    # two ragged int inputs (variable-length lists of feature indices)
    inp_active  = layers.Input(shape = (None,), ragged = True, dtype = 'int32', name = 'Input_Active')
    inp_passive = layers.Input(shape = (None,), ragged = True, dtype = 'int32', name = 'Input_Passive')
    inp_pcnt    = layers.Input(shape = (), dtype = 'int32', name = 'Input_PieceCount')

    # separate embedding tables (no shared weights)
    emb_shared = layers.Embedding(
        input_dim  = FEATURE_COUNT,
        output_dim = EMBED_DIM,
        name       = 'Embedding_Shared'
    )

    emb_active  = emb_shared(inp_active)
    emb_passive = emb_shared(inp_passive)

    # accumulate embeddings per sample (reduce over sequence axis)
    summed_active = layers.Lambda(
        lambda x: tf.reduce_sum(x, axis = 1),
        output_shape = (EMBED_DIM,),
        name         = 'Accumulator_Active'
    )(emb_active)

    summed_passive = layers.Lambda(
        lambda x: tf.reduce_sum(x, axis = 1),
        output_shape = (EMBED_DIM,),
        name         = 'Accumulator_Passive'
    )(emb_passive)

    # concatenate the two accumulators
    concat = layers.Concatenate(name = 'Accumulator_Concat')([summed_active, summed_passive])

    subnets = []
    for i in range(8):
        # two dense layers, CReLU activation
        h1 = layers.Dense(H1_NEURONS, activation = ClippedReLU, name = f'Subnet_{i}_Dense_1')(concat)
        h2 = layers.Dense(H2_NEURONS, activation = ClippedReLU, name = f'Subnet_{i}_Dense_2')(h1)
        output = layers.Dense(
            1,
            activation = 'sigmoid',
            name       = f'Subnet_{i}_Output'
        )(h2)

        subnets.append(output)

    # wrap tf.stack into a Lambda so we only use Keras layers on KerasTensors
    stacked = layers.Lambda(lambda inputs: tf.stack(inputs, axis = 1), name = 'Stack_Subnets')(subnets)

    # select the right subnet according to piece count bucket
    def select_fn(args):
        stacked_tensor, pc = args
        bucket = tf.gather(BUCKET_TABLE, pc)
        # stacked_tensor shape: (batch, 8, 1)
        # want to pick stacked_tensor[bucket] for each batch element
        # reshape bucket to (batch,) and use tf.range to collect indices
        batch_idx = tf.range(tf.shape(bucket)[0])
        indices   = tf.stack([batch_idx, bucket], axis = 1) # (batch, 2)
        selected  = tf.gather_nd(stacked_tensor, indices)   # (batch, 1)
        return selected

    select = layers.Lambda(
        select_fn,
        name         = 'Select_Subnet',
        output_shape = (1,)
    )([stacked, inp_pcnt])

    lr_schedule = optimizers.schedules.ExponentialDecay(
        initial_learning_rate = 1e-2,
        decay_rate            = 0.99993, # 0.99991
        decay_steps           = 1,
        staircase             = False
    )

    model = models.Model([inp_active, inp_passive, inp_pcnt], select)
    model.compile(
        optimizer = optimizers.AdamW(
            learning_rate = lr_schedule,
            weight_decay  = 1e-5,
            clipnorm      = 1.0
        ),
        loss    = losses.BinaryCrossentropy(),
        metrics = [keras.metrics.MeanAbsoluteError(name = 'mae')]
    )
    return model

def save_weights_binary(model, weights_path = WEIGHTS_PATH, shapes_path = SHAPES_PATH):
    weights = model.get_weights()
    shapes = [list(w.shape) for w in weights]

    # flatten all weights to a single 1D float32 array
    flat = np.concatenate([w.ravel().astype(np.float32) for w in weights]) if len(weights) else np.array([], dtype = np.float32)

    # write the flat array
    flat.tofile(weights_path)

    # write shapes
    with open(shapes_path, "w") as f:
        json.dump(shapes, f)
    return shapes, flat.size

def load_weights_binary(model, weights_path = WEIGHTS_PATH, shapes_path = SHAPES_PATH):
    if not os.path.exists(weights_path) or not os.path.exists(shapes_path):
        return False

    with open(shapes_path, "r") as f:
        shapes = json.load(f)

    # total number of floats expected
    total = 0
    sizes = []
    for s in shapes:
        size = int(np.prod(s))
        sizes.append(size)
        total += size

    # read binary as float32
    try:
        flat = np.fromfile(weights_path, dtype = np.float32)
    except Exception as e:
        print("failed to read weights file:", e)
        return False

    if flat.size != total:
        print(f"weight count mismatch: expected {total} floats, got {flat.size}.")
        return False

    # reconstruct
    weights = []
    idx = 0
    for size, shape in zip(sizes, shapes):
        w = flat[idx: idx + size].reshape(tuple(shape)).astype(np.float32)
        weights.append(w)
        idx += size

    try:
        model.set_weights(weights)
    except Exception as e:
        print("model.set_weights failed:", e)
        return False

    return True

def engine_worker(worker_id: int, samples_queue: Queue, stop_event: mp.Event):
    print(f"[worker {worker_id}] starting self-play; cmd = {ENGINE_CMD}")

    try:
        engine = chess.engine.SimpleEngine.popen_uci(ENGINE_CMD)
    except Exception as e:
        print(f"[worker {worker_id}] failed to start engine: {e}")
        return
    
    try:
        book = chess.polyglot.open_reader(BOOK_PATH)
    except Exception:
        book = None

    rng = random.Random(time.time() + worker_id)

    while not stop_event.is_set():
        load_config()

        board = chess.Board()
        plies = 0
        random_move_freq = rng.random() * CONFIG["random_move_freq"]

        while plies < MAX_PLIES and not stop_event.is_set():
            # random move chance
            if rng.random() < random_move_freq:
                legal_moves = list(board.legal_moves)
                move        = rng.choice(legal_moves)
                board.push(move)

            # random polyglot book move
            elif book is not None and plies < CONFIG["book_moves"]:
                try:
                    entries = list(book.find_all(board))
                    if entries:
                        entry = random.choice(entries)
                        board.push(entry.move)
                except Exception:
                    pass

            # otherwise let the engine choose the move
            else:
                try:
                    move_depth = rng.randint(3, 12)
                    result     = engine.play(board, chess.engine.Limit(depth = move_depth))

                    if result.move is None:
                        break

                    board.push(result.move)
                except Exception as e:
                    print(f"[worker {worker_id}] play() error: {e}")
                    break

            plies += 1
            if board.is_game_over():
                break

            try:
                info  = engine.analyse(board, chess.engine.Limit(
                    depth = rng.randint(CONFIG["min_depth"], CONFIG["max_depth"])
                ))
                score = info.get("score")

            except Exception as e:
                print(f"[worker {worker_id}] analyse() error: {e}")
                break

            if not score:
                continue

            sc = score.white()
            cp = 0.0
            
            if sc.is_mate():
                cp = 1800 if sc.mate() > 0 else -1800
            else:
                cp = np.clip(sc.score(), -1800, 1800)

            # if black is the active side, the score must be inverted
            if board.turn == chess.BLACK:
                cp = -cp

            # map cp to [0,1]
            target = 1.0 / (1.0 + np.exp(-cp / 400.0))

            # generate feature indices for both the real
            # board and the vertically mirrored version
            w_indices, b_indices, m_w_indices, m_b_indices = board_features(board)

            w_np = np.array(w_indices, dtype = np.int32)
            b_np = np.array(b_indices, dtype = np.int32)
            m_w_np = np.array(m_w_indices, dtype = np.int32)
            m_b_np = np.array(m_b_indices, dtype = np.int32)

            try:
                if (board.turn == chess.WHITE):
                    samples_queue.put(((w_np, b_np), float(target)), timeout = 1.0)

                    if CONFIG["mirror_enabled"]:
                        samples_queue.put(((m_w_np, m_b_np), float(target)), timeout = 1.0)

                        samples_queue.put(((b_np, w_np), float(1.0 - target)), timeout = 1.0)
                        samples_queue.put(((m_b_np, m_w_np), float(1.0 - target)), timeout = 1.0)

                else:
                    samples_queue.put(((b_np, w_np), float(target)), timeout = 1.0)

                    if CONFIG["mirror_enabled"]:
                        samples_queue.put(((m_b_np, m_w_np), float(target)), timeout = 1.0)

                        samples_queue.put(((w_np, b_np), float(1.0 - target)), timeout = 1.0)
                        samples_queue.put(((m_w_np, m_b_np), float(1.0 - target)), timeout = 1.0)

            except Exception:
                time.sleep(0.05)

        time.sleep(0.01)

    try:
        engine.quit()
    except Exception:
        pass
    print(f"[worker {worker_id}] stopping.")

def trainer_loop(model, samples_queue: Queue, stop_event: mp.Event):
    last_save = time.time()
    x_batch   = []  # list of tuples (active, passive)
    y_batch   = []
    seen      = 0

    try:
        while not stop_event.is_set():
            try:
                x, y = samples_queue.get(timeout = 1.0)
            except Exception:
                pass
            else:
                x_batch.append(x)
                y_batch.append(y)

            if len(x_batch) >= BATCH_SIZE:
                # build ragged tensors separately for both accumulators
                active  = [pair[0] for pair in x_batch]
                passive = [pair[1] for pair in x_batch]

                x_active  = tf.ragged.constant(active,  dtype = tf.int32)
                x_passive = tf.ragged.constant(passive, dtype = tf.int32)
                x_pcnts   = np.array([len(a) + 2 for a in active], dtype = np.int32)

                y_np = np.array(y_batch, dtype = np.float32).reshape(-1, 1)

                loss, mae = model.train_on_batch(
                    [x_active, x_passive, x_pcnts], y_np
                )

                seen += len(x_batch)
                x_batch.clear()
                y_batch.clear()

                timestamp = datetime.now().isoformat()
                print(f"[{timestamp}] samples: {seen} loss: {loss:.6f} mae: {mae:.6f} lr: {model.optimizer.learning_rate:.8f}")

                # CSV log (logs are limited to avoid spam)
                if (seen % 32768 == 0):
                    with open('log.csv', "a") as f:
                        f.write(f"{seen},{loss:.6f},{mae:.6f},{timestamp}\n")

            # periodic save by wall-clock
            if time.time() - last_save > SAVE_EVERY_SEC:
                #load_config()
                
                try:
                    _, count = save_weights_binary(model)
                    print(f"[{datetime.now().isoformat()}] saved weights ({count} floats) -> {WEIGHTS_PATH}, shapes -> {SHAPES_PATH}")
                except Exception as e:
                    print("failed to save weights:", e)
                last_save = time.time()

    except KeyboardInterrupt:
        print("training interrupted, exiting gracefully...")

    finally:
        # final save on exit
        try:
            _, count = save_weights_binary(model)
            print(f"[{datetime.now().isoformat()}] final save weights ({count} floats) -> {WEIGHTS_PATH}, shapes -> {SHAPES_PATH}")
        except Exception as e:
            print("final save failed:", e)
        stop_event.set()

def main():

    # build model
    model = build_model()
    print("\nbuilt new model.\n")

    # try to load raw weights if present
    if os.path.exists(WEIGHTS_PATH) and os.path.exists(SHAPES_PATH):
        print("\nfound raw weights + shapes. Attempting to load...\n")
        try:
            load_weights_binary(model)
        except Exception as e:
            print("\nerror while loading weights: ", e)

    # create the CSV log file if it doesn't exist
    if not os.path.exists('log.csv'):
        with open('log.csv', "w") as f:
            f.write("samples,loss,mae,timestamp\n")

    # set up multiprocessing
    mp_ctx        = mp.get_context("spawn")
    samples_queue = mp_ctx.Queue(maxsize = SAMPLES_QUEUE_MAX)
    stop_event    = mp_ctx.Event()

    workers = []
    for i in range(NUM_WORKERS):
        p = mp_ctx.Process(
            target = engine_worker,
            args   = (i, samples_queue, stop_event),
            daemon = True
        )
        p.start()
        workers.append(p)

    def handle_sigint(signum, frame):
        print("stopping...")
        stop_event.set()
    signal.signal(signal.SIGINT, handle_sigint)

    try:
        trainer_loop(model, samples_queue, stop_event)
    finally:
        print("waiting for workers...")
        stop_event.set()
        for p in workers:
            p.join(timeout = 2.0)

if __name__ == "__main__":
    main()