#!/bin/bash

# https://github.com/HDFGroup/hsds/tree/ae1e926502dcbd023b9b20ba981cad54086a64e7#quick-start
# https://github.com/HDFGroup/hsds/blob/ae1e926502dcbd023b9b20ba981cad54086a64e7/docs/post_install.md#test-data-setup

#
echo "stop on error"
set -e

#
echo "prepare python environment"
python -m venv .venv

source .venv/bin/activate

pip install hsds h5py h5pyd

#
echo "prepare HSDS environment"
mkdir -p artifacts/hsds/test/hsdstest

echo "admin:admin" > artifacts/hsds/passwd.txt

#
echo "start HSDS in the background"
hsds \
    --root_dir artifacts/hsds/test \
    --password_file artifacts/hsds/passwd.txt \
    --logfile artifacts/hsds/hs.log \
    --host localhost \
    --port 5101 &

#
echo "wait up to 30 seconds for HSDS to come up"
timeout 30 bash -c 'while [[ "$(curl -s -o /dev/null -w ''%{http_code}'' http://localhost:5101/domains)" != "200" ]]; do sleep 5; done' || false

#
echo "create /shared/ domain"
hstouch -u admin -p admin -e http://localhost:5101 /shared/

#
echo "load tall.h5"
wget https://s3.amazonaws.com/hdfgroup/data/hdf5test/tall.h5 -P artifacts/hsds
hsload  -u admin -p admin -e http://localhost:5101 artifacts/hsds/tall.h5 /shared/

#
echo "run test"

if [[ "${CI}" == "true" ]]; then
    dotnet test --filter "FullyQualifiedName~PureHDF.Tests.Reading.VOL.HsdsTests" -c Release /p:BuildProjectReferences=false 
else
    dotnet test --filter "FullyQualifiedName~PureHDF.Tests.Reading.VOL.HsdsTests"
fi

#
echo "stop HSDS"
pkill python
rm -r artifacts/hsds