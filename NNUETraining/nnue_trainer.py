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

# only log warnings and errors
os.environ["TF_CPP_MIN_LOG_LEVEL"] = "2"

# the absolute path to where the script is running
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))

# the location and name of the trained model that is to be
# saved. if previously trained models are imported, these
# names must be identical (old model will be overwritten)
MODEL_DIR = os.path.join(SCRIPT_DIR, "nnue_model.keras")

# the absolute path to Stockfish/any other UCI engine,
# that is used for self-play and position evaluation
STOCKFISH_CMD = "C:\\Users\\michn\\Downloads\\Stockfish.exe"

EVAL_TIME         = 0.4    # the time for the engine to evaluate each position (in seconds)
NUM_WORKERS       = 10     # the number of individual "threads" working in parallel 
BATCH_SIZE        = 2048
EMBED_DIM         = 256    # dimensions/size of the feature embedding vectors
HIDDEN_NEURONS    = 32     # number of neurons in the hidden layers
LEARNING_RATE     = 1e-4
SAMPLES_QUEUE_MAX = 10000
SAVE_EVERY_SEC    = 300    # the current model version is saved every once in a while
MAX_PLIES         = 200    # stop self-play games after this many plies

# the number of unique vectors that will be used in the accumulator.
# corresponds to 2 colors * 6 piece types * 64 squares = 768 features
NUM_FEATURES = 768

# maps a piece of certain color and position combo
# to a single feature index in range 0-767. color
# becomes 1 for black, 0 for white
def feature_index(piece_type: int, color: bool, square: int) -> int:
    return (int(color) * 6 + (piece_type - 1)) * 64 + square

# mirror a single feature index (0..767) -> returns mirrored index (0..767).
# for every position in the training batch we also train on the mirrored one
def mirror_feature_index(idx: int) -> int:
    # decode
    square     = idx % 64
    rest       = idx // 64
    color_bit  = rest // 6       # 0 for white, 1 for black
    piece_type = (rest % 6) + 1  # 1..6

    # mirror square and flip color
    mirrored_square    = chess.square_mirror(square)
    mirrored_color_bit = 1 - color_bit

    return (mirrored_color_bit * 6 + (piece_type - 1)) * 64 + mirrored_square

# mirror a list of feature indices
def mirror_indices(indices: list) -> list:
    return [mirror_feature_index(i) for i in indices]

# creates a list of all active features on the board
def board_features(board: chess.Board):
    indices = []
    for sq in chess.SQUARES:
        piece = board.piece_at(sq)
        
        # only add features if the piece exists
        if piece:
            indices.append(feature_index(
                piece.piece_type,
                piece.color == chess.BLACK,
                sq
            ))
            
    return indices

# SCReLU activation function
@tf.function
def screlu_tf(x):
    return tf.square(tf.clip_by_value(x, 0.0, 1.0))

def SCReLU():
    # output shape must be present, so Keras know it's the same as input
    return layers.Lambda(lambda x: screlu_tf(x), 
                         name         = "SCReLU", 
                         output_shape = lambda s: s)

def build_model(num_features = NUM_FEATURES, embed_dim = EMBED_DIM, hidden_units = HIDDEN_NEURONS):
    # the input layer takes 32 feature indices
    inp = layers.Input(
        shape = (32,),
        dtype = 'int32',
        name  = 'feature_indices'
    )

    # then shift the indices to be non-negative
    shifted = layers.Lambda(lambda x: x + 1,
                            output_shape = lambda s: s,
                            name         = 'shift_indices')(inp)

    # the embedding layer - maps each feature index to its
    # respective feature embedding vector (128-dimensional)
    embedding = layers.Embedding(num_features + 1, embed_dim, name = 'feature_embedding')(shifted)

    # all embedding vectors are summed (future accumulator)
    summed = layers.Lambda(lambda x: tf.reduce_sum(x, axis = 1),
                           output_shape = lambda s: (s[0], s[2]),
                           name         = 'embed_sum')(embedding)

    # a hidden dense layer and SCReLU activation
    hidden    = layers.Dense(hidden_units)(summed)
    activated = SCReLU()(hidden)
    
    # the output layer is a single sigmoid-activation neuron.
    # values closer to 1 denote a position better for white,
    # while values closer to 0 are better for black
    output = layers.Dense(1, activation = 'sigmoid')(activated)

    # compile the model
    model = models.Model(inp, output)
    model.compile(
        optimizer = optimizers.Adam(LEARNING_RATE),
        loss      = losses.BinaryCrossentropy(),
        metrics   = [tf.keras.metrics.MeanSquaredError(name = 'mse')]
    )
    return model

def stockfish_worker(worker_id: int, samples_queue: Queue, stop_event: mp.Event):
    print(f"[worker {worker_id}] starting self-play, stockfish = {STOCKFISH_CMD}")

    try:
        engine = chess.engine.SimpleEngine.popen_uci(STOCKFISH_CMD)
        engine.configure({"Hash": 16})

    except Exception as e:
        print(f"[worker {worker_id}] failed to start Stockfish: {e}")
        return

    rng = random.Random(time.time() + worker_id)

    while not stop_event.is_set():
        board = chess.Board()
        plies = 0
        while not board.is_game_over() and plies < MAX_PLIES and not stop_event.is_set():

            # 25 % chance for random moves during the game.
            if rng.random() < 0.25:
                legal_moves = list(board.legal_moves)
                move        = rng.choice(legal_moves)
                board.push(move)

            else:
                try:
                    # sometimes limit time more for randomness
                    move_time = EVAL_TIME * rng.uniform(0.1, 1.0)
                    result = engine.play(board, chess.engine.Limit(time = move_time))
                    if result.move is None:
                        break
                    
                    board.push(result.move)
                except Exception as e:
                    print(f"[worker {worker_id}] play() error: {e}")
                    break

            plies += 1

            try:
                info  = engine.analyse(board, chess.engine.Limit(time = EVAL_TIME))
                score = info.get("score")

            except Exception as e:
                print(f"[worker {worker_id}] analyse() error: {e}")
                break

            cp = 0.0
            if score:
                sc = score.white()
                if sc.is_mate():
                    cp = 1000 if sc.mate() > 0 else -1000
                else:
                    cp = np.clip(sc.score(), -1000, 1000)

            # squeeze all evals into 0..1 interval
            target = 1.0 / (1.0 + np.exp(-cp / 400.0))

            # the feature indices for this position
            indices = board_features(board)

            # mirror the board and piece colors, and compute indices as well
            mirrored = mirror_indices(indices)

            # padd all indices to 32 (input layer size)
            padded_original = indices  + [-1] * (32 - len(indices))
            padded_mirror   = mirrored + [-1] * (32 - len(mirrored))

            try:
                samples_queue.put((np.array(padded_original, dtype = np.int32), float(target)),       timeout=1.0)
                samples_queue.put((np.array(padded_mirror,   dtype = np.int32), float(1.0 - target)), timeout=1.0) # mirrored target
                
            except Exception:
                time.sleep(0.05)

        # brief pause between games
        time.sleep(0.05)

    engine.quit()
    print(f"[worker {worker_id}] stopping.")

def trainer_loop(model, samples_queue: Queue, stop_event: mp.Event):
    last_save = time.time()
    x_batch, y_batch = [], []
    seen = 0
    try:
        while not stop_event.is_set():
            try:
                x, y = samples_queue.get(timeout = 1.0)
            except Exception:
                continue

            x_batch.append(x)
            y_batch.append(y)

            if len(x_batch) >= BATCH_SIZE:
                x_np = np.stack(x_batch, axis = 0)
                y_np = np.array(y_batch, dtype = np.float32).reshape(-1, 1)

                loss, mse = model.train_on_batch(x_np, y_np)

                seen += len(x_batch)
                x_batch.clear()
                y_batch.clear()

                timestamp = datetime.now().isoformat()
                log_info  = f"[{timestamp}] samples = {seen} loss = {loss:.6f} MSE = {mse:.6f}"

                print(log_info)
                with open("log.txt", "a") as f:
                    f.write(log_info + '\n')

            if time.time() - last_save > SAVE_EVERY_SEC:
                print("Saving current model version...")
                model.save(MODEL_DIR, include_optimizer = True)
                last_save = time.time()

    except KeyboardInterrupt:
        print("Training interrupted, exiting gracefully...")
    finally:
        model.save(MODEL_DIR, include_optimizer = True)
        stop_event.set()

def main():
    # build or load model
    if os.path.exists(MODEL_DIR):
        try:
            print(f"Loading model from {MODEL_DIR}")
            model = tf.keras.models.load_model(
                MODEL_DIR,
                custom_objects = {'SCReLU': SCReLU, 'screlu_tf': screlu_tf},
                safe_mode      = False
            )

            # --- force all Lambda layers to know about tf and screlu_tf ---
            for layer in model.layers:
                if isinstance(layer, keras.layers.Lambda):
                    fn_globals = layer.function.__globals__
                    fn_globals['tf']        = tf
                    fn_globals['screlu_tf'] = screlu_tf

        except Exception as e:
            print(f"Load failed: {e}, building new model...")
            model = build_model()
    else:
        model = build_model()
        print("Finished building new model.")

    mp_ctx        = mp.get_context("spawn")
    samples_queue = mp_ctx.Queue(maxsize = SAMPLES_QUEUE_MAX)
    stop_event    = mp_ctx.Event()

    workers = []
    for i in range(NUM_WORKERS):
        p = mp_ctx.Process(
            target = stockfish_worker,
            args   = (i, samples_queue, stop_event),
            daemon = True
        )
        p.start()
        workers.append(p)

    def handle_sigint(signum, frame):
        print("SIGINT received; stopping...")
        stop_event.set()
    signal.signal(signal.SIGINT, handle_sigint)

    try:
        trainer_loop(model, samples_queue, stop_event)
    finally:
        print("Waiting for workers...")
        stop_event.set()
        for p in workers:
            p.join(timeout = 2)

if __name__ == "__main__":
    main()
