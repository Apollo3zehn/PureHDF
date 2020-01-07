using System;
using System.IO;

namespace HDF5.NET
{
    public class DatatypeMessage : Message
    {
        #region Constructors

        public DatatypeMessage(BinaryReader reader) : base(reader)
        {
            this.ClassVersion = reader.ReadByte();

            this.BitFieldDescription = this.Class switch
            {
                DatatypeMessageClass.FixedPoint     => new FixedPointBitFieldDescription(reader),
                DatatypeMessageClass.FloatingPoint  => new FloatingPointBitFieldDescription(reader),
                DatatypeMessageClass.Time           => new TimeBitFieldDescription(reader),
                DatatypeMessageClass.String         => new StringBitFieldDescription(reader),
                DatatypeMessageClass.BitField       => new BitFieldBitFieldDescription(reader),
                DatatypeMessageClass.Opaque         => new OpaqueBitFieldDescription(reader),
                DatatypeMessageClass.Compount       => new CompoundBitFieldDescription(reader),
                DatatypeMessageClass.Reference      => new ReferenceBitFieldDescription(reader),
                DatatypeMessageClass.Enumerated     => new EnumerationBitFieldDescription(reader),
                DatatypeMessageClass.VariableLength => new VariableLengthBitFieldDescription(reader),
                DatatypeMessageClass.Array          => null,
                _                                   => throw new NotSupportedException($"The data type message class '{this.Class}' is not supported.")
            };

            this.Size = reader.ReadUInt32();

            this.Properties = this.Class switch
            {
                DatatypeMessageClass.FixedPoint     => new FixedPointPropertyDescription(reader),
                DatatypeMessageClass.FloatingPoint  => new FloatingPointPropertyDescription(reader),
                DatatypeMessageClass.Time           => new TimePropertyDescription(reader),
                DatatypeMessageClass.String         => null,
                DatatypeMessageClass.BitField       => new BitFieldPropertyDescription(reader),
                DatatypeMessageClass.Opaque         => new OpaquePropertyDescription(reader),
                DatatypeMessageClass.Compount       => new CompoundPropertyDescription(reader),
                DatatypeMessageClass.Reference      => null,
                DatatypeMessageClass.Enumerated     => new EnumerationPropertyDescription(reader),
                DatatypeMessageClass.VariableLength => new VariableLengthPropertyDescription(reader),
                DatatypeMessageClass.Array          => new ArrayPropertyDescription(reader),
                _                                   => throw new NotSupportedException($"The data type message class '{this.Class}' is not supported.")
            };
        }

        #endregion

        #region Properties

        public DatatypeBitFieldDescription? BitFieldDescription { get; set; }
        public ulong Size { get; set; }
        public DatatypePropertyDescription? Properties { get; set; }

        public byte Version
        {
            get
            {
                return (byte)(this.ClassVersion & 0x0F);
            }
            set
            {
                if (value > 0x0F)
                    throw new Exception("The version number must be <= 15.");

                this.ClassVersion = (byte)(this.ClassVersion & 0xF0);
                this.ClassVersion |= value;
            }
        }

        public DatatypeMessageClass Class
        {
            get
            {
                return (DatatypeMessageClass)((this.ClassVersion & 0xF0) >> 4);
            }
            set
            {
                this.ClassVersion = (byte)(this.ClassVersion & 0x0F);
                this.ClassVersion |= (byte)((byte)value << 4);
            }
        }

        private byte ClassVersion { get; set; }

        #endregion
    }
}
