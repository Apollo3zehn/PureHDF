export function getFileObject(input) {
    if (!input.files || input.files.length === 0)
        return null;
    return input.files[0];
}

export function getBlobSize(blob) {
    return blob.size;
}

export async function readBlobSlice(blob, start, end) {
    const buf = await blob.slice(start, end).arrayBuffer();
    return new Uint8Array(buf);
}
