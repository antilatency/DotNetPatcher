using System;
using System.Collections.Generic;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.InteropServices;
using System.Text;

namespace DotNetPatcher {

    public enum PrecodeType {
        PRECODE_INVALID = InvalidPrecode.Type,
        PRECODE_STUB = StubPrecodeFields.Type,
#if HAS_NDIRECT_IMPORT_PRECODE
        PRECODE_NDIRECT_IMPORT = NDirectImportPrecodeFields.Type,
#endif
#if HAS_FIXUP_PRECODE
        PRECODE_FIXUP = FixupPrecodeFields.Type,
#endif
#if HAS_THISPTR_RETBUF_PRECODE
        PRECODE_THISPTR_RETBUF = ThisPtrRetBufPrecodeFields.Type,
#endif
    }



    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct GenericPrecodeFields {
        public byte b0;
        public byte b1;
        public byte b2;
        public byte b3;
        public byte b4;
        public byte b5;
        public byte b6;
        public byte b7;
        public byte b8;
        public byte b9;
        public byte b10;
        public byte b11;
        public byte b12;
        public byte b13;
        public byte b14;
        public byte b15;
    }

    public class Precode : NativeObject<GenericPrecodeFields> {
        public Precode(IntPtr ptr) : base(ptr) { }


#if HOST_64BIT
        public const int OFFSETOF_PRECODE_TYPE = 0;
        public const int OFFSETOF_PRECODE_TYPE_CALL_OR_JMP = 5;
        public const int OFFSETOF_PRECODE_TYPE_MOV_R10 = 10;
        public const int SIZEOF_PRECODE_BASE = 16;
#else
    public const int OFFSETOF_PRECODE_TYPE      =        5;
    public const int OFFSETOF_PRECODE_TYPE_CALL_OR_JMP = 5;
    public const int OFFSETOF_PRECODE_TYPE_MOV_RM_R  =   6;
    public const int SIZEOF_PRECODE_BASE            =    8;
#endif // HOST_64BIT


#if TARGET_AMD64
        public const ushort X86_INSTR_MOV_R10_IMM64 = 0xBA49;      // mov r10, imm64
#endif
        public const byte X86_INSTR_CALL_REL32 = 0xE8;   // call rel32
        public const byte X86_INSTR_JMP_REL32 = 0xE9;        // jmp rel32

        public static int GetEntryPointOffset() {
#if _TARGET_ARM_
            return THUMB_CODE;
#else
            return 0;
#endif
        }

        public IntPtr GetEntryPoint() {
  
            return NativePtr + GetEntryPointOffset();
        }

        public static bool IsValidType(PrecodeType t) {

            switch (t) {
                case PrecodeType.PRECODE_STUB:
#if HAS_NDIRECT_IMPORT_PRECODE
                case PrecodeType.PRECODE_NDIRECT_IMPORT:
#endif // HAS_NDIRECT_IMPORT_PRECODE
#if HAS_FIXUP_PRECODE
                case PrecodeType.PRECODE_FIXUP:
#endif // HAS_FIXUP_PRECODE
#if HAS_THISPTR_RETBUF_PRECODE
                case PrecodeType.PRECODE_THISPTR_RETBUF:
#endif // HAS_THISPTR_RETBUF_PRECODE
                    return true;
                default:
                    return false;
            }
        }

        public new PrecodeType GetType()
        {
            return GetPrecodeType(NativePtr);
        }

