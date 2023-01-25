from pathlib import Path
import sys
import os
import h5py

file_path = sys.argv[1]
folder_path = os.path.dirname(file_path)
Path(folder_path).mkdir(parents=True, exist_ok=True)

with h5py.File(file_path, "w") as h5_file:
    chunk_size = int(1 * 1024 * 1024 * 100 / 4) # 4 bytes per value
    chunk_count = 10

    dataset = h5_file.create_dataset(
        name="chunked",
        shape=(chunk_count * chunk_size,),
        chunks=(chunk_size,))

    data = range(0, chunk_size)

    for i in range(0, 10):
        start = chunk_size * i
        end = chunk_size * (i + 1)
        dataset[start:end] = data