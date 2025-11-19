#
# Kreveta chess engine by ZlomenyMesic
# started 4-3-2025
#

import os
import json
import numpy as np
import tensorflow as tf

# the absolute path to where the script is running
SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))

MODEL_PATH  = os.path.join(SCRIPT_DIR, "nnue_model.keras")
WEIGHTS_BIN = os.path.join(SCRIPT_DIR, "export\\nnue_weights.bin")
SHAPES_JSON = os.path.join(SCRIPT_DIR, "export\\nnue_shapes.json")

def main():
    print("Loading model...")
    model = tf.keras.models.load_model(
        MODEL_PATH,
        safe_mode = False
    )

    weights = model.get_weights()
    shapes  = [w.shape for w in weights]

    # save shapes metadata
    with open(SHAPES_JSON, "w") as f:
        json.dump(shapes, f)

    # flatten everything to float32 and write sequentially
    with open(WEIGHTS_BIN, "wb") as f:
        for w in weights:
            arr = np.asarray(w, dtype = np.float32).flatten()
            f.write(arr.tobytes())

    print("Saved:")
    print(f" - {WEIGHTS_BIN}")
    print(f" - {SHAPES_JSON}")

if __name__ == "__main__":
    main()