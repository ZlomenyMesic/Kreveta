#
# Kreveta chess engine by ZlomenyMesic
# started 4-3-2025
#

import os
import struct

NN_NAME = "nnue_weights.bin"

SCRIPT_DIR   = os.path.dirname(os.path.abspath(__file__))
WEIGHTS_PATH = os.path.join(SCRIPT_DIR, "archive\\nnue-128-16-16-v4.bin")
OUTPUT_PATH  = os.path.join(SCRIPT_DIR, f"weights\\{NN_NAME}")

SCALE = 1024

with open(WEIGHTS_PATH, "rb") as f:
    data = f.read()

floats = struct.iter_unpack("h", data)

with open(OUTPUT_PATH, "wb") as f:
    for (x,) in floats:
        # quantize with the scale
        q = float(x) / SCALE

        f.write(struct.pack("f", q))

print("dequantization complete :)")
