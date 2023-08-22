# import h5py

# with h5py.File("reference_attribute.h5", "w") as file:

#     file.attrs["attribute1"] = "value 1"
#     attribute1 = file.attrs["attribute1"]

#     file.attrs["attribute2"] = 42

#     group = file.create_group("reference")
#     group.attrs["attribute3"] = 3.14

#     dataset = group.create_dataset("attribute", shape=(3,), dtype=h5py.ref_dtype)

#     references = []
#     references.append(attribute1.ref)
#     references.append(h5py.Reference(file, "attribute2"))
#     references.append(h5py.Reference(group, "attribute3"))

#     dataset[...] = references