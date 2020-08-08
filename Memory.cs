using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace DotNetPatcher {
    public static class Memory {
        public static byte[] ReadBytes(IntPtr ptr, int offset, int length) {
            byte[] buffer = new byte[length];
            var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            Marshal.Copy(ptr, buffer, offset, length);
            handle.Free();
            return buffer;
        }

        public static T ReadStruct<T>(IntPtr ptr) where T : struct {
            return Marshal.PtrToStructure<T>(ptr);
        }

        public static void WriteStruct<T>(IntPtr ptr, T value) where T : struct {
            Marshal.StructureToPtr(value, ptr, false);
        }


        public static IntPtr rel32Decode(IntPtr pRel32) {
            return pRel32 + 4 + Marshal.ReadInt32(pRel32);
        }

        public static void rel32Encode(IntPtr pRel32, IntPtr targetAbsoluteAddress) {
            int rel = (int)(targetAbsoluteAddress.ToInt64() - 4 - pRel32.ToInt64());
            Marshal.WriteInt32(pRel32, rel);
        }


        public static void dumpMemory(IntPtr ptr) {
            byte[] buffer = new byte[48];
            var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            Marshal.Copy(ptr, buffer, 0, buffer.Length);
            handle.Free();

            for (int i = 0; i < buffer.Length; ++i) {
                Console.Write($"{(buffer[i]):X2} ");
            }
            Console.WriteLine();
        }

        public static uint ALIGN_UP(uint val, uint alignment) {

            // alignment must be a power of 2 for this implementation to work (need modulo otherwise)
            // _ASSERTE(0 == (alignment & (alignment - 1)));
            uint result = (val + (alignment - 1)) & ~(alignment - 1);
            //_ASSERTE(result >= val);      // check for overflow
            return result;
        }

    }


    public struct RelativePointer
    {
        public IntPtr RelativeValue;

        public IntPtr GetValueMaybeNull(IntPtr _this)
        {
            if (RelativeValue == IntPtr.Zero)
            {
                return IntPtr.Zero;
            }
            return new IntPtr(_this.ToInt64() + RelativeValue.ToInt64());
        }
    }
}
