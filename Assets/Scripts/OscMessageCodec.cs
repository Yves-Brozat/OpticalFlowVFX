using System;
using System.Runtime.InteropServices;
using System.Text;

namespace OpticalFlowTest
{
    // One OSC 1.0 message per UDP datagram, as sent by the Quest.
    public static class OscMessageCodec
    {
        [StructLayout(LayoutKind.Explicit)]
        struct FloatBits
        {
            [FieldOffset(0)] public float Value;
            [FieldOffset(0)] public uint Bits;
        }

        static bool ReadString(byte[] data, ref int offset, out string value)
        {
            value = null;
            int start = offset;
            while (offset < data.Length && data[offset] != 0)
            {
                if (data[offset] > 127) return false;
                offset++;
            }
            if (offset == data.Length) return false;
            value = Encoding.ASCII.GetString(data, start, offset - start);
            int padded = (offset + 4) & ~3;
            if (padded > data.Length) return false;
            while (offset < padded)
                if (data[offset++] != 0) return false;
            return true;
        }

        public static bool TryDecode(byte[] data, out string address, out float[] values)
        {
            address = null;
            values = null;
            if (data == null || data.Length == 0 || data.Length % 4 != 0) return false;
            int offset = 0;
            if (!ReadString(data, ref offset, out address) || !address.StartsWith("/")) return false;
            if (!ReadString(data, ref offset, out string tags) || !tags.StartsWith(",")) return false;
            var result = new float[tags.Length - 1];
            for (int i = 1; i < tags.Length; i++)
            {
                char tag = tags[i];
                if (tag == 'T' || tag == 'F') { result[i - 1] = tag == 'T' ? 1 : 0; continue; }
                if ((tag != 'f' && tag != 'i') || offset > data.Length - 4) return false;
                uint bits = (uint)data[offset] << 24 | (uint)data[offset + 1] << 16 |
                            (uint)data[offset + 2] << 8 | data[offset + 3];
                offset += 4;
                float value = tag == 'f' ? new FloatBits { Bits = bits }.Value : unchecked((int)bits);
                if (float.IsNaN(value) || float.IsInfinity(value)) return false;
                result[i - 1] = value;
            }
            if (offset != data.Length) return false;
            values = result;
            return true;
        }

        public static byte[] Encode(string address, float[] values)
        {
            if (string.IsNullOrEmpty(address) || address[0] != '/')
                throw new ArgumentException("OSC address must start with /.", nameof(address));
            foreach (char c in address)
                if (c == 0 || c > 127) throw new ArgumentException("OSC address must be ASCII without nulls.", nameof(address));
            if (values == null) throw new ArgumentNullException(nameof(values));
            long size = ((address.Length + 4L) & ~3L) + ((values.Length + 5L) & ~3L) + values.Length * 4L;
            if (size > 65507) throw new ArgumentException("OSC message exceeds UDP payload limit.");
            var data = new byte[(int)size];
            Encoding.ASCII.GetBytes(address, 0, address.Length, data, 0);
            int offset = (address.Length + 4) & ~3;
            data[offset] = (byte)',';
            for (int i = 0; i < values.Length; i++) data[offset + 1 + i] = (byte)'f';
            offset += (values.Length + 5) & ~3;
            foreach (float value in values)
            {
                if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentException("OSC values must be finite.");
                uint bits = new FloatBits { Value = value }.Bits;
                data[offset++] = (byte)(bits >> 24);
                data[offset++] = (byte)(bits >> 16);
                data[offset++] = (byte)(bits >> 8);
                data[offset++] = (byte)bits;
            }
            return data;
        }
    }
}