        public static PrecodeType GetPrecodeType(IntPtr data) {


#if HAS_OFFSETOF_PRECODE_TYPE
            byte[] m_data = Memory.ReadBytes(data, 0, SIZEOF_PRECODE_BASE);
            byte type = m_data[OFFSETOF_PRECODE_TYPE];
#if TARGET_X86
            if (type == X86_INSTR_MOV_RM_R)
                type = m_data[OFFSETOF_PRECODE_TYPE_MOV_RM_R];
#endif //  TARGET_X86

#if TARGET_AMD64
            if (type == (X86_INSTR_MOV_R10_IMM64 & 0xFF))
                type = m_data[OFFSETOF_PRECODE_TYPE_MOV_R10];
            else if ((type == (X86_INSTR_CALL_REL32 & 0xFF)) || (type == (X86_INSTR_JMP_REL32 & 0xFF)))
                type = m_data[OFFSETOF_PRECODE_TYPE_CALL_OR_JMP];
#endif // _AMD64

#if (HAS_FIXUP_PRECODE && TARGET_X86) || TARGET_AMD64
            if (type == FixupPrecodeFields.TypePrestub)
                type = FixupPrecodeFields.Type;
#endif

#if TARGET_ARM
            static_assert_no_msg(offsetof(StubPrecode, m_pTarget) == offsetof(NDirectImportPrecode, m_pMethodDesc));
            // If the precode does not have thumb bit on target, it must be NDirectImportPrecode.
            if (type == StubPrecode::Type && ((AsStubPrecode()->m_pTarget & THUMB_CODE) == 0))
                type = NDirectImportPrecode::Type;
#endif

            return (PrecodeType)type;

#else // OFFSETOF_PRECODE_TYPE
            return PRECODE_STUB;
#endif // OFFSETOF_PRECODE_TYPE
        }


        public static bool isJumpRel64(IntPtr pCode)
        {
            var pbCode = Memory.ReadBytes(pCode, 0, 12);

            return 0x48 == pbCode[0] &&
                   0xB8 == pbCode[1] &&
                   0xFF == pbCode[10] &&
                   0xE0 == pbCode[11];
        }

        public static IntPtr decodeJump64(IntPtr pBuffer) {
            // mov rax, xxx
            // jmp rax
            // _ASSERTE(isJumpRel64(pBuffer));

            return Marshal.ReadIntPtr(pBuffer + 2);
        }

        public bool IsPointingTo(IntPtr target, IntPtr addr) {

#if CROSSGEN_COMPILE
            // Crossgen does not create jump stubs on AMD64, so just return always false here to 
            // avoid non-deterministic behavior.
            return FALSE;
#else // CROSSGEN_COMPILE
            if (target == addr)
                return true;

#if _TARGET_AMD64_
            // Handle jump stubs
            if (isJumpRel64(target)) {
                target = decodeJump64(target);
                if (target == addr)
                    return true;
            }
#endif // _TARGET_AMD64_

            return false;
#endif // CROSSGEN_COMPILE
        }

        public StubPrecode AsStubPrecode() {
            return new StubPrecode(NativePtr);
        }


#if HAS_FIXUP_PRECODE
        public FixupPrecode AsFixupPrecode() {
            return new FixupPrecode(NativePtr);
        }
#endif
        public ThisPtrRetBufPrecode AsThisPtrRetBufPrecode() {
            return new ThisPtrRetBufPrecode(NativePtr);
        }

        // Note: This is immediate target of the precode. It does not follow jump stub if there is one.
        public IntPtr GetTarget() {
            var target = IntPtr.Zero;

            PrecodeType precodeType = GetType();
            switch (precodeType) {
                case PrecodeType.PRECODE_STUB:
                    target = AsStubPrecode().GetTarget();
                    break;
#if HAS_FIXUP_PRECODE
                case PrecodeType.PRECODE_FIXUP:
                    target = AsFixupPrecode().GetTarget();
                    break;
#endif
#if HAS_THISPTR_RETBUF_PRECODE
                case PrecodeType.PRECODE_THISPTR_RETBUF:
                    target = AsThisPtrRetBufPrecode().GetTarget();
                    break;
#endif 

                default:
                    //UnexpectedPrecodeType("Precode::GetTarget", precodeType);
                    break;
            }
            return target;
        }


        public bool IsPointingToNativeCode(IntPtr pNativeCode) {
            return IsPointingTo(GetTarget(), pNativeCode);
        }

        public static Precode GetPrecodeFromEntryPoint(IntPtr addr, bool fSpeculative = false) {

#if DACCESS_COMPILE
            // Always use speculative checks with DAC
            fSpeculative = TRUE;
#endif
            if (IsValidType(GetPrecodeType(addr))) {
                return new Precode(addr);
            }
            return null;
        }

