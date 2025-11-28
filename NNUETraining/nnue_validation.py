#
# Kreveta chess engine by ZlomenyMesic
# started 4-3-2025
#

import os
import json

import numpy as np
import tensorflow as tf
from keras import layers, models, losses, activations, metrics
import chess

# only log warnings and errors
os.environ["TF_CPP_MIN_LOG_LEVEL"] = "2"

# model path is the name of the loaded model
SCRIPT_DIR   = os.path.dirname(os.path.abspath(__file__))
WEIGHTS_PATH = os.path.join(SCRIPT_DIR, "weights\\nnue_weights.bin")
SHAPES_PATH  = os.path.join(SCRIPT_DIR, "weights\\nnue_shapes.json")

FEATURE_COUNT     = 40960
EMBED_DIM         = 256
H1_NEURONS        = 16
H2_NEURONS        = 32

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

def ClippedReLU(x):
    return activations.relu(x, max_value = 1.0)

def build_model():
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
    for i in range(4):
        # two dense layers, CReLU activation
        h1 = layers.Dense(H1_NEURONS, activation = ClippedReLU, name = f'Subnet_{i}_Dense16')(concat)
        h2 = layers.Dense(H2_NEURONS, activation = ClippedReLU, name = f'Subnet_{i}_Dense32')(h1)
        subnets.append(h2)

    # stack the 4 subnet outputs into shape (batch, 4, H2_NEURONS)
    # wrap tf.stack into a Lambda so we only use Keras layers on KerasTensors
    stacked = layers.Lambda(lambda inputs: tf.stack(inputs, axis = 1), name = 'Stack_Subnets')(subnets)

    # select the right subnet according to piece count bucket
    def select_fn(args):
        stacked_tensor, pc = args

        # pc has shape (batch,) integer counts
        bucket  = tf.clip_by_value(pc // 8, 0, 3)
        one_hot = tf.one_hot(bucket, depth = 4, dtype = stacked_tensor.dtype)

        # expand one_hot to (batch, 4, 1) to multiply with stacked (batch, 4, H)
        one_hot = one_hot[..., None]
        return tf.reduce_sum(stacked_tensor * one_hot, axis = 1) # (batch, H2_NEURONS)

    select = layers.Lambda(
        select_fn,
        name         = 'SelectSubnet',
        output_shape = (H2_NEURONS,)
    )([stacked, inp_pcnt])

    output = layers.Dense(
        1,
        activation = 'sigmoid',
        name       = 'Output'
    )(select)

    model = models.Model([inp_active, inp_passive, inp_pcnt], output)
    model.compile(
        optimizer = None,
        loss    = losses.BinaryCrossentropy(),
        metrics = [metrics.MeanAbsoluteError(name = 'mae')]
    )
    return model

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
        flat = np.fromfile(weights_path, dtype=np.float32)
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
        w = flat[idx: idx+size].reshape(tuple(shape)).astype(np.float32)
        weights.append(w)
        idx += size

    try:
        model.set_weights(weights)
    except Exception as e:
        print("model.set_weights failed:", e)
        return False

    return True

model = build_model()
load_weights_binary(model)

# set of tested positions. feel free to add new ones
test_positions = {
    "start position":        chess.Board(),
    "white up a queen":      chess.Board("4k3/8/8/8/8/8/8/3QK3 w - - 0 1"),
    "black up a queen":      chess.Board("3qk3/8/8/8/8/8/8/4K3 w - - 0 1"),
    "white queen x rook":    chess.Board("8/4r3/3k4/8/8/5Q2/2K5/8 w - - 0 1"),
    "white queen x rook 2":  chess.Board("8/Q7/3k4/8/8/5r2/2K5/8 w - - 0 1"),
    "black queen x rook":    chess.Board("8/4R3/3K4/8/8/5q2/2k5/8 w - - 0 1"),
    "black queen x rook 2":  chess.Board("8/q7/3K4/8/8/5R2/2k5/8 w - - 0 1"),
    "1. e4 (good)":          chess.Board("rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR w KQkq e3 0 1"),
    "1. f4 (bad)":           chess.Board("rnbqkbnr/pppppppp/8/8/5P2/8/PPPPP1PP/RNBQKBNR w KQkq e3 0 1"),
    "1. e4 e5":              chess.Board("rnbqkbnr/pppp1ppp/8/4p3/4P3/8/PPPP1PPP/RNBQKBNR w KQkq e6 0 1"),
    "1. e4 c5":              chess.Board("rnbqkbnr/pp1ppppp/8/2p5/4P3/8/PPPP1PPP/RNBQKBNR w KQkq c6 0 1"),
}

# evaluate all positions
for name, board in test_positions.items():
    w_indices, b_indices = board_features(board)
    
    xw    = tf.ragged.constant([w_indices], dtype = tf.int32)
    xb    = tf.ragged.constant([b_indices], dtype = tf.int32)
    xpcnt = np.array([len(w_indices) + 2],  dtype = np.int32)

    predict = float(model.predict(
        [xw, xb, xpcnt] if board.turn == chess.WHITE else [xb, xw, xpcnt],
        verbose = 0
    )[0][0])

    if (board.turn == chess.BLACK):
        predict = 1.0 - predict

    print(board)
    print(f"FEN:       {board.fen()}")
    print(f"Position:  {name}")
    print(f"NNUE Eval: {predict:.5f}\n")