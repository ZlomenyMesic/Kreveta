#
# Kreveta chess engine by ZlomenyMesic
# started 4-3-2025
#

import os
import struct

NN_NAME = "nnue-256-16-16-v6.bin"

SCRIPT_DIR   = os.path.dirname(os.path.abspath(__file__))
WEIGHTS_PATH = os.path.join(SCRIPT_DIR, "weights\\nnue_weights_snapshot_260M.bin")
OUTPUT_PATH  = os.path.join(SCRIPT_DIR, f"archive\\{NN_NAME}")

SCALE = 1024

with open(WEIGHTS_PATH, "rb") as f:
    data = f.read()

floats = struct.iter_unpack("f", data)

with open(OUTPUT_PATH, "wb") as f:
    for (x,) in floats:
        # quantize with the scale
        q = round(x * SCALE)

        # clamp to int16 range
        q = max(-32768, min(32767, int(q)))

        f.write(struct.pack("h", q))

print("quantization complete :)")
