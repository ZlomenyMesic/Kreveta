#
# Kreveta chess engine by ZlomenyMesic
# started 4-3-2025
#

import os
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

ENGINE_CMD  = "C:\\Users\\michn\\Downloads\\Stockfish.exe"
NUM_WORKERS = 10

BOOK_PATH = "C:\\Users\\michn\\Downloads\\polyglot\\Human.bin"
BOOK_MOVES = 20

# total features (two accumulators combined)
NUM_FEATURES_TOTAL = 81920  # 2 * 64 * 64 * 5 * 2
FEATURES_PER_ACC   = 40960

EMBED_DIM         = 256
H1_NEURONS        = 32
H2_NEURONS        = 32
LEARNING_RATE     = 1e-3
BATCH_SIZE        = 2048

SAMPLES_QUEUE_MAX = 10000
SAVE_EVERY_SEC    = 300
MAX_PLIES         = 260

# global counter for the number of
# positions already trained/seen
seen = 0

def feature_index_for_acc(king_square: int, piece_type: int, is_black: bool, piece_square: int) -> int:
    # skip kings (just to make sure)
    if piece_type == chess.KING or piece_type == None:
        return -1

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

    w_king_sq = board.king(chess.WHITE)
    b_king_sq = board.king(chess.BLACK)

    # if a king is missing (shouldn't happen in legal positions), we still produce empty lists.
    if w_king_sq is None or b_king_sq is None:
        return [], []

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

        # index into white accumulator (white king as reference)
        idx_w = feature_index_for_acc(
            king_square  = w_king_sq,
            piece_type   = piece.piece_type,
            is_black     = is_black,
            piece_square = sq
        )
        # index into black accumulator (black king as reference)
        idx_b = feature_index_for_acc(
            king_square  = b_king_sq,
            piece_type   = piece.piece_type,
            is_black     = is_black,
            piece_square = sq
        )

        if idx_w != -1:
            w_indices.append(idx_w)
        if idx_b != -1:
            b_indices.append(idx_b)

    return w_indices, b_indices

def mirrored_board_features(board: chess.Board):
    # create a mirrored + color-flipped board  
    mirrored = chess.Board(fen = board.fen()) # copy

    # mirror piece placement
    piece_map = {}

    for sq in chess.SQUARES:
        p = board.piece_at(sq)
        if p is None:
            continue

        new_sq = chess.square_mirror(sq)

        # flip piece color
        flipped_color = not p.color

        piece_map[new_sq] = chess.Piece(p.piece_type, flipped_color)

    mirrored.clear_board()
    for sq, piece in piece_map.items():
        mirrored.set_piece_at(sq, piece)

    # now extract features normally
    return board_features(mirrored)

def CReLU(x):
    return keras.activations.relu(x, max_value = 1.0)

def build_model():
    # two ragged int inputs (variable-length lists of feature indices)
    inp_active  = layers.Input(shape = (None,), ragged = True, dtype = 'int32', name='Inp_Active')
    inp_passive = layers.Input(shape = (None,), ragged = True, dtype = 'int32', name='Inp_Passive')

    # separate embedding tables (no shared weights)
    emb_active = layers.Embedding(
        input_dim  = FEATURES_PER_ACC,
        output_dim = EMBED_DIM,
        name       = 'Embedding_Active'
    )(inp_active)
    emb_passive = layers.Embedding(
        input_dim  = FEATURES_PER_ACC,
        output_dim = EMBED_DIM,
        name       = 'Embedding_Passive'
    )(inp_passive)

    # accumulate embeddings per sample (reduce over sequence axis)
    summed_active = layers.Lambda(
        lambda x: tf.reduce_sum(x, axis = 1),
        output_shape = (EMBED_DIM,),
        name         = 'EmbedSum_Active'
    )(emb_active)

    summed_passive = layers.Lambda(
        lambda x: tf.reduce_sum(x, axis = 1),
        output_shape = (EMBED_DIM,),
        name         = 'EmbedSum_Passive'
    )(emb_passive)

    # concatenate the two accumulators
    concat = layers.Concatenate(name='Embed_Concat')([summed_active, summed_passive])

    # two dense layers, CReLU activation
    h1 = layers.Dense(
        H1_NEURONS,
        activation = CReLU,
        name       = 'Hidden_1'
    )(concat)
    h2 = layers.Dense(
        H2_NEURONS,
        activation = CReLU,
        name       = 'Hidden_2'
    )(h1)

    output = layers.Dense(1, activation = 'sigmoid', name = 'Output')(h2)

    model = models.Model([inp_active, inp_passive], output)
    model.compile(
        optimizer = optimizers.AdamW(
            learning_rate = LEARNING_RATE,
            weight_decay  = 1e-5,
            clipnorm      = 1.0
        ),
        loss    = losses.BinaryCrossentropy(),
        metrics = [keras.metrics.MeanAbsoluteError(name = 'mae')]
    )
    return model

def save_model_diag(model, filename):
    from keras.utils import plot_model
    plot_model(
        model,
        to_file                = filename,
        show_shapes            = True,
        show_layer_names       = True,
        expand_nested          = True,
        dpi                    = 200, # image resolution
        show_layer_activations = True
    )

