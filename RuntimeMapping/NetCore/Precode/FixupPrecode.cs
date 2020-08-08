using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using DotNetPatcher;

namespace DotNetPatcher {

    public class FixupPrecode : NativeObject<FixupPrecodeFields> {
        public FixupPrecode(IntPtr ptr) : base(ptr)
        {
        }


#if HAS_FIXUP_PRECODE_CHUNKS
        public IntPtr GetBase() {
            return new IntPtr(NativePtr.ToInt64() + (Fields.m_PrecodeChunkIndex + 1) * Marshal.SizeOf<FixupPrecode>());
        }

        public IntPtr GetMethodDesc() {
            // This lookup is also manually inlined in PrecodeFixupThunk assembly code
            IntPtr basePtr = Marshal.ReadIntPtr(GetBase());
            if (basePtr == IntPtr.Zero)
                return IntPtr.Zero;
            return basePtr + (Fields.m_MethodDescChunkIndex * 4); //MethodDesc::ALIGNMENT);
        }
#else
        public IntPtr GetMethodDesc() {
            return m_pMethodDesc;
        }
#endif
        public void SetTargetAbsoluteUnsafe(IntPtr absoluteTargetAddress)
        {
            var fieldOffset = Marshal.OffsetOf<FixupPrecodeFields>("m_rel32");
            var pRel32 = new IntPtr(NativePtr.ToInt64() + fieldOffset.ToInt64());
            Memory.rel32Encode(pRel32, absoluteTargetAddress);
        }

        public IntPtr GetTarget() {
            var fieldOffset = Marshal.OffsetOf<FixupPrecodeFields>("m_rel32");
            return Memory.rel32Decode(new IntPtr(NativePtr.ToInt64() + fieldOffset.ToInt64()));
        }


        /*void ResetTargetInterlocked();
        BOOL SetTargetInterlocked(TADDR target, TADDR expected);

        static BOOL IsFixupPrecodeByASM(TADDR addr) {
            LIMITED_METHOD_CONTRACT;

            return *dac_cast<PTR_BYTE>(addr) == X86_INSTR_JMP_REL32;
        }*/


    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct FixupPrecodeFields {

        public const int TypePrestub = 0x5E;
        // The entrypoint has to be 8-byte aligned so that the "call PrecodeFixupThunk" can be patched to "jmp NativeCode" atomically.
        // call PrecodeFixupThunk
        // db TypePrestub (pop esi)
        // db MethodDescChunkIndex
        // db PrecodeChunkIndex

        public const int Type = 0x5F;
        // After it has been patched to point to native code
        // jmp NativeCode
        // db Type (pop edi)

        public byte m_op;
        public int m_rel32;
        public byte m_type;
        public byte m_MethodDescChunkIndex;
        public byte m_PrecodeChunkIndex;
#if HAS_FIXUP_PRECODE_CHUNKS
        // Fixup precode chunk is associated with MethodDescChunk. The layout of the fixup precode chunk is:
        //
        // FixupPrecode     Entrypoint PrecodeChunkIndex = 2
        // FixupPrecode     Entrypoint PrecodeChunkIndex = 1
        // FixupPrecode     Entrypoint PrecodeChunkIndex = 0
        // TADDR            Base of MethodDescChunk
#else
            public IntPtr m_pMethodDesc;
#endif
    }
}
