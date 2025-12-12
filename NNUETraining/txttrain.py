import os
import glob
import json
import numpy as np
import tensorflow as tf
import keras
from keras import layers, models, optimizers, losses
import chess

# -------------------------
# SETTINGS
# -------------------------
DATA_DIR      = "C:\\Users\\michn\\Desktop\\positions"        # folder with your 12 text files
WEIGHTS_PATH  = "weights\\nnue_weights.bin"
SHAPES_PATH   = "weights\\nnue_shapes.json"

FEATURE_COUNT = 40960
EMBED_DIM     = 256
H1_NEURONS    = 16
H2_NEURONS    = 32

LEARNING_RATE = 1e-2
BATCH_SIZE    = 4096
EPOCHS        = 2   # increase later

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

# -------------------------
# FEATURE INDEXING (UNCHANGED)
# -------------------------

def feature_index(king_square: int, piece_type: int, is_black: bool, piece_square: int) -> int:

    piece_type_idx = piece_type - 1
    color_bit      = 1 if is_black else 0

    king_offset  = king_square * 640
    piece_offset = (piece_type_idx * 2 + color_bit) * 64 + piece_square

    return int(king_offset + piece_offset)


def board_features(board: chess.Board):
    w_indices = []
    b_indices = []

    w_king_sq = board.king(chess.WHITE)
    b_king_sq = board.king(chess.BLACK)

    # if a king is missing (shouldn't happen in legal positions), we still produce empty lists.
    if w_king_sq is None or b_king_sq is None:
        return []

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

        w_indices.append(idx_w)
        b_indices.append(idx_b)

    return w_indices, b_indices


# -------------------------
# MODEL (UNCHANGED)
# -------------------------

def ClippedReLU(x):
    return keras.activations.relu(x, max_value=1.0)

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

    model = models.Model([inp_active, inp_passive, inp_pcnt], select)
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

# -------------------------
# SCORE → TARGET
# -------------------------

def score_to_target(cp: float) -> float:
    cp = np.clip(cp, -1800, 1800)
    return 1.0 / (1.0 + np.exp(-cp / 300.0))


# -------------------------
# LOAD DATA
# -------------------------

def data_generator():

    files = sorted(glob.glob(os.path.join(DATA_DIR, "*.txt")))

    for file in files:
        print("Reading:", file)

        with open(file, "r", encoding="utf-8") as f:
            for line in f:
                try:
                    fen, cp = line.strip().split(";")
                    cp = float(cp)
                    board = chess.Board(fen)

                    w, b = board_features(board)

                    if board.turn == chess.WHITE:
                        active, passive = w, b
                    else:
                        active, passive = b, w

                    pcnt = len(active) + 2
                    target = score_to_target(cp)

                    if pcnt > 32:
                        continue

                    yield np.array(active, np.int32), np.array(passive, np.int32), np.int32(pcnt), np.float32(target)

                except:
                    continue

def save_pure_weights(model):
    weights = model.get_weights()

    shapes = [w.shape for w in weights]
    flat = np.concatenate([w.flatten() for w in weights]).astype(np.float32)

    with open(WEIGHTS_PATH, "wb") as f:
        flat.tofile(f)

    with open(SHAPES_PATH, "w") as f:
        json.dump([list(s) for s in shapes], f)

    print(f"\n✅ Saved pure weights to {WEIGHTS_PATH}")
    print(f"✅ Saved shapes to {SHAPES_PATH}")


def load_pure_weights(model):
    if not os.path.exists(WEIGHTS_PATH) or not os.path.exists(SHAPES_PATH):
        print("No pure weights found, starting fresh.")
        return

    with open(SHAPES_PATH, "r") as f:
        shapes = json.load(f)

    flat = np.fromfile(WEIGHTS_PATH, dtype=np.float32)

    weights = []
    idx = 0
    for shape in shapes:
        size = np.prod(shape)
        w = flat[idx:idx + size].reshape(shape)
        weights.append(w)
        idx += size

    model.set_weights(weights)

    print(f"\n✅ Loaded pure weights from {WEIGHTS_PATH}")


# -------------------------
# TRAIN
# -------------------------

def train():

    model = build_model()
    model.summary()

    load_pure_weights(model)

    active_batch  = []
    passive_batch = []
    pcnt_batch    = []
    y_batch       = []

    seen = 0

    for active, passive, pcnt, y in data_generator():

        active_batch.append(active)
        passive_batch.append(passive)
        pcnt_batch.append(pcnt)
        y_batch.append(y)

        if len(active_batch) >= BATCH_SIZE:

            x_active  = tf.ragged.constant(active_batch)
            x_passive = tf.ragged.constant(passive_batch)
            x_pcnt    = np.array(pcnt_batch)
            y_np      = np.array(y_batch).reshape(-1, 1)

            loss, mae = model.train_on_batch(
                [x_active, x_passive, x_pcnt],
                y_np
            )

            seen += len(active_batch)
            print(f"samples: {seen}   loss: {loss:.6f}   mae: {mae:.6f}")

            if (seen % 32768 == 0):
                with open('log.csv', "a") as f:
                    f.write(f"{seen},{loss:.6f},{mae:.6f}\n")

            active_batch.clear()
            passive_batch.clear()
            pcnt_batch.clear()
            y_batch.clear()

    save_pure_weights(model)
    print("\n✅ Training finished. Pure weights saved.")


if __name__ == "__main__":
    train()
