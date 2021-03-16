using System;
using System.IO;

namespace HDF5.NET
{
    public partial class H5File : H5Group, IDisposable
    {
        #region Properties

        public string Path { get; } = ":memory:";

        public Func<IChunkCache> ChunkCacheFactory
        {
            get
            {
                if (_chunkCacheFactory is not null)
                    return _chunkCacheFactory;

                else
                    return H5File.DefaultChunkCacheFactory;
            }
            set
            {
                _chunkCacheFactory = value;
            }
        }

        public static Func<IChunkCache> DefaultChunkCacheFactory = () => new SimpleChunkCache();

        #endregion

        #region Methods

        public static H5File Open(string filePath, FileMode mode, FileAccess fileAccess, FileShare fileShare)
        {
            return H5File.Open(filePath, mode, fileAccess, fileShare, deleteOnClose: false);
        }

#warning Stream + filepath does not make sense. Improve constructors generally.
        public static H5File Open(Stream stream, string absoluteFilePath, bool deleteOnClose)
        {
            var reader = new H5BinaryReader(stream);

            // superblock
            int stepSize = 512;
            var signature = reader.ReadBytes(8);

            while (!H5File.ValidateSignature(signature, Superblock.FormatSignature))
            {
                reader.Seek(stepSize - 8, SeekOrigin.Current);

                if (reader.BaseStream.Position >= reader.BaseStream.Length)
                    throw new Exception("The file is not a valid HDF 5 file.");

                signature = reader.ReadBytes(8);
                stepSize *= 2;
            }

            var version = reader.ReadByte();

            var superblock = (Superblock)(version switch
            {
                0 => new Superblock01(reader, version),
                1 => new Superblock01(reader, version),
                2 => new Superblock23(reader, version),
                3 => new Superblock23(reader, version),
                _ => throw new NotSupportedException($"The superblock version '{version}' is not supported.")
            });

            reader.BaseAddress = superblock.BaseAddress;

            ulong address;
            var superblock01 = superblock as Superblock01;

            if (superblock01 is not null)
            {
                address = superblock01.RootGroupSymbolTableEntry.HeaderAddress;
            }
            else
            {
                var superblock23 = superblock as Superblock23;

                if (superblock23 is not null)
                    address = superblock23.RootGroupObjectHeaderAddress;
                else
                    throw new Exception($"The superblock of type '{superblock.GetType().Name}' is not supported.");
            }


            reader.Seek((long)address, SeekOrigin.Begin);
            var context = new H5Context(reader, superblock);
            var header = ObjectHeader.Construct(context);

            var file = new H5File(context, default, header, absoluteFilePath, deleteOnClose);
            var reference = new H5NamedReference("/", address, file);
            file.Reference = reference;

            return file;
        }

        public void Dispose()
        {
            H5Cache.Clear(this.Context.Superblock);
            this.Context.Reader.Dispose();

            if (_deleteOnClose && System.IO.File.Exists(this.Path))
            {
                try
                {
                    System.IO.File.Delete(this.Path);
                }
                catch
                {
                    //
                }
            }    
        }

        #endregion
    }
}
