using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace DotNetPatcher {

    [StructLayout(LayoutKind.Sequential)]
    public struct MethodDescChunkFields {
        public IntPtr m_methodTable;
        public IntPtr m_next;
        public byte m_size;// The size of this chunk minus 1 (in multiples of MethodDesc::ALIGNMENT)
        public byte m_count;// The number of MethodDescs in this chunk minus 1
        public ushort m_flagsAndTokenRange;
    }

    public class MethodDescChunk : NativeObject<MethodDescChunkFields> {

        public MethodDescChunk(IntPtr ptr) : base(ptr) { }

        public MethodTable GetMethodTable()
        {
            return new MethodTable(Fields.m_methodTable);
        }

        public MethodDescChunk GetNextChunk() {
            if (Fields.m_next == IntPtr.Zero) {
                return null;
            }
            return new MethodDescChunk(Fields.m_next);
        }

        public bool HasCompactEntryPoints() {

#if HAS_COMPACT_ENTRYPOINTS
            return (m_flagsAndTokenRange & enum_flag_HasCompactEntrypoints) != 0;
#else
            return false;
#endif
        }

        public int GetCount() {
            return Fields.m_count + 1;
        }

        public uint SizeOf() {
            return (uint)(Marshal.SizeOf<MethodDescChunkFields>() + (Fields.m_size + 1) * MethodDesc.ALIGNMENT); //;
        }

        public IntPtr GetTemporaryEntryPoints() {

            //_ASSERTE(HasTemporaryEntryPoints());
            return Marshal.ReadIntPtr(NativePtr - 1 * IntPtr.Size);
            // return *(dac_cast < DPTR(TADDR) > (this) - 1);
        }

        public IntPtr GetTemporaryEntryPoint(int index) {

            //_ASSERTE(HasTemporaryEntryPoints());

#if HAS_COMPACT_ENTRYPOINTS
            if (HasCompactEntryPoints()) {
#if _TARGET_ARM_

                return GetTemporaryEntryPoints() + COMPACT_ENTRY_ARM_CODE + THUMB_CODE + index * TEP_ENTRY_SIZE;

#else // _TARGET_ARM_

                int fullBlocks = index / TEP_MAX_BLOCK_INDEX;
                int remainder = index % TEP_MAX_BLOCK_INDEX;

                return GetTemporaryEntryPoints() + 1 + (fullBlocks * TEP_FULL_BLOCK_SIZE) +
                       (remainder * TEP_ENTRY_SIZE) + ((remainder >= TEP_MAX_BEFORE_INDEX) ? TEP_CENTRAL_JUMP_SIZE : 0);

#endif // _TARGET_ARM_
            }
#endif // HAS_COMPACT_ENTRYPOINTS

            return Precode.GetPrecodeForTemporaryEntryPoint(GetTemporaryEntryPoints(), index).GetEntryPoint();
        }

        public MethodDesc GetMethodDesc(int id)
        {
            var ss = SizeOf();
            var s = Marshal.SizeOf<MethodDescChunkFields>();
            var ptr = NativePtr + s;

            var al = MethodDesc.ALIGNMENT;

            var offset = Marshal.SizeOf<MethodDescFields>() * id;
            ptr = new IntPtr(ptr.ToInt64() + offset);
            return new MethodDesc(ptr);
        }
    }
}
