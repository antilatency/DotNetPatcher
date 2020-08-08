using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace DotNetPatcher {
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ThisPtrRetBufPrecodeFields {

#if HOST_64BIT
        public const int Type = 0x90;
#else
            public const int Type = 0xC2;
#endif

        // mov regScratch,regArg0
        // mov regArg0,regArg1
        // mov regArg1,regScratch
        // nop
        // jmp EntryPoint
        // dw pMethodDesc

#if TARGET_64BIT
        public byte m_nop1;
#endif
#if TARGET_64BIT
        public byte m_prefix1;
#endif
        public ushort m_movScratchArg0;

#if TARGET_64BIT
        public byte m_prefix2;
#endif
        public ushort m_movArg0Arg1;

#if TARGET_64BIT
        public byte m_prefix3;
#endif

        public ushort m_movArg1Scratch;
        public byte m_nop2;
        public byte m_jmp;
        public int m_rel32;
        public IntPtr m_pMethodDesc;
    }


    public class ThisPtrRetBufPrecode : NativeObject<ThisPtrRetBufPrecodeFields>
    {
        public const int REL32_JMP_SELF = -5;

        public ThisPtrRetBufPrecode(IntPtr ptr) : base(ptr)
        {
        }

        public IntPtr GetMethodDesc() {
            return Fields.m_pMethodDesc;
        }

       
        public IntPtr GetTarget()
        {
            // This precode is never patched lazily - pretend that the uninitialized m_rel32 points to prestub
            if (Fields.m_rel32 == REL32_JMP_SELF)
            {
                throw new NotImplementedException();//return GetPreStubEntryPoint();
            }
            return Memory.rel32Decode(NativePtr + Marshal.OffsetOf<ThisPtrRetBufPrecodeFields>("m_rel32").ToInt32() );
        }

        //BOOL SetTargetInterlocked(TADDR target, TADDR expected);
    }

}
