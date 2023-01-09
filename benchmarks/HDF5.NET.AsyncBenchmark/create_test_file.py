from pathlib import Path
import sys
import os
import h5py

file_path = sys.argv[1]
folder_path = os.path.dirname(file_path)
Path(folder_path).mkdir(parents=True, exist_ok=True)

with h5py.File(file_path, "w") as h5_file:
    chunk_size = 1 * 1024 * 256 # 4 bytes per value; equal to the SimpleChunkCache default size
    chunk_count = 1000

    dataset = h5_file.create_dataset(
        name="chunked",
        shape=(chunk_count * chunk_size,),
        chunks=(chunk_size,))

    data = range(0, chunk_size * 250)

    dataset[chunk_size * 0:chunk_size * 250] = data
    dataset[chunk_size * 250:chunk_size * 500] = data
    dataset[chunk_size * 500:chunk_size * 750] = data
    dataset[chunk_size * 750:chunk_size * 1000] = data