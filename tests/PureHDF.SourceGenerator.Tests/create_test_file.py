import h5py

file_path = "testfiles/source_generator.h5"

with h5py.File(file_path, "w") as h5_file:

    data = range(0, 10)

    dataset1 = h5_file.create_dataset(name="dataset1", data=data)

    group1 = h5_file.create_group(name="group1")
    group1_dataset1 = group1.create_dataset(name="1invalid_start_character", data=data)
    group1_dataset2 = group1.create_dataset(name="dataset_!", data=data)
    group1_dataset3 = group1.create_dataset(name="dataset_?", data=data)

    group1_group1 = group1.create_group(name="sub_group1")
    group1_group1_dataset1 = group1_group1.create_dataset(name="sub_sub_dataset1", data=data)
    
    group2 = h5_file.create_group(name="group2")
    group2_dataset1 = group2.create_dataset(name="sub_dataset1", data=data)