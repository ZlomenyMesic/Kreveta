#
# Kreveta chess engine by ZlomenyMesic
# started 4-3-2025
#

import pandas as pd
import matplotlib.pyplot as plt

LOGFILE = "log.csv"

df = pd.read_csv(LOGFILE)

plt.figure(figsize=(10,5))
plt.plot(df["samples"], df["loss"])
plt.xlabel("samples seen")
plt.ylabel("loss")
plt.title("Training Loss Curve")
plt.grid(True)
plt.savefig("loss_curve.png", dpi=200)

plt.figure(figsize=(10,5))
plt.plot(df["samples"], df["mae"])
plt.xlabel("samples seen")
plt.ylabel("MAE")
plt.title("Mean Absolute Error Curve")
plt.grid(True)
plt.savefig("mae_curve.png", dpi=200)