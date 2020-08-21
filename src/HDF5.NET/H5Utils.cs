using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HDF5.NET
{
    public static class H5Utils
    {
        public static void ValidateSignature(byte[] actual, byte[] expected)
        {
            var actualString = Encoding.ASCII.GetString(actual);
            var expectedString = Encoding.ASCII.GetString(expected);

            if (actualString != expectedString)
                throw new Exception($"The actual signature '{actualString}' does not match the expected signature '{expectedString}'.");
        }

        public static bool IsPowerOfTwo(ulong x)
        {
            return (x != 0) && ((x & (x - 1)) == 0);
        }

        public static ulong FindMinByteCount(ulong value)
        {
            ulong lg_v = 1;

            while ((value >>= 1) != 0)
            {
                lg_v++;
            }

            ulong result = lg_v >> 3;

            if (lg_v != result << 3)
                result += 1;

            return result;
        }

        public static string ReadFixedLengthString(BinaryReader reader, int length, CharacterSetEncoding characterSet = CharacterSetEncoding.ASCII)
        {
            var data = reader.ReadBytes(length);

            return characterSet switch
            {
                CharacterSetEncoding.ASCII  => Encoding.ASCII.GetString(data),
                CharacterSetEncoding.UTF8   => Encoding.UTF8.GetString(data),
                _ => throw new FormatException($"The character set encoding '{characterSet}' is not supported.")
            };
        }

        public static string ReadNullTerminatedString(BinaryReader reader, bool pad, CharacterSetEncoding characterSet = CharacterSetEncoding.ASCII)
        {
            var data = new List<byte>();
            var byteValue = reader.ReadByte();

            while (byteValue != '\0')
            {
                data.Add(byteValue);
                byteValue = reader.ReadByte();
            }

            var result = characterSet switch
            {
                CharacterSetEncoding.ASCII  => Encoding.ASCII.GetString(data.ToArray()),
                CharacterSetEncoding.UTF8   => Encoding.UTF8.GetString(data.ToArray()),
                _ => throw new FormatException($"The character set encoding '{characterSet}' is not supported.")
            };

            if (pad)
            {
                var paddingCount = 8 - (result.Length + 1) % 8;
                reader.BaseStream.Seek(paddingCount, SeekOrigin.Current);
            }

            return result;
        }
    }
}
