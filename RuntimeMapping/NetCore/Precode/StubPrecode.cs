using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace DotNetPatcher {
    // Regular precode
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct StubPrecodeFields {

#if HOST_64BIT
        public const byte Type = 0xF8;
        // mov r10,pMethodDesc
        // clc
        // jmp Stub
#else
        public const byte Type = 0xED;
        // mov eax,pMethodDesc
        // mov ebp,ebp
        // jmp Stub
#endif // HOST_64BIT

#if TARGET_64BIT
        public ushort m_movR10;
#else
            public byte m_movEAX;
#endif
        public IntPtr m_pMethodDesc; //TODO: really 8 bytes?? Maybe 5?

#if !TARGET_64BIT
            public byte m_mov_rm_r;
#endif
        public byte m_type;
        public byte m_jmp;
        public int m_rel32;
    }


    public class StubPrecode : NativeObject<StubPrecodeFields>
    {
        public StubPrecode(IntPtr ptr) : base(ptr)
        {
        }

        public IntPtr GetMethodDesc() {
            return Fields.m_pMethodDesc;
        }

        public IntPtr GetTarget() {
            var fieldOffset = Marshal.OffsetOf<StubPrecodeFields>("m_rel32");
            return Memory.rel32Decode(new IntPtr(NativePtr.ToInt64() + fieldOffset.ToInt64()));
        }

        /*void ResetTargetInterlocked() {
            rel32SetInterlocked(&m_rel32, GetPreStubEntryPoint(), (MethodDesc*)GetMethodDesc());
        }*/
        /*public bool SetTargetInterlocked(IntPtr target, IntPtr expected) {
            //EnsureWritableExecutablePages(&m_rel32);
            return rel32SetInterlocked(&m_rel32, target, expected, (MethodDesc*)GetMethodDesc());
        }*/
    }
}
