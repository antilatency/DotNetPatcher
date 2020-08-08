using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace DotNetPatcher {
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct NDirectImportPrecodeFields
    {

#if HOST_64BIT
        public const byte Type = 0xF9;
        // mov r10,pMethodDesc
        // clc
        // jmp Stub
#else
            public const byte Type = 0xC0;
            // mov eax,pMethodDesc
            // mov ebp,ebp
            // jmp Stub
#endif // HOST_64BIT

#if TARGET_64BIT
        public ushort m_movR10;
#else
        public byte m_movEAX;
#endif
        public IntPtr m_pMethodDesc;

#if !TARGET_64BIT
        public byte m_mov_rm_r;
#endif
        public byte m_type;
        public byte m_jmp;
        public int m_rel32;
    }

    public class NDirectImportPrecode : NativeObject<NDirectImportPrecodeFields>
    {
        public NDirectImportPrecode(IntPtr ptr) : base(ptr)
        {
        }

        public IntPtr GetMethodDesc() {
            return Fields.m_pMethodDesc;
        }

        public IntPtr GetTarget() {
            var fieldOffset = Marshal.OffsetOf<NDirectImportPrecodeFields>("m_rel32");
            return Memory.rel32Decode(new IntPtr(NativePtr.ToInt64() + fieldOffset.ToInt64()));
        }

        /*void ResetTargetInterlocked() {
            rel32SetInterlocked(&m_rel32, GetPreStubEntryPoint(), (MethodDesc*)GetMethodDesc());
        }*/

        /*BOOL SetTargetInterlocked(TADDR target, TADDR expected) {
            return rel32SetInterlocked(&m_rel32, target, expected, (MethodDesc*)GetMethodDesc());
        }*/
    }
}