        static int AlignOf(PrecodeType t)
        {
            int align = IntPtr.Size; // PRECODE_ALIGNMENT;

#if _TARGET_X86_ && HAS_FIXUP_PRECODE
        // Fixup precodes has to be aligned to allow atomic patching
        if (t == PRECODE_FIXUP)
            align = 8;
#endif // _TARGET_X86_ && HAS_FIXUP_PRECODE

#if _TARGET_ARM_ && HAS_COMPACT_ENTRYPOINTS
        // Precodes have to be aligned to allow fast compact entry points check
        _ASSERTE (align >= sizeof(void*));
#endif // _TARGET_ARM_ && HAS_COMPACT_ENTRYPOINTS

            return align;
        }

        static uint SizeOf(PrecodeType t) {

            switch (t) {
                case PrecodeType.PRECODE_STUB:
                    return (uint)Marshal.SizeOf<StubPrecode>();
#if HAS_NDIRECT_IMPORT_PRECODE
                case PrecodeType.PRECODE_NDIRECT_IMPORT:
                    return (uint)Marshal.SizeOf < NDirectImportPrecode>();
#endif // HAS_NDIRECT_IMPORT_PRECODE
#if HAS_FIXUP_PRECODE
                case PrecodeType.PRECODE_FIXUP:
                    return (uint)Marshal.SizeOf < FixupPrecode>();
#endif // HAS_FIXUP_PRECODE
#if HAS_THISPTR_RETBUF_PRECODE
                case PrecodeType.PRECODE_THISPTR_RETBUF:
                    return (uint)Marshal.SizeOf < ThisPtrRetBufPrecode>();
#endif // HAS_THISPTR_RETBUF_PRECODE

                default:
                    throw new Exception("Precode::SizeOf");
                    break;
            }
            return 0;
        }

        public static uint SizeOfTemporaryEntryPoint(PrecodeType t) {

#if HAS_FIXUP_PRECODE_CHUNKS
            //_ASSERTE(t != PRECODE_FIXUP);
#endif
            return Memory.ALIGN_UP(SizeOf(t), (uint)AlignOf(t));
        }

        public static Precode GetPrecodeForTemporaryEntryPoint(IntPtr temporaryEntryPoints, int index) {
            PrecodeType t = (new Precode(temporaryEntryPoints)).GetType();
#if HAS_FIXUP_PRECODE_CHUNKS
            if (t == PrecodeType.PRECODE_FIXUP) {
                return new Precode(temporaryEntryPoints + index * Marshal.SizeOf<FixupPrecode>());
            }
#endif
            var oneSize = SizeOfTemporaryEntryPoint(t);
            return new Precode(temporaryEntryPoints + (int)(index * oneSize));
        }

        public NDirectImportPrecode AsNDirectImportPrecode()
        {
            return new NDirectImportPrecode(NativePtr);
        }

        public MethodDesc GetMethodDesc(bool fSpeculative = false /*= FALSE*/) {
            IntPtr pMD = IntPtr.Zero;

            PrecodeType precodeType = GetType();
            switch (precodeType) {
                case PrecodeType.PRECODE_STUB:
                    pMD = AsStubPrecode().GetMethodDesc();
                    break;
#if HAS_NDIRECT_IMPORT_PRECODE
                case PrecodeType.PRECODE_NDIRECT_IMPORT:
                    pMD = AsNDirectImportPrecode().GetMethodDesc();
                    break;
#endif // HAS_NDIRECT_IMPORT_PRECODE
#if HAS_FIXUP_PRECODE
                case PrecodeType.PRECODE_FIXUP:
                    pMD = AsFixupPrecode().GetMethodDesc();
                    break;
#endif // HAS_FIXUP_PRECODE
#if HAS_THISPTR_RETBUF_PRECODE
                case PrecodeType.PRECODE_THISPTR_RETBUF:
                    pMD = AsThisPtrRetBufPrecode().GetMethodDesc();
                    break;
#endif // HAS_THISPTR_RETBUF_PRECODE

                default:
                    break;
            }

            if (pMD == IntPtr.Zero) {
                if (fSpeculative)
                    return null;
                else
                {
                    throw new Exception("Precode::GetMethodDesc");
                }
            }

            // GetMethodDesc() on platform specific precode types returns TADDR. It should return 
            // PTR_MethodDesc instead. It is a workaround to resolve cyclic dependency between headers. 
            // Once we headers factoring of headers cleaned up, we should be able to get rid of it.

            // For speculative calls, pMD can be garbage that causes IBC logging to crash
          

            return new MethodDesc(pMD);
        }


    }
}
