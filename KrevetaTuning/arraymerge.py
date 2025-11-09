#
# Kreveta chess engine by ZlomenyMesic
# started 4-3-2025
#

import re

shifts_path   = "C:\\Users\\michn\\Desktop\\Kreveta\\Kreveta\\KrevetaTuning\\bin\\Release\\net9.0\\output.txt"
init_arr_path = "C:\\Users\\michn\\Desktop\\Kreveta\\Kreveta\\KrevetaTuning\\initarr.txt"
output_path   = "C:\\Users\\michn\\Desktop\\Kreveta\\Kreveta\\KrevetaTuning\\output.txt"

shift_scale   = 1.2


with open(init_arr_path, "r") as file:
    content = file.read()

# remove C-style comments (// ...)
content = re.sub(r"//.*", "", content)

# remove brackets
content = content.replace("[", "").replace("]", "")

# find all integers (including negatives)
numbers = re.findall(r"-?\d+", content)

# convert to int
values = [int(n) for n in numbers]

with open(shifts_path, 'r') as file:
    lines = [line.rstrip() for line in file]

cur = 0
for line in lines:
    shift = int(round(shift_scale * float(line.split(' ')[3])))

    values[cur] += shift
    cur += 1

print(values)
input()