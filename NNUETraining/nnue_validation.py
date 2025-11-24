#
# Kreveta chess engine by ZlomenyMesic
# started 4-3-2025
#

import os

import numpy as np
import tensorflow as tf
import keras
import chess

# only log warnings and errors
os.environ["TF_CPP_MIN_LOG_LEVEL"] = "2"

# model path is the name of the loaded model
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
MODEL_PATH = os.path.join(SCRIPT_DIR, "nnue_model.keras")

def feature_index_for_acc(king_square: int, piece_type: int, piece_color: bool, piece_square: int) -> int:
    # skip kings (just to make sure)
    if piece_type == chess.KING or piece_type == None:
        return -1

    # map piece_type 1..5 into 0..4
    piece_type_idx = piece_type - 1
    color_bit      = 1 if piece_color else 0

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
        piece_color_bit = piece.color == chess.BLACK

        # index into white accumulator (white king as reference)
        idx_w = feature_index_for_acc(
            king_square  = w_king_sq,
            piece_type   = piece.piece_type,
            piece_color  = piece_color_bit,
            piece_square = sq
        )
        # index into black accumulator (black king as reference)
        idx_b = feature_index_for_acc(
            king_square  = b_king_sq,
            piece_type   = piece.piece_type,
            piece_color  = piece_color_bit,
            piece_square = sq
        )

        if idx_w != -1:
            w_indices.append(idx_w)
        if idx_b != -1:
            b_indices.append(idx_b)

    return w_indices, b_indices

def CReLU(x):
    return keras.activations.relu(x, max_value = 1.0)

# next try to load the actual model
print(f"Loading model from {MODEL_PATH} ...")

model = tf.keras.models.load_model(
    MODEL_PATH,
    custom_objects = {"CReLU": CReLU},
    safe_mode      = False
)

print("Model loaded.")

# for some reason, we must let all Lambda layers know
# about tf and screlu, since they don't know by default
for layer in model.layers:
    if isinstance(layer, keras.layers.Lambda):
        fn_globals = layer.function.__globals__
        fn_globals['tf'] = tf

print("Injected tf into all Lambda layers.\n")

# set of tested positions. feel free to add new ones
test_positions = {
    "start position":        chess.Board(),
    "white up a queen":      chess.Board("4k3/8/8/8/8/8/8/3QK3 w - - 0 1"),
    "black up a queen":      chess.Board("3qk3/8/8/8/8/8/8/4K3 w - - 0 1"),
    "white queen x rook":    chess.Board("8/4r3/3k4/8/8/5Q2/2K5/8 w - - 0 1"),
    "white queen x rook 2":  chess.Board("8/Q7/3k4/8/8/5r2/2K5/8 w - - 0 1"),
    "black queen x rook":    chess.Board("8/4R3/3K4/8/8/5q2/2k5/8 w - - 0 1"),
    "black queen x rook 2":  chess.Board("8/q7/3K4/8/8/5R2/2k5/8 w - - 0 1"),
    "1. e4 (good)":          chess.Board("rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e3 0 1"),
    "1. f4 (bad)":           chess.Board("rnbqkbnr/pppppppp/8/8/5P2/8/PPPPP1PP/RNBQKBNR b KQkq e3 0 1"),
    "1. e4 e5":              chess.Board("rnbqkbnr/pppp1ppp/8/4p3/4P3/8/PPPP1PPP/RNBQKBNR w KQkq e6 0 1"),
    "1. e4 c5":              chess.Board("rnbqkbnr/pp1ppppp/8/2p5/4P3/8/PPPP1PPP/RNBQKBNR w KQkq c6 0 1"),
}

# evaluate all positions
for name, board in test_positions.items():
    w_indices, b_indices = board_features(board)
    
    xw = tf.ragged.constant([w_indices], dtype = tf.int32)
    xb = tf.ragged.constant([b_indices], dtype = tf.int32)
    predict = float(model.predict([xw, xb], verbose = 0)[0][0])

    print(board)
    print(f"FEN:       {board.fen()}")
    print(f"Position:  {name}")
    print(f"NNUE Eval: {predict:.4f}\n")