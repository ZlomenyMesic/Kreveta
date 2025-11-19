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
ENGINE_CMD = "C:\\Users\\michn\\Downloads\\Stockfish.exe"

EVAL_TIME         = 0.4   # the time for the engine to evaluate each position (in seconds)
NUM_WORKERS       = 10     # the number of individual "threads" working in parallel 

# the number of unique vectors that will be used in the accumulator.
# corresponds to 2 colors * 6 piece types * 64 squares = 768 features
NUM_FEATURES      = 768
EMBED_DIM         = 256    # dimensions/size of the feature embedding vectors
H1_NEURONS        = 32     # number of neurons in the first hidden layer
H2_NEURONS        = 32     # second hidden layer
LEARNING_RATE     = 1e-5
BATCH_SIZE        = 1500

SAMPLES_QUEUE_MAX = 10000
SAVE_EVERY_SEC    = 300    # the current model version is saved every once in a while
MAX_PLIES         = 225    # stop self-play games after this many plies

# maps a piece of certain color and position combo
# to a single feature index in range 0-767. color
# becomes 1 for black, 0 for white
def feature_index(piece_type: int, color: bool, square: int) -> int:
    return (int(color) * 6 + (piece_type - 1)) * 64 + square

# mirror a single feature index (0..767) -> returns mirrored index (0..767).
# for every position in the training batch we also train on the mirrored one
def mirror_feature_index(idx: int) -> int:
    # decode
    square     = idx   % 64
    rest       = idx  // 64
    color_bit  = rest // 6  # 0 for white, 1 for black
    piece_type = rest  % 6  # 0..5

    # mirror square and flip color
    mirrored_square    = chess.square_mirror(square)
    mirrored_color_bit = 1 - color_bit

    return (mirrored_color_bit * 6 + piece_type) * 64 + mirrored_square

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

def build_model():
    # the input layer takes at most 32 feature indices
    input = layers.Input(
        shape  = (None,),
        ragged = True,
        dtype  = 'int32',
        name   = 'FeatureIndices'
    )

    # the embedding layer - maps each feature index to its
    # respective feature embedding vector (256-dimensional)
    embedding = layers.Embedding(
        NUM_FEATURES,
        EMBED_DIM,
        name = 'Embedding'
    )(input)

    # all embedding vectors are summed (future accumulator)
    summed = layers.Lambda(
        lambda x: tf.reduce_sum(x, axis = 1),
        output_shape = (EMBED_DIM,),
        name         = 'EmbeddingSum'
    )(embedding)

    h1 = layers.Dense(H1_NEURONS, activation = 'relu', name = "Hidden_1")(summed)
    h2 = layers.Dense(H2_NEURONS, activation = 'relu', name = "Hidden_2")(h1)
    
    # the output layer is a single sigmoid-activation neuron.
    # values closer to 1 denote a position better for white,
    # while values closer to 0 are better for black
    output = layers.Dense(1, activation = 'sigmoid', name = 'Output')(h2)

    # compile the model
    model = models.Model(input, output)
    model.compile(
        optimizer = optimizers.AdamW(
            learning_rate = LEARNING_RATE,
            weight_decay  = 1e-5,
            clipnorm      = 1.0
        ),
        loss      = losses.MeanSquaredError(),
        metrics   = [keras.metrics.MeanAbsoluteError(name = 'mae')]
    )
    return model

def engine_worker(worker_id: int, samples_queue: Queue, stop_event: mp.Event):
    print(f"[worker {worker_id}] starting self-play; cmd = {ENGINE_CMD}")

    try:
        engine = chess.engine.SimpleEngine.popen_uci(ENGINE_CMD)
        #engine.configure({
        #    "PolyglotBook":    "C:\\Users\\michn\\Downloads\\rodent.bin",
        #    "PolyglotUseBook": True,
        #    "PrintStats":      False
        #})

    except Exception as e:
        print(f"[worker {worker_id}] failed to start engine: {e}")
        return

    rng = random.Random(time.time() + worker_id)

    while not stop_event.is_set():
        board = chess.Board()
        plies = 0
        while plies < MAX_PLIES and not stop_event.is_set():

            # 30 % chance for random moves during the game.
            if rng.random() < 0.3:
                legal_moves = list(board.legal_moves)
                move        = rng.choice(legal_moves)
                board.push(move)

            else:
                try:
                    # sometimes limit time more for randomness
                    move_time = EVAL_TIME * rng.uniform(0.3, 1.0)
                    result    = engine.play(board, chess.engine.Limit(time = move_time))

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
                info  = engine.analyse(board, chess.engine.Limit(time = EVAL_TIME))
                score = info.get("score")

            except Exception as e:
                print(f"[worker {worker_id}] analyse() error: {e}")
                break

            cp = 0.0
            if score:
                sc = score.white()
                if sc.is_mate():
                    cp = 1500 if sc.mate() > 0 else -1500
                else:
                    # Kreveta already clips the score into [-1000..1000]
                    cp = np.clip(sc.score(), -1350, 1350)
            else: continue

            # squeeze all evals into [0..1] interval
            target = 1.0 / (1.0 + np.exp(-cp / 400.0))

            # feature indices for this position, and for a mirrored 
            # version - opposite indices and piece colors
            indices  = board_features(board)
            mirrored = mirror_indices(indices)

            try:
                # add the features to the queue along with their evaluations
                samples_queue.put((np.array(indices,  dtype = np.int32), float(target)),       timeout = 1.0)
                samples_queue.put((np.array(mirrored, dtype = np.int32), float(1.0 - target)), timeout = 1.0)
                
            except Exception:
                time.sleep(0.05)

        # brief pause between games
        time.sleep(0.1)

    engine.quit()
    print(f"[worker {worker_id}] Stopping.")

def trainer_loop(model, samples_queue: Queue, stop_event: mp.Event):
    last_save = time.time()
    x_batch   = []
    y_batch   = []
    seen      = 0

    try:
        while not stop_event.is_set():
            try:
                x, y = samples_queue.get(timeout = 1.0)

            except Exception:
                continue

            x_batch.append(x)
            y_batch.append(y)

            if len(x_batch) >= BATCH_SIZE:
                x_ragged = tf.ragged.constant(x_batch, dtype = tf.int32)
                y_np     = np.array(y_batch, dtype = np.float32).reshape(-1, 1)

                loss, mae = model.train_on_batch(x_ragged, y_np)

                seen += len(x_batch)
                x_batch.clear()
                y_batch.clear()

                timestamp = datetime.now().isoformat()
                log_info  = f"[{timestamp}] samples: {seen} loss: {loss:.5f} mae: {mae:.5f}"

                print(log_info)
                with open("log.txt", "a") as f:
                    f.write(log_info + '\n')

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
    # build or load model
    if os.path.exists(MODEL_DIR):
        try:
            print(f"loading model from {MODEL_DIR}")
            model = tf.keras.models.load_model(
                MODEL_DIR,
                safe_mode = False
            )

            # force the embedding sum lambda layer to know about tf
            for layer in model.layers:
                if isinstance(layer, keras.layers.Lambda):
                    fn_globals = layer.function.__globals__
                    fn_globals['tf'] = tf

        except Exception as e:
            print(f"load failed: {e}, building new model...")
            model = build_model()

    else:
        model = build_model()
        print("finished building new model.")

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
            p.join(timeout = 2)

if __name__ == "__main__":
    main()
