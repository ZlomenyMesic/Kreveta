# rewritten_streaming_trainer.py
import os
import json
import time
from datetime import datetime
import random

import numpy as np
import tensorflow as tf
from keras import layers, models, optimizers, losses
import keras
import chess

import pyarrow.dataset as ds

# only log warnings and errors
os.environ["TF_CPP_MIN_LOG_LEVEL"] = "2"

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))

# model / weights (keep your original paths)
MODEL_DIR    = os.path.join(SCRIPT_DIR, "nnue_model.keras")
WEIGHTS_PATH = os.path.join(SCRIPT_DIR, "weights", "nnue_weights.bin")
SHAPES_PATH  = os.path.join(SCRIPT_DIR, "weights", "nnue_shapes.json")

# DATA
PARQUET_FILES = [
    "C:\\Users\\michn\\Downloads\\archive\\train-00000-of-00016.parquet",
    "C:\\Users\\michn\\Downloads\\archive\\train-00001-of-00016.parquet",
    "C:\\Users\\michn\\Downloads\\archive\\train-00002-of-00016.parquet",
    "C:\\Users\\michn\\Downloads\\archive\\train-00003-of-00016.parquet",
    "C:\\Users\\michn\\Downloads\\archive\\train-00004-of-00016.parquet",
    "C:\\Users\\michn\\Downloads\\archive\\train-00005-of-00016.parquet",
    "C:\\Users\\michn\\Downloads\\archive\\train-00006-of-00016.parquet",
    "C:\\Users\\michn\\Downloads\\archive\\train-00007-of-00016.parquet",
    "C:\\Users\\michn\\Downloads\\archive\\train-00008-of-00016.parquet",
    "C:\\Users\\michn\\Downloads\\archive\\train-00009-of-00016.parquet",
    "C:\\Users\\michn\\Downloads\\archive\\train-00010-of-00016.parquet",
    "C:\\Users\\michn\\Downloads\\archive\\train-00011-of-00016.parquet",
    "C:\\Users\\michn\\Downloads\\archive\\train-00012-of-00016.parquet",
    "C:\\Users\\michn\\Downloads\\archive\\train-00013-of-00016.parquet",
    "C:\\Users\\michn\\Downloads\\archive\\train-00014-of-00016.parquet",
    "C:\\Users\\michn\\Downloads\\archive\\train-00015-of-00016.parquet",
]

if not PARQUET_FILES:
    PARQUET_FILES = sorted([os.path.join(SCRIPT_DIR, f) for f in os.listdir(SCRIPT_DIR) if f.endswith(".parquet")])

# hyperparams (tweak as needed)
FEATURE_COUNT      = 40960
EMBED_DIM          = 192
H1_NEURONS         = 16
H2_NEURONS         = 16
LEARNING_RATE      = 1e-3
BATCH_SIZE         = 4096  # training batch size (keeps memory reasonable)
SAVE_EVERY_SEC     = 300
PARQUET_BATCH_ROWS = 5000  # how many rows per pyarrow record batch read (tune)
DEPTH_MIN          = 22

# ---------- keep feature index / board_features as in original ----------
def feature_index(king_square: int, piece_type: int, is_black: bool, piece_square: int) -> int:
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
    if w_king_sq is None or b_king_sq is None:
        return [], []
    for sq in chess.SQUARES:
        piece = board.piece_at(sq)
        if piece is None:
            continue
        if piece.piece_type == chess.KING:
            continue
        is_black = piece.color == chess.BLACK
        idx_w = feature_index(
            king_square  = w_king_sq,
            piece_type   = piece.piece_type,
            is_black     = is_black,
            piece_square = sq
        )
        idx_b = feature_index(
            king_square  = b_king_sq ^ 56,
            piece_type   = piece.piece_type,
            is_black     = not is_black,
            piece_square = sq ^ 56
        )
        w_indices.append(idx_w)
        b_indices.append(idx_b)
    return w_indices, b_indices

def ClippedReLU(x):
    return keras.activations.relu(x, max_value = 1.0)