def engine_worker(worker_id: int, samples_queue: Queue, stop_event: mp.Event):
    print(f"[worker {worker_id}] starting self-play; cmd = {ENGINE_CMD}")

    try:
        engine = chess.engine.SimpleEngine.popen_uci(ENGINE_CMD)
    except Exception as e:
        print(f"[worker {worker_id}] failed to start engine: {e}")
        return
    
    try:
        book = chess.polyglot.open_reader(BOOK_PATH)
    except:
        book = None

    rng = random.Random(time.time() + worker_id)

    while not stop_event.is_set():
        board = chess.Board()
        plies = 0

        # some games will be very good while
        # other games will be very chaotic. this
        # should make the model learn both weird
        # and sophisticated positions
        random_move_freq = rng.random() * 0.10

        while plies < MAX_PLIES and not stop_event.is_set():

            # random move chance
            if rng.random() < random_move_freq:
                legal_moves = list(board.legal_moves)
                move        = rng.choice(legal_moves)
                board.push(move)

            # random polyglot book move
            elif book is not None and plies < BOOK_MOVES:
                try:
                    # get all entries for current position
                    entries = list(book.find_all(board))
                    if entries:
                        # pick a completely random entry
                        entry = random.choice(entries)
                        board.push(entry.move)

                except:
                    pass

            # otherwise let the engine choose the move
            else:
                try:
                    move_depth = rng.randint(4, 10)
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
                info  = engine.analyse(board, chess.engine.Limit(depth = 8))
                score = info.get("score")

            except Exception as e:
                print(f"[worker {worker_id}] analyse() error: {e}")
                break

            if not score:
                continue

            sc = score.white()
            cp = 0.0
            if sc.is_mate():
                cp = 2000 if sc.mate() > 0 else -2000
            else:
                cp = np.clip(sc.score(), -1800, 1800)

            # score has to be color relative
            if board.turn == chess.BLACK:
                cp = -cp

            # map cp to [0,1]
            target = 1.0 / (1.0 + np.exp(-cp / 400.0))

            # generate features for the board. since we use the active first
            # layout, black has to be always mirrored, regardless of whether
            # we actually use mirroring augmentation
            w_indices,  _  = board_features(board)
            _, b_indices   = mirrored_board_features(board)

            w_np = np.array(w_indices, dtype = np.int32)
            b_np = np.array(b_indices, dtype = np.int32)

            # always put the non-mirrored position in the queue
            try:
                if board.turn == chess.WHITE:
                    samples_queue.put(((w_np, b_np), float(target)), timeout = 1.0)
                else:
                    samples_queue.put(((b_np, w_np), float(target)), timeout = 1.0)

            except Exception:
                time.sleep(0.05)

            # and if we use mirroring augmentation, white and black
            # indices are simply switched, not actually mirrored
            try:
                if board.turn == chess.WHITE:
                    samples_queue.put(((b_np, w_np), float(1.0 - target)), timeout = 1.0)
                else:
                    samples_queue.put(((w_np, b_np), float(1.0 - target)), timeout = 1.0)

            except Exception:
                time.sleep(0.05)

        # a short pause after every game
        time.sleep(0.01)

    engine.quit()
    print(f"[worker {worker_id}] stopping.")

def trainer_loop(model, samples_queue: Queue, stop_event: mp.Event):
    last_save = time.time()
    x_batch   = []  # list of tuples (active, passive)
    y_batch   = []
    global seen

    try:
        while not stop_event.is_set():
            try:
                x, y = samples_queue.get(timeout = 1.0)
            except Exception:
                continue

            x_batch.append(x)
            y_batch.append(y)

            if len(x_batch) >= BATCH_SIZE:
                # build ragged tensors separately for both accumulators
                actives  = [pair[0] for pair in x_batch]
                passives = [pair[1] for pair in x_batch]

                x_actives  = tf.ragged.constant(actives,  dtype = tf.int32)
                x_passives = tf.ragged.constant(passives, dtype = tf.int32)
                y_np = np.array(y_batch, dtype = np.float32).reshape(-1, 1)

                loss, mae = model.train_on_batch(
                    [x_actives, x_passives],
                    y_np
                )

                seen += len(x_batch)
                x_batch.clear()
                y_batch.clear()

                timestamp = datetime.now().isoformat()
                print(f"[{timestamp}] samples: {seen} loss: {loss:.6f} mae: {mae:.6f}")

                # CSV log (logs are limited to avoid spam)
                if (seen % 65536 == 0):
                    with open('log.csv', "a") as f:
                        f.write(f"{seen},{loss:.6f},{mae:.6f},{timestamp}\n")

            if time.time() - last_save > SAVE_EVERY_SEC:
                print("saving current model version...")
                model.save(MODEL_DIR, include_optimizer = True)
                last_save = time.time()

    except KeyboardInterrupt:
        print("training interrupted, exiting gracefully...")

    finally:
        model.save(MODEL_DIR, include_optimizer = True)
        stop_event.set()

def main():
    # load or build
    if os.path.exists(MODEL_DIR):
        try:
            print(f"\nloading model from {MODEL_DIR}\n")
            model = tf.keras.models.load_model(
                MODEL_DIR,
                custom_objects = {"CReLU": CReLU},
                safe_mode      = False
            )

            # force Lambda layers to see tf in globals (if model has Lambda)
            for layer in model.layers:
                if isinstance(layer, keras.layers.Lambda):
                    fn_globals = layer.function.__globals__
                    fn_globals['tf'] = tf

        except Exception as e:
            print(f"\nload failed: {e}, building new model...\n")
            model = build_model()
    else:
        model = build_model()
        print("\nfinished building new model.\n")

    try:
        save_model_diag(model, 'architecture.png')
    except:
        print("\nsaving model diagram failed.\n")

    # create the CSV log file if it doesn't exist
    if not os.path.exists('log.csv'):
        with open('log.csv', "w") as f:
            f.write("samples,loss,mae,timestamp\n")

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
            p.join(timeout=2)

if __name__ == "__main__":
    main()
