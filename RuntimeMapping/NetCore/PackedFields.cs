using System;
using System.Collections.Generic;
using System.Text;

namespace DotNetPatcher {

    interface IPackedFieldsData {
        uint[] GetFields();
        void Clear();
    }

    static class PackedFields {
        enum Constants : uint {
            kMaxLengthBits = 5,    // Number of bits needed to express the maximum length of a field (32-bits)
            kBitsPerDWORD = 32,   // Number of bits in a DWORD
        };

        public static uint GetUnpackedField<T>(ref T data, uint dwFieldIndex) where T : IPackedFieldsData {
            return data.GetFields()[dwFieldIndex];
        }

        public static void SetUnpackedField<T>(ref T data, uint dwFieldIndex, uint dwValue) where T : IPackedFieldsData {
            data.GetFields()[dwFieldIndex] = dwValue;
        }

        public static uint BitVectorGet<T>(ref T data, uint dwOffset, uint dwLength) where T : IPackedFieldsData {
            uint dwStartBlock = dwOffset / (uint)Constants.kBitsPerDWORD;
            uint dwEndBlock = (dwOffset + dwLength - 1) / (uint)Constants.kBitsPerDWORD;
            if (dwStartBlock == dwEndBlock) {
                uint dwValueShift = dwOffset % (uint)Constants.kBitsPerDWORD;
                uint dwValueMask = ((1U << (int)dwLength) - 1) << (int)dwValueShift;
                return (uint)(data.GetFields()[dwStartBlock] & dwValueMask) >> (int)dwValueShift;
            } else {
                uint dwInitialBits = (uint)Constants.kBitsPerDWORD - (dwOffset % (uint)Constants.kBitsPerDWORD);   // Number of bits to get in the first DWORD
                uint dwReturn;
                dwReturn = BitVectorGet(ref data, dwOffset, dwInitialBits);
                dwReturn |= BitVectorGet(ref data, dwOffset + dwInitialBits, dwLength - dwInitialBits) << (int)dwInitialBits;

                return dwReturn;
            }
        }

        public static void BitVectorSet<T>(ref T data, uint dwOffset, uint dwLength, uint dwValue) where T : IPackedFieldsData {
            uint dwStartBlock = dwOffset / (uint)Constants.kBitsPerDWORD;
            uint dwEndBlock = (dwOffset + dwLength - 1) / (uint)Constants.kBitsPerDWORD;
            if (dwStartBlock == dwEndBlock) {
                uint dwValueShift = dwOffset % (uint)Constants.kBitsPerDWORD;
                uint dwValueMask = ((1U << (int)dwLength) - 1) << (int)dwValueShift;

                data.GetFields()[dwStartBlock] &= ~dwValueMask;             // Zero the target bits
                data.GetFields()[dwStartBlock] |= dwValue << (int)dwValueShift;  // Or in the new value (suitably shifted)
            } else {
                uint dwInitialBits = (uint)Constants.kBitsPerDWORD - (dwOffset % (uint)Constants.kBitsPerDWORD);   // Number of bits to set in the first DWORD
                uint dwInitialMask = (1U << (int)dwInitialBits) - 1;                    // Mask covering those value bits

                // Set the portion of the value residing in the first DWORD.
                BitVectorSet(ref data, dwOffset, dwInitialBits, dwValue & dwInitialMask);

                // And then the remainder in the second DWORD.
                BitVectorSet(ref data, dwOffset + dwInitialBits, dwLength - dwInitialBits, dwValue >> (int)dwInitialBits);
            }
        }

        public static uint GetPackedField<T>(ref T data, uint dwFieldIndex) where T : IPackedFieldsData {
            uint dwOffset = 0;
            for (uint i = 0; i < dwFieldIndex; i++)
                dwOffset += (uint)Constants.kMaxLengthBits + BitVectorGet(ref data, dwOffset, (uint)Constants.kMaxLengthBits) + 1; // +1 since size is [1,32] not [0,31]

            uint dwFieldLength = BitVectorGet(ref data, dwOffset, (uint)Constants.kMaxLengthBits) + 1;
            dwOffset += (uint)Constants.kMaxLengthBits;
            uint dwReturn = BitVectorGet(ref data, dwOffset, dwFieldLength);
            return dwReturn;
        }
    }
}