def build_model() -> keras.Model:
    inp_active  = layers.Input(shape = (None,), ragged = True, dtype = 'int32', name = 'Input_Active')
    inp_passive = layers.Input(shape = (None,), ragged = True, dtype = 'int32', name = 'Input_Passive')
    inp_pcnt    = layers.Input(shape = (), dtype = 'int32', name = 'Input_PieceCount')

    emb_shared = layers.Embedding(input_dim = FEATURE_COUNT, output_dim = EMBED_DIM, name = 'Embedding_Shared')
    emb_active  = emb_shared(inp_active)
    emb_passive = emb_shared(inp_passive)

    summed_active = layers.Lambda(lambda x: tf.reduce_sum(x, axis = 1), output_shape = (EMBED_DIM,), name='Accumulator_Active')(emb_active)
    summed_passive = layers.Lambda(lambda x: tf.reduce_sum(x, axis = 1), output_shape = (EMBED_DIM,), name='Accumulator_Passive')(emb_passive)

    concat = layers.Concatenate(name = 'Accumulator_Concat')([summed_active, summed_passive])

    subnets = []
    for i in range(8):
        h1 = layers.Dense(H1_NEURONS, activation = ClippedReLU, name = f'Subnet_{i}_Dense_1')(concat)
        h2 = layers.Dense(H2_NEURONS, activation = ClippedReLU, name = f'Subnet_{i}_Dense_2')(h1)
        output = layers.Dense(1, activation = 'sigmoid', name = f'Subnet_{i}_Output')(h2)
        subnets.append(output)

    stacked = layers.Lambda(lambda inputs: tf.stack(inputs, axis = 1), name = 'Stack_Subnets')(subnets)

    def select_fn(args):
        stacked_tensor, pc = args
        bucket = tf.clip_by_value(pc // 4, 0, 7)
        batch_idx = tf.range(tf.shape(bucket)[0])
        indices   = tf.stack([batch_idx, bucket], axis = 1)
        selected  = tf.gather_nd(stacked_tensor, indices)
        return selected

    select = layers.Lambda(select_fn, name = 'Select_Subnet', output_shape = (1,))([stacked, inp_pcnt])

    model = models.Model([inp_active, inp_passive, inp_pcnt], select)
    model.compile(
        optimizer = optimizers.AdamW(learning_rate = LEARNING_RATE, weight_decay = 1e-5, clipnorm = 1.0),
        loss    = losses.BinaryCrossentropy(),
        metrics = [keras.metrics.MeanAbsoluteError(name = 'mae')]
    )
    return model

# ---------- keep binary save/load ----------
def save_weights_binary(model, weights_path = WEIGHTS_PATH, shapes_path = SHAPES_PATH):
    weights = model.get_weights()
    shapes = [list(w.shape) for w in weights]
    flat = np.concatenate([w.ravel().astype(np.float32) for w in weights]) if len(weights) else np.array([], dtype = np.float32)
    os.makedirs(os.path.dirname(weights_path), exist_ok=True)
    flat.tofile(weights_path)
    with open(shapes_path, "w") as f:
        json.dump(shapes, f)
    return shapes, flat.size

def load_weights_binary(model, weights_path = WEIGHTS_PATH, shapes_path = SHAPES_PATH):
    if not os.path.exists(weights_path) or not os.path.exists(shapes_path):
        return False
    with open(shapes_path, "r") as f:
        shapes = json.load(f)
    total = 0
    sizes = []
    for s in shapes:
        size = int(np.prod(s))
        sizes.append(size)
        total += size
    try:
        flat = np.fromfile(weights_path, dtype = np.float32)
    except Exception as e:
        print("failed to read weights file:", e)
        return False
    if flat.size != total:
        print(f"weight count mismatch: expected {total} floats, got {flat.size}.")
        return False
    weights = []
    idx = 0
    for size, shape in zip(sizes, shapes):
        w = flat[idx: idx+size].reshape(tuple(shape)).astype(np.float32)
        weights.append(w)
        idx += size
    try:
        model.set_weights(weights)
    except Exception as e:
        print("model.set_weights failed:", e)
        return False
    return True

# ---------- streaming parquet reader + trainer ----------
def sigmoid(x):
    return 1.0 / (1.0 + np.exp(-x))

def process_parquet_file(path, model, sample_accumulator, last_save_time):
    """
    Streams a parquet file and trains the model incrementally.
    sample_accumulator: dict with keys 'active', 'passive', 'pcnts', 'targets', 'seen'
    returns updated last_save_time
    """
    dataset = ds.dataset(path, format="parquet")
    rng = random.Random()

    # Older PyArrow (<14) uses Scanner.from_dataset
    try:
        scanner = ds.Scanner.from_dataset(
            dataset,
            columns=['fen', 'depth', 'cp', 'mate'],
            batch_size=PARQUET_BATCH_ROWS
        )
        batches = scanner.to_batches()

    # Newer versions have dataset.scanner()
    except AttributeError:
        scanner = dataset.scanner(
            columns=['fen', 'depth', 'cp', 'mate'],
            batch_size=PARQUET_BATCH_ROWS
        )
        batches = scanner.to_batches()

    for record_batch in batches:
        # convert to python-native lists (fast enough for our batch sizes)
        batch = record_batch.to_pydict()
        fens = batch.get('fen', [])
        depths = batch.get('depth', [])
        cps = batch.get('cp', [])
        mates = batch.get('mate', [])

        for fen, depth, cp_val, mate in zip(fens, depths, cps, mates):
            # filter depth
            try:
                if depth is None or int(depth) < DEPTH_MIN:
                    continue
            except Exception:
                continue

            # cp/mate handling
            if mate is not None:
                try:
                    mate_i = int(mate)
                except Exception:
                    mate_i = 0
                cp = 1800 if mate_i > 0 else -1800
            else:
                if cp_val is None:
                    continue
                cp = int(cp_val)

            # clamp
            cp = int(np.clip(cp, -1800, 1800))

            # parse fen to check active color and get features
            try:
                board = chess.Board(fen)
            except Exception:
                continue

            if board.turn == chess.BLACK:
                cp = -cp

            # build target label (same scaling as original)
            target = sigmoid(cp / 400.0)

            w_indices, b_indices = board_features(board)
            if not w_indices or not b_indices:
                # skip ill-formed positions
                continue

            active = w_indices if board.turn == chess.WHITE else b_indices
            passive = b_indices if board.turn == chess.WHITE else w_indices

            # append to accumulator
            sample_accumulator['active'].append(np.array(active, dtype=np.int32))
            sample_accumulator['passive'].append(np.array(passive, dtype=np.int32))
            sample_accumulator['pcnts'].append(np.int32(len(w_indices) + 2))  # same heuristic as original
            sample_accumulator['targets'].append(np.float32(target))
            sample_accumulator['seen'] += 1

            # when enough samples, train
            if len(sample_accumulator['targets']) >= BATCH_SIZE:
                # build ragged tensors
                x_active  = tf.ragged.constant(sample_accumulator['active'], dtype=tf.int32)
                x_passive = tf.ragged.constant(sample_accumulator['passive'], dtype=tf.int32)
                x_pcnts   = np.array(sample_accumulator['pcnts'], dtype=np.int32)
                y_np = np.array(sample_accumulator['targets'], dtype=np.float32).reshape(-1, 1)

                loss, mae = model.train_on_batch([x_active, x_passive, x_pcnts], y_np)
                timestamp = datetime.now().isoformat()
                print(f"[{timestamp}] samples_total: {sample_accumulator['seen']} batch_loss: {loss:.6f} mae: {mae:.6f}")

                # CSV log
                if rng.random() < 0.05:
                    with open('log.csv', "a") as f:
                        f.write(f"{sample_accumulator['seen']},{loss:.6f},{mae:.6f},{timestamp}\n")

                # clear batch arrays
                sample_accumulator['active'].clear()
                sample_accumulator['passive'].clear()
                sample_accumulator['pcnts'].clear()
                sample_accumulator['targets'].clear()

            # periodic save
            if time.time() - last_save_time > SAVE_EVERY_SEC:
                try:
                    _, count = save_weights_binary(model)
                    print(f"[{datetime.now().isoformat()}] saved weights ({count} floats) -> {WEIGHTS_PATH}, shapes -> {SHAPES_PATH}")
                except Exception as e:
                    print("failed to save weights:", e)
                last_save_time = time.time()
    return last_save_time

def main():
    model = build_model()
    print("\nbuilt new model.\n")

    if os.path.exists(WEIGHTS_PATH) and os.path.exists(SHAPES_PATH):
        print("\nfound raw weights + shapes. Attempting to load...\n")
        ok = load_weights_binary(model)
        print("load weights ok:", ok)

    # CSV log header
    if not os.path.exists('log.csv'):
        with open('log.csv', "w") as f:
            f.write("samples,loss,mae,timestamp\n")

    sample_acc = {'active': [], 'passive': [], 'pcnts': [], 'targets': [], 'seen': 0}
    last_save_time = time.time()

    try:
        for p in PARQUET_FILES:
            if not os.path.exists(p):
                print("skipping missing file:", p)
                continue
            print(f"processing parquet file: {p}")
            last_save_time = process_parquet_file(p, model, sample_acc, last_save_time)
            # after each file, optionally force a training step on remaining samples if enough
            if len(sample_acc['targets']) >= 128:
                x_active  = tf.ragged.constant(sample_acc['active'], dtype=tf.int32)
                x_passive = tf.ragged.constant(sample_acc['passive'], dtype=tf.int32)
                x_pcnts   = np.array(sample_acc['pcnts'], dtype=np.int32)
                y_np = np.array(sample_acc['targets'], dtype=np.float32).reshape(-1, 1)
                loss, mae = model.train_on_batch([x_active, x_passive, x_pcnts], y_np)
                print(f"[{datetime.now().isoformat()}] post-file flush: loss={loss:.6f} mae={mae:.6f}")
                sample_acc['active'].clear()
                sample_acc['passive'].clear()
                sample_acc['pcnts'].clear()
                sample_acc['targets'].clear()
    except KeyboardInterrupt:
        print("training interrupted by user.")
    finally:
        # final save
        try:
            _, count = save_weights_binary(model)
            print(f"[{datetime.now().isoformat()}] final save weights ({count} floats) -> {WEIGHTS_PATH}, shapes -> {SHAPES_PATH}")
        except Exception as e:
            print("final save failed:", e)

if __name__ == "__main__":
    main()
