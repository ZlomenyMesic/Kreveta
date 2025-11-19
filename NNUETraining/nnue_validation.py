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

# maps a piece of certain color and position combo
# to a single feature index in range 0-767. color
# becomes 0 for black, 1 for white
def feature_index(piece_type: int, color: bool, square: int) -> int:
    return (int(color) * 6 + (piece_type - 1)) * 64 + square

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

# next try to load the actual model
print(f"Loading model from {MODEL_PATH} ...")

model = tf.keras.models.load_model(
    MODEL_PATH,
    safe_mode = False
)

print("Model loaded.")

# for some reason, we must let all Lambda layers know
# about tf and screlu, since they don't know by default
for layer in model.layers:
    if isinstance(layer, keras.layers.Lambda):
        fn_globals = layer.function.__globals__
        fn_globals['tf']        = tf

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
    indices = board_features(board)
    expand  = np.expand_dims(np.array(indices, dtype = np.int32), axis = 0)
    predict = float(model.predict(expand, verbose=0)[0][0])

    print(board)
    print(f"FEN:       {board.fen()}")
    print(f"Position:  {name}")
    print(f"NNUE Eval: {predict:.4f}\n")