using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace DotNetPatcher {
    [StructLayout(LayoutKind.Sequential)]
    public struct MethodDescFields {
        public ushort m_wFlags3AndTokenRemainder;
        public byte m_chunkIndex;
        public byte m_bFlags2;
        public ushort m_wSlotNumber;
        public MethodDescClassification m_wFlags;
    }

    public interface IMethodDescFieldsProvider
    {
        MethodDescFields GetBaseFields();
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MethodDescFieldsWrapper : IMethodDescFieldsProvider {
        public MethodDescFields BaseFields;

        public MethodDescFields GetBaseFields()
        {
            return BaseFields;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MethodImpl
    {
        public IntPtr pdwSlots;       // Maintains the slots and tokens in sorted order, the first entry is the size
        public IntPtr pImplementedMD;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ComPlusCallInfo {
        // IL stub for CLR to COM call
        public IntPtr m_pILStub; //union with  //MethodDesc* m_pEventProviderMD; MethodDesc of the COM event provider to forward the call to (COM event interfaces)

        // method table of the interface which this represents
        public IntPtr m_pInterfaceMT;

        // We need only 3 bits here, see enum Flags below.
        public byte m_flags;

        // ComSlot() (is cached when we first invoke the method and generate
        // the stubs for it. There's probably a better place to do this
        // caching but I'm not sure I know all the places these things are
        // created.)
        public ushort m_cachedComSlot;

#if TARGET_X86
    WORD m_cbStackArgumentSize;
    LPVOID m_pRetThunk;
#endif // TARGET_X86

        // This field gets set only when this MethodDesc is marked as PreImplemented
        public RelativePointer m_pStubMD;
    }

    [Flags]
    public enum MethodDescClassification : ushort {
        // Method is IL, FCall etc., see MethodClassification above.
        mdcClassification = 0x0007,
        mdcClassificationCount = mdcClassification + 1,

        // Note that layout of code:MethodDesc::s_ClassificationSizeTable depends on the exact values
        // of mdcHasNonVtableSlot and mdcMethodImpl

        // Has local slot (vs. has real slot in MethodTable)
        mdcHasNonVtableSlot = 0x0008,

        // Method is a body for a method impl (MI_MethodDesc, MI_NDirectMethodDesc, etc)
        // where the function explicitly implements IInterface.foo() instead of foo().
        mdcMethodImpl = 0x0010,

        // Has slot for native code
        mdcHasNativeCodeSlot = 0x0020,

#if FEATURE_COMINTEROP
        mdcHasComPlusCallInfo = 0x0040,
#else
        // unused                           = 0x0040,
#endif

        // Method is static
        mdcStatic = 0x0080,

        // unused                           = 0x0100,
        // unused                           = 0x0200,

        // Duplicate method. When a method needs to be placed in multiple slots in the
        // method table, because it could not be packed into one slot. For eg, a method
        // providing implementation for two interfaces, MethodImpl, etc
        mdcDuplicate = 0x0400,

        // Has this method been verified?
        mdcVerifiedState = 0x0800,

        // Is the method verifiable? It needs to be verified first to determine this
        mdcVerifiable = 0x1000,

        // Is this method ineligible for inlining?
        mdcNotInline = 0x2000,

        // Is the method synchronized
        mdcSynchronized = 0x4000,

        // Does the method's slot number require all 16 bits
        mdcRequiresFullSlotNumber = 0x8000
    };


    public static class MethodDescCommon
    {
        public static int[] MethodDescSizes;
        public static int[] s_ClassificationSizeTable;
        private static int[] MethodDescSizesWithAdjustment(int adjustment) {
            int[] result = new int[MethodDescSizes.Length];

            for (int i = 0; i < MethodDescSizes.Length; ++i) {
                result[i] = MethodDescSizes[i] + adjustment;
            }

            return result;
        }


        public struct NonVtableSlot {
            public IntPtr value;
        }

        public struct NativeCodeSlot {
            public RelativePointer value;
        }

        public struct FixupListSlot {
            public RelativePointer value;
        }

        static MethodDescCommon() {
            MethodDescSizes = new int[]
            {
                Marshal.SizeOf<MethodDescFields>(),                 /* mcIL            */  
                Marshal.SizeOf<FCallMethodDescFields>(),            /* mcFCall         */  
                Marshal.SizeOf<NDirectMethodDescFields>(),          /* mcNDirect       */  
                Marshal.SizeOf<EEImplMethodDescFields>(),           /* mcEEImpl        */  
                Marshal.SizeOf<ArrayMethodDescFields>(),            /* mcArray         */  
                Marshal.SizeOf<InstantiatedMethodDescFields>(),     /* mcInstantiated  */  
                Marshal.SizeOf<ComPlusCallMethodDescFields>(),      /* mcComInterOp    */  
                Marshal.SizeOf<DynamicMethodDescFields>()           /* mcDynamic       */
            };


            List<int> sizes = new List<int>();


            sizes.AddRange(MethodDescSizesWithAdjustment(0));

            // This extended part of the table is used for faster MethodDesc size lookup.
            // We index using optional slot flags into it
            sizes.AddRange(MethodDescSizesWithAdjustment(Marshal.SizeOf<NonVtableSlot>()));
            sizes.AddRange(MethodDescSizesWithAdjustment(Marshal.SizeOf<MethodImpl>()));
            sizes.AddRange(MethodDescSizesWithAdjustment(Marshal.SizeOf<NonVtableSlot>() + Marshal.SizeOf<MethodImpl>()));

            sizes.AddRange(MethodDescSizesWithAdjustment(Marshal.SizeOf<NativeCodeSlot>()));
            sizes.AddRange(MethodDescSizesWithAdjustment(Marshal.SizeOf<NonVtableSlot>() + Marshal.SizeOf<NativeCodeSlot>()));
            sizes.AddRange(MethodDescSizesWithAdjustment(Marshal.SizeOf<MethodImpl>() + Marshal.SizeOf<NativeCodeSlot>()));
            sizes.AddRange(MethodDescSizesWithAdjustment(Marshal.SizeOf<NonVtableSlot>() + Marshal.SizeOf<MethodImpl>() + Marshal.SizeOf<NativeCodeSlot>()));

#if FEATURE_COMINTEROP
            sizes.AddRange(MethodDescSizesWithAdjustment(Marshal.SizeOf<ComPlusCallInfo>()));
            sizes.AddRange(MethodDescSizesWithAdjustment(Marshal.SizeOf<NonVtableSlot>() + Marshal.SizeOf<ComPlusCallInfo>()));
            sizes.AddRange(MethodDescSizesWithAdjustment(Marshal.SizeOf<MethodImpl>() + Marshal.SizeOf<ComPlusCallInfo>()));
            sizes.AddRange(MethodDescSizesWithAdjustment(Marshal.SizeOf<NonVtableSlot>() + Marshal.SizeOf<MethodImpl>() + Marshal.SizeOf<ComPlusCallInfo>()));

            sizes.AddRange(MethodDescSizesWithAdjustment(Marshal.SizeOf<NativeCodeSlot>() + Marshal.SizeOf<ComPlusCallInfo>()));
            sizes.AddRange(MethodDescSizesWithAdjustment(Marshal.SizeOf<NonVtableSlot>() + Marshal.SizeOf<NativeCodeSlot>() + Marshal.SizeOf<ComPlusCallInfo>()));
            sizes.AddRange(MethodDescSizesWithAdjustment(Marshal.SizeOf<MethodImpl>() + Marshal.SizeOf<NativeCodeSlot>() + Marshal.SizeOf<ComPlusCallInfo>()));
            sizes.AddRange(MethodDescSizesWithAdjustment(Marshal.SizeOf<NonVtableSlot>() + Marshal.SizeOf<MethodImpl>() + Marshal.SizeOf<NativeCodeSlot>() +
                Marshal.SizeOf<ComPlusCallInfo>()));
#endif
            s_ClassificationSizeTable = sizes.ToArray();

        }

    }

    public class MethodDescGeneric<T> : NativeObject<T> where T:struct, IMethodDescFieldsProvider {

 
       

        //#define METHOD_DESC_SIZES(adjustment)                                       \
        //adjustment + sizeof(MethodDesc),                 /* mcIL            */  \
        //adjustment + sizeof(FCallMethodDesc),            /* mcFCall         */  \
        //adjustment + sizeof(NDirectMethodDesc),          /* mcNDirect       */  \
        //adjustment + sizeof(EEImplMethodDesc),           /* mcEEImpl        */  \
        //adjustment + sizeof(ArrayMethodDesc),            /* mcArray         */  \
        //adjustment + sizeof(InstantiatedMethodDesc),     /* mcInstantiated  */  \
        //adjustment + sizeof(ComPlusCallMethodDesc),      /* mcComInterOp    */  \
        //adjustment + sizeof(DynamicMethodDesc)           /* mcDynamic       */

        /* public static byte[] s_ClassificationSizeTable = new byte[]
         {
             METHOD_DESC_SIZES(0),

             // This extended part of the table is used for faster MethodDesc size lookup.
             // We index using optional slot flags into it
             METHOD_DESC_SIZES(sizeof(NonVtableSlot)),
             METHOD_DESC_SIZES(sizeof(MethodImpl)),
             METHOD_DESC_SIZES(sizeof(NonVtableSlot) + sizeof(MethodImpl)),

             METHOD_DESC_SIZES(sizeof(NativeCodeSlot)),
             METHOD_DESC_SIZES(sizeof(NonVtableSlot) + sizeof(NativeCodeSlot)),
             METHOD_DESC_SIZES(sizeof(MethodImpl) + sizeof(NativeCodeSlot)),
             METHOD_DESC_SIZES(sizeof(NonVtableSlot) + sizeof(MethodImpl) + sizeof(NativeCodeSlot)),

 #ifdef FEATURE_COMINTEROP
             METHOD_DESC_SIZES(sizeof(ComPlusCallInfo)),
             METHOD_DESC_SIZES(sizeof(NonVtableSlot) + sizeof(ComPlusCallInfo)),
             METHOD_DESC_SIZES(sizeof(MethodImpl) + sizeof(ComPlusCallInfo)),
             METHOD_DESC_SIZES(sizeof(NonVtableSlot) + sizeof(MethodImpl) + sizeof(ComPlusCallInfo)),

             METHOD_DESC_SIZES(sizeof(NativeCodeSlot) + sizeof(ComPlusCallInfo)),
             METHOD_DESC_SIZES(sizeof(NonVtableSlot) + sizeof(NativeCodeSlot) + sizeof(ComPlusCallInfo)),
             METHOD_DESC_SIZES(sizeof(MethodImpl) + sizeof(NativeCodeSlot) + sizeof(ComPlusCallInfo)),
             METHOD_DESC_SIZES(sizeof(NonVtableSlot) + sizeof(MethodImpl) + sizeof(NativeCodeSlot) + sizeof(ComPlusCallInfo))
 #endif
         };*/


#if TARGET_64BIT
        public const int ALIGNMENT_SHIFT = 3;
#else
        public const int ALIGNMENT_SHIFT = 2;
#endif
        public const uint ALIGNMENT = (1 << ALIGNMENT_SHIFT);
        public const uint ALIGNMENT_MASK = (ALIGNMENT - 1);

        public MethodDescGeneric(IntPtr ptr) : base(ptr)
        {

        }

        [Flags]
        public enum flag2 : byte {
            // enum_flag2_HasPrecode implies that enum_flag2_HasStableEntryPoint is set.
            HasStableEntryPoint = 0x01,   // The method entrypoint is stable (either precode or actual code)
            HasPrecode = 0x02,   // Precode has been allocated for this method

            IsUnboxingStub = 0x04,
             unused0                                       = 0x08,

            IsJitIntrinsic = 0x10,   // Jit may expand method as an intrinsic

            IsEligibleForTieredCompilation = 0x20,

            RequiresCovariantReturnTypeChecking = 0x40,

             unused1                           = 0x80,
        };


        public static MethodDesc GetMethodDescFromStubAddr(IntPtr addr, bool fSpeculative = false /*=FALSE*/) {
 
#if HAS_COMPACT_ENTRYPOINTS
            if (MethodDescChunk::IsCompactEntryPointAtAddress(addr)) {
                pMD = MethodDescChunk::GetMethodDescFromCompactEntryPoint(addr, fSpeculative);
                RETURN(pMD);
            }
#endif // HAS_COMPACT_ENTRYPOINTS

            // Otherwise this must be some kind of precode
            //
            Precode pPrecode = Precode.GetPrecodeFromEntryPoint(addr, fSpeculative);
            //PREFIX_ASSUME(fSpeculative || (pPrecode != NULL));
            if (pPrecode != null) {
                var pMD = pPrecode.GetMethodDesc(fSpeculative);
                return pMD;
            }

            return null;
        }


        public bool HasPrecode() {
            return (Fields.GetBaseFields().m_bFlags2 & (byte)flag2.HasPrecode) != 0;
        }

        public bool HasNativeCodeSlot() {
            return (Fields.GetBaseFields().m_wFlags & MethodDescClassification.mdcHasNativeCodeSlot) != 0;
        }

        public MethodClassification GetClassification() {
            return (MethodClassification)((uint)(Fields.GetBaseFields().m_wFlags & MethodDescClassification.mdcClassification));
        }

        public bool IsUnboxingStub() {
            return (Fields.GetBaseFields().m_bFlags2 & (byte)flag2.IsUnboxingStub) != 0;
        }

        public bool IsInstantiatingStub() {
            return
                (GetClassification() == MethodClassification.mcInstantiated)
                && !IsUnboxingStub()
                && AsInstantiatedMethodDesc().IMD_IsWrapperStubWithInstantiations();
        }


        bool IsGenericMethodDefinition() {
            return GetClassification() == MethodClassification.mcInstantiated && AsInstantiatedMethodDesc().IMD_IsGenericMethodDefinition();
        }

        public bool IsWrapperStub() {
            return (IsUnboxingStub() || IsInstantiatingStub());
        }

        public bool IsEnCAddedMethod()
        {
            return (GetClassification() == MethodClassification.mcInstantiated) && AsInstantiatedMethodDesc().IMD_IsEnCAddedMethod();
        }

        public bool IsEnCMethod()
        {
            return false; //TODO: implement
            /* WRAPPER_NO_CONTRACT;
             Module* pModule = GetModule();
             PREFIX_ASSUME(pModule != NULL);
             return pModule->IsEditAndContinueEnabled();*/
        }

        public bool IsFCall() {
            return MethodClassification.mcFCall == GetClassification();
        }

        public bool HasNonVtableSlot()
        {
            return (Fields.GetBaseFields().m_wFlags & MethodDescClassification.mdcHasNonVtableSlot) != 0;
        }

        public static int GetBaseSize(MethodClassification classification) {
            //_ASSERTE(classification < mdcClassificationCount);
            return MethodDescCommon.s_ClassificationSizeTable[(int)classification];
        }


        public int GetBaseSize() {
            return GetBaseSize(GetClassification());
        }

        public bool IsZapped()
        {
#if FEATURE_PREJIT
            return GetMethodDescChunk().IsZapped();
#else
            return false;
#endif
        }

        public int GetMethodDescIndex()
        {
            return Fields.GetBaseFields().m_chunkIndex;
        }


        public MethodDescChunk GetMethodDescChunk()
        {
            return new MethodDescChunk(NativePtr - (int)(Marshal.SizeOf<MethodDescChunkFields>() + (GetMethodDescIndex() * MethodDesc.ALIGNMENT))); 
        }

        public MethodTable GetMethodTable()
        {
            var pChunk = GetMethodDescChunk();
            return pChunk.GetMethodTable();
        }

        public bool RequiresFullSlotNumber() {
            return (Fields.GetBaseFields().m_wFlags & MethodDescClassification.mdcRequiresFullSlotNumber) != 0;
        }

        enum enum_packed : ushort {
            enum_packedSlotLayout_SlotMask = 0x03FF,
            enum_packedSlotLayout_NameHashMask = 0xFC00
        };

        public uint GetSlot() {
#if !DACCESS_COMPILE
            // The DAC build uses this method to test for "sanity" of a MethodDesc, and
            // doesn't need the assert.
            //_ASSERTE(!IsEnCAddedMethod() || !"Cannot get slot for method added via EnC");
#endif // !DACCESS_COMPILE

            Console.WriteLine("GetSlot: A");
            // Check if this MD is using the packed slot layout
            if (!RequiresFullSlotNumber()) {
                Console.WriteLine("GetSlot: B");
                return (uint)(Fields.GetBaseFields().m_wSlotNumber & (ushort)enum_packed.enum_packedSlotLayout_SlotMask);
            }
            Console.WriteLine("GetSlot: C");
            return Fields.GetBaseFields().m_wSlotNumber;
        }

        public IntPtr GetMethodEntryPoint() {
            // Similarly to SetMethodEntryPoint(), it is up to the caller to ensure that calls to this function are appropriately
            // synchronized

            // Keep implementations of MethodDesc::GetMethodEntryPoint and MethodDesc::GetAddrOfSlot in sync!

            
            if (HasNonVtableSlot()) {
                Console.WriteLine("GetMethodEntryPoint: HasNonVtableSlot");

                int size = GetBaseSize();
                var pSlot = NativePtr + size;

                // return IsZapped() ? NonVtableSlot::GetValueAtPtr(pSlot) : *PTR_PCODE(pSlot);
                return Marshal.ReadIntPtr(pSlot); //TODO: add support for zapped?
            }
            Console.WriteLine("GetMethodEntryPoint: NOT  HasNonVtableSlot");
            //_ASSERTE(GetMethodTable()->IsCanonicalMethodTable());
            return GetMethodTable().GetSlot(GetSlot());
        }

        public IntPtr GetStableEntryPoint() {

            Debug.Assert(HasStableEntryPoint());
            Debug.Assert(!IsVersionableWithVtableSlotBackpatch());

            return GetMethodEntryPoint();
        }

        bool IsEligibleForTieredCompilation() {

#if FEATURE_TIERED_COMPILATION
            return (Fields.GetBaseFields().m_bFlags2 & (byte)flag2.IsEligibleForTieredCompilation) != 0;
#else
            return false;
#endif
        }

        public bool IsVersionableWithoutJumpStamp() {
#if FEATURE_CODE_VERSIONING
            return IsEligibleForTieredCompilation();
#else
            return false;
#endif
        }

        public bool IsNativeCodeStableAfterInit() {

#if FEATURE_JIT_PITCHING
        if (IsPitchable())
            return false;
#endif

            return !IsVersionableWithoutJumpStamp() && !IsEnCMethod();
        }

        public bool HasStableEntryPoint() {
            return (Fields.GetBaseFields().m_bFlags2 & (byte)flag2.HasStableEntryPoint) != 0;
        }

        public Precode GetPrecode() {
            Debug.Assert(HasPrecode());
            return Precode.GetPrecodeFromEntryPoint(GetStableEntryPoint());
        }

        public bool IsPointingToNativeCode() {

            if (!HasStableEntryPoint())
                return false;

            if (!HasPrecode())
                return true;

            return GetPrecode().IsPointingToNativeCode(GetNativeCode());
        }

        public IntPtr GetNativeCode() {
            if (HasNativeCodeSlot()) {
                // When profiler is enabled, profiler may ask to rejit a code even though we
                // we have ngen code for this MethodDesc.  (See MethodDesc::DoPrestub).
                // This means that NativeCodeSlot::GetValueMaybeNullAtPtr(GetAddrOfNativeCodeSlot())
                // is not stable. It can turn from non-zero to zero.


                var pCode = new IntPtr(Marshal.ReadIntPtr(GetAddrOfNativeCodeSlot()).ToInt64() & ~FIXUP_LIST_MASK);
#if _TARGET_ARM_
                if (pCode != NULL)
                    pCode |= THUMB_CODE;
#endif
                return pCode;
            }

            if (!HasStableEntryPoint() || HasPrecode())
                return IntPtr.Zero;

            return GetStableEntryPoint();
        }


        public bool IsPointingToStableNativeCode() {
            if (!IsNativeCodeStableAfterInit())
                return false;

            return IsPointingToNativeCode();
        }

        public bool IsVirtualSlot() {
            return GetSlot() < GetMethodTable().GetNumVirtuals();
        }

        public bool IsVtableSlot() {
            return IsVirtualSlot() && !HasNonVtableSlot();
        }

        public bool IsStatic() {
            return (Fields.GetBaseFields().m_wFlags & MethodDescClassification.mdcStatic) != 0;
        }

        public bool IsInterface() {
            return GetMethodTable().IsInterface();
        }

        public bool Helper_IsEligibleForVersioningWithVtableSlotBackpatch() {
            //_ASSERTE(IsVersionableWithoutJumpStamp());
           // _ASSERTE(IsIL() || IsDynamicMethod());

#if FEATURE_CODE_VERSIONING && !CROSSGEN_COMPILE
        //_ASSERTE(CodeVersionManager::IsMethodSupported(PTR_MethodDesc(this)));

        
        return
            // Policy
           // g_pConfig->BackpatchEntryPointSlots()
            true &&

            // Functional requirement - The entry point must be through a vtable slot in the MethodTable that may be recorded
            // and backpatched
            IsVtableSlot() &&

            // Functional requirement - True interface methods are not backpatched, see DoBackpatch()
            !(IsInterface() && !IsStatic());
#else
            // Entry point slot backpatch is disabled for CrossGen
            return false;
#endif
        }

        public bool IsVersionableWithVtableSlotBackpatch() {
            return IsVersionableWithoutJumpStamp() && Helper_IsEligibleForVersioningWithVtableSlotBackpatch();
        }

        bool IsVersionableWithPrecode() {
            return IsVersionableWithoutJumpStamp() && !Helper_IsEligibleForVersioningWithVtableSlotBackpatch();
        }

        public bool IsIL() {
            return MethodClassification.mcIL == GetClassification() || MethodClassification.mcInstantiated == GetClassification();
        }
        public bool IsArray()
        {
            return MethodClassification.mcArray == GetClassification();
        }

        public bool IsNoMetadata()
        {
            return MethodClassification.mcDynamic == GetClassification();
        }

        public uint GetAttrs()
        {
            if (IsArray())
                return (new ArrayMethodDesc(NativePtr)).GetAttrs();
            else if (IsNoMetadata())
                return (new DynamicMethodDesc(NativePtr)).GetAttrs();;

            throw new NotImplementedException();
            /*uint dwAttributes;
            if (FAILED(GetMDImport()->GetMethodDefProps(GetMemberDef(), &dwAttributes)))
            {   // Class loader already asked for attributes, so this should always succeed (unless there's a 
                // bug or a new code path)
                _ASSERTE(!"If this ever fires, then this method should return HRESULT");
                return 0;
            }
            return dwAttributes;*/
        }

        public bool IsVirtual() {
            return (GetAttrs() & (int)CorMethodAttr.mdVirtual) != 0;
        }

        public bool IsAbstract() {
            return (GetAttrs() & (int)CorMethodAttr.mdAbstract) != 0;
        }

        public bool ContainsGenericVariables() {
            // If this is a method of a generic type, does the type have
            // non-instantiated type arguments

            /*if (TypeHandle(GetMethodTable()).ContainsGenericVariables())
                return TRUE;*/
            //TODO: implement

            if (IsGenericMethodDefinition())
                return true;

            // If this is an instantiated generic method, are there are any generic type variables
            /*if (GetNumGenericMethodArgs() != 0) {
                Instantiation methodInst = GetMethodInstantiation();
                for (DWORD i = 0; i < methodInst.GetNumArgs(); i++) {
                    if (methodInst[i].ContainsGenericVariables())
                        return TRUE;
                }
            }*/
            //TODO: implement

            return false;
        }

        public bool MayHaveNativeCode() {
            // This code flow of this method should roughly match the code flow of MethodDesc::DoPrestub.

            switch (GetClassification()) {
                case MethodClassification.mcIL:              // IsIL() case. Handled below.
                    break;
                case MethodClassification.mcFCall:           // FCalls do not have real native code.
                    return false;
                case MethodClassification.mcNDirect:         // NDirect never have native code (note that the NDirect method
                    return false;       //  does not appear as having a native code even for stubs as IL)
                case MethodClassification.mcEEImpl:          // Runtime provided implementation. No native code.
                    return false;
                case MethodClassification.mcArray:           // Runtime provided implementation. No native code.
                    return false;
                case MethodClassification.mcInstantiated:    // IsIL() case. Handled below.
                    break;
#if FEATURE_COMINTEROP 
                case MethodClassification.mcComInterop:      // Generated stub. No native code.
                    return false;
#endif
                case MethodClassification.mcDynamic:         // LCG or stub-as-il.
                    return true;
                default:
                    throw new Exception("Unknown classification");
            }

            if (!IsIL())
            {
                throw new Exception("!IsIL()");
            }
            //_ASSERTE(IsIL());

            if ((IsInterface() && !IsStatic() && IsVirtual() && IsAbstract()) || IsWrapperStub() || ContainsGenericVariables() || IsAbstract()) {
                return false;
            }

            return true;
        }

        public bool MayHavePrecode() {
            
            bool result = IsVersionableWithoutJumpStamp() ? IsVersionableWithPrecode() : !MayHaveNativeCode();
            //_ASSERTE(!result || !IsVersionableWithVtableSlotBackpatch());
            return result;
        }

        public IntPtr GetAddrOfSlot() {
            // Keep implementations of MethodDesc::GetMethodEntryPoint and MethodDesc::GetAddrOfSlot in sync!

            if (HasNonVtableSlot()) {
                // Slots in NGened images are relative pointers
               // _ASSERTE(!IsZapped());

                var size = GetBaseSize();

                return NativePtr + size;
            }

           // _ASSERTE(GetMethodTable()->IsCanonicalMethodTable());
            return GetMethodTable().GetSlotPtr(GetSlot());
        }

        public IntPtr GetTemporaryEntryPoint() {
            MethodDescChunk pChunk = GetMethodDescChunk();
            //_ASSERTE(pChunk->HasTemporaryEntryPoints());

            int lo = 0, hi = pChunk.GetCount() - 1;

            // Find the temporary entrypoint in the chunk by binary search
            while (lo < hi) {
                int mid = (lo + hi) / 2;

                var pEntryPoint2 = pChunk.GetTemporaryEntryPoint(mid);

                MethodDesc pMD = MethodDesc.GetMethodDescFromStubAddr(pEntryPoint2);
                if (NativePtr == pMD.NativePtr)
                    return pEntryPoint2;

                if (NativePtr.ToInt64() > pMD.NativePtr.ToInt64())
                    lo = mid + 1;
                else
                    hi = mid - 1;
            }

            //_ASSERTE(lo == hi);

            var pEntryPoint = pChunk.GetTemporaryEntryPoint(lo);

#if _DEBUG
            MethodDesc* pMD = MethodDesc::GetMethodDescFromStubAddr(pEntryPoint);
            _ASSERTE(PTR_HOST_TO_TADDR(this) == PTR_HOST_TO_TADDR(pMD));
#endif

            return pEntryPoint;
        }

        public bool IsNDirect() {
            return MethodClassification.mcNDirect == GetClassification();
        }

#if FEATURE_COMINTEROP 
        public bool IsComPlusCall() {
            return MethodClassification.mcComInterop == GetClassification();
        }
        public bool IsGenericComPlusCall()
        {
            return (MethodClassification.mcInstantiated == GetClassification() && AsInstantiatedMethodDesc().IMD_HasComPlusCallInfo());
        }

    //inline void SetupGenericComPlusCall();
#else // !FEATURE_COMINTEROP
        // hardcoded to return FALSE to improve code readibility
        inline DWORD IsComPlusCall() {
            LIMITED_METHOD_CONTRACT;
            return FALSE;
        }
        inline DWORD IsGenericComPlusCall() {
            LIMITED_METHOD_CONTRACT;
            return FALSE;
        }
#endif // !FEATURE_COMINTEROP



        public bool RequiresMethodDescCallingConvention(bool fEstimateForChunk = false /*=FALSE*/) {
            // Interop marshaling is implemented using shared stubs
            if (IsNDirect() || IsComPlusCall() || IsGenericComPlusCall())
                return true;

            return false;
        }

        public PrecodeType GetPrecodeType() {

            PrecodeType precodeType = PrecodeType.PRECODE_INVALID;

#if HAS_FIXUP_PRECODE
            if (!RequiresMethodDescCallingConvention()) {
                // Use the more efficient fixup precode if possible
                precodeType = PrecodeType.PRECODE_FIXUP;
            } else
#endif // HAS_FIXUP_PRECODE
            {
                precodeType = PrecodeType.PRECODE_STUB;
            }

            return precodeType;
        }

        public Precode GetOrCreatePrecode() {
  
           // _ASSERTE(!IsVersionableWithVtableSlotBackpatch());

            if (HasPrecode()) {
                return GetPrecode();
            }

            var pSlot = GetAddrOfSlot();
            var tempEntry = GetTemporaryEntryPoint();

            PrecodeType requiredType = GetPrecodeType();
            PrecodeType availableType = PrecodeType.PRECODE_INVALID;

            if (!GetMethodDescChunk().HasCompactEntryPoints()) {
                availableType = Precode.GetPrecodeFromEntryPoint(tempEntry).GetType();
            }

            // Allocate the precode if necessary
            if (requiredType != availableType) {
                // code:Precode::AllocateTemporaryEntryPoints should always create precode of the right type for dynamic methods.
                // If we took this path for dynamic methods, the precode may leak since we may allocate it in domain-neutral loader heap.
                
                throw new NotImplementedException();
                /*_ASSERTE(!IsLCGMethod());

                AllocMemTracker amt;
                Precode* pPrecode = Precode::Allocate(requiredType, this, GetLoaderAllocator(), &amt);
                PCODE newVal;
                PCODE oldVal;
                TADDR* slotAddr;

                if (IsVtableSlot()) {
                    newVal = MethodTable::VTableIndir2_t::GetRelative(pSlot, pPrecode->GetEntryPoint());
                    oldVal = MethodTable::VTableIndir2_t::GetRelative(pSlot, tempEntry);
                    slotAddr = (TADDR*)EnsureWritablePages((MethodTable::VTableIndir2_t*)pSlot);
                } else {
                    newVal = pPrecode->GetEntryPoint();
                    oldVal = tempEntry;
                    slotAddr = (TADDR*)EnsureWritablePages((PCODE*)pSlot);
                }

                if (FastInterlockCompareExchangePointer(slotAddr, (TADDR)newVal, (TADDR)oldVal) == oldVal)
                    amt.SuppressRelease();*/
            }

            // Set the flags atomically
           // InterlockedUpdateFlags2(enum_flag2_HasStableEntryPoint | enum_flag2_HasPrecode, TRUE);

            IntPtr addr;
            if (IsVtableSlot())
            {
                addr = Marshal.ReadIntPtr(pSlot); //((MethodTable::VTableIndir2_t*)pSlot)->GetValue();
            } else {
                addr = Marshal.ReadIntPtr(pSlot); //*((PCODE*)pSlot);
            }
            return Precode.GetPrecodeFromEntryPoint(addr);
        }


        public IntPtr TryGetMultiCallableAddrOfCode(CORINFO_ACCESS_FLAGS accessFlags) {
            
            if (IsGenericMethodDefinition()) {
                throw new Exception("Cannot take the address of an uninstantiated generic method.");
            }

            if ((accessFlags & CORINFO_ACCESS_FLAGS.CORINFO_ACCESS_LDFTN) == CORINFO_ACCESS_FLAGS.CORINFO_ACCESS_LDFTN) {
                // Whenever we use LDFTN on shared-generic-code-which-requires-an-extra-parameter
                // we need to give out the address of an instantiating stub.  This is why we give
                // out GetStableEntryPoint() for the IsInstantiatingStub() case: this is
                // safe.  But first we assert that we only use GetMultiCallableAddrOfCode on
                // the instantiating stubs and not on the shared code itself.
                //_ASSERTE(!RequiresInstArg());
               // _ASSERTE(!IsSharedByGenericMethodInstantiations());

                // No other access flags are valid with CORINFO_ACCESS_LDFTN
                //_ASSERTE((accessFlags & ~CORINFO_ACCESS_LDFTN) == 0);
            }

            // We create stable entrypoints for these upfront
            if (IsWrapperStub() || IsEnCAddedMethod())
                return GetStableEntryPoint();


            // For EnC always just return the stable entrypoint so we can update the code
            if (IsEnCMethod())
            { 
                return GetStableEntryPoint();
            }
               

            // If the method has already been jitted, we can give out the direct address
            // Note that we may have previously created a FuncPtrStubEntry, but
            // GetMultiCallableAddrOfCode() does not need to be idempotent.

            if (IsFCall()) {
                throw new NotImplementedException();
                // Call FCalls directly when possible
               /* if (!IsInterface() && !GetMethodTable()->ContainsGenericVariables()) {
                    BOOL fSharedOrDynamicFCallImpl;
                    PCODE pFCallImpl = ECall::GetFCallImpl(this, &fSharedOrDynamicFCallImpl);

                    if (!fSharedOrDynamicFCallImpl)
                        return pFCallImpl;

                    // Fake ctors share one implementation that has to be wrapped by prestub
                    GetOrCreatePrecode();
                }*/
            } else {
                if (IsPointingToStableNativeCode())
                    return GetNativeCode();
            }

            if (HasStableEntryPoint())
                return GetStableEntryPoint();

            if (IsVersionableWithVtableSlotBackpatch()) {
                // Caller has to call via slot or allocate funcptr stub
                return IntPtr.Zero;
            }

            // Force the creation of the precode if we would eventually got one anyway
            if (MayHavePrecode())
                return GetOrCreatePrecode().GetEntryPoint();

#if HAS_COMPACT_ENTRYPOINTS
            // Caller has to call via slot or allocate funcptr stub
            return NULL;
#else // HAS_COMPACT_ENTRYPOINTS
            //
            // Embed call to the temporary entrypoint into the code. It will be patched 
            // to point to the actual code later.
            //
            return GetTemporaryEntryPoint();
#endif // HAS_COMPACT_ENTRYPOINTS
        }


  //     public IntPtr
  //         GetMultiCallableAddrOfCode(CORINFO_ACCESS_FLAGS accessFlags = CORINFO_ACCESS_FLAGS.CORINFO_ACCESS_LDFTN)
  //     {
  //         IntPtr ret = TryGetMultiCallableAddrOfCode(accessFlags);
  //
  //         if (ret == IntPtr.Zero) {
  //             // We have to allocate funcptr stub
  //             //ret = GetLoaderAllocator()->GetFuncPtrStubs()->GetFuncPtrStub(this);
  //         }
  //
  //         return ret;
  //     }

        public IntPtr GetAddrOfNativeCodeSlot() {

            var size = MethodDescCommon.s_ClassificationSizeTable[(ushort)(Fields.GetBaseFields().m_wFlags & (MethodDescClassification.mdcClassification | MethodDescClassification.mdcHasNonVtableSlot | MethodDescClassification.mdcMethodImpl))];

            return new IntPtr(NativePtr.ToInt64() + size);
        }

        public const long FIXUP_LIST_MASK = 1;
        public int SizeOf()
        {
            var id = (ushort) (Fields.GetBaseFields().m_wFlags &
                             (MethodDescClassification.mdcClassification
                              | MethodDescClassification.mdcHasNonVtableSlot
                              | MethodDescClassification.mdcMethodImpl
#if FEATURE_COMINTEROP
                              | MethodDescClassification.mdcHasComPlusCallInfo
#endif
                              | MethodDescClassification.mdcHasNativeCodeSlot));

            var size = MethodDescCommon.s_ClassificationSizeTable[id];

#if FEATURE_PREJIT
            if (HasNativeCodeSlot()) {
                size += ( (Marshal.ReadIntPtr(GetAddrOfNativeCodeSlot()).ToInt64() & FIXUP_LIST_MASK) != 0) ? Marshal.SizeOf<MethodDescCommon.FixupListSlot>() : 0;
            }
#endif

            return size;
        }


        public InstantiatedMethodDesc AsInstantiatedMethodDesc()
        {

            if(GetClassification() != MethodClassification.mcInstantiated)
            {
                throw new Exception("WTF");
            }
            return new InstantiatedMethodDesc(NativePtr);
        }

    }

    public class MethodDesc : MethodDescGeneric<MethodDescFieldsWrapper>
    {
        public MethodDesc(IntPtr ptr) : base(ptr)
        {
        }

    }


    public class InstantiatedMethodDesc : MethodDescGeneric<InstantiatedMethodDescFields>
    {
        enum Kind : ushort
        {
            KindMask = 0x07,
            GenericMethodDefinition = 0x00,
            UnsharedMethodInstantiation = 0x01,
            SharedMethodInstantiation = 0x02,
            WrapperStubWithInstantiations = 0x03,

#if EnC_SUPPORTED
            // Non-virtual method added through EditAndContinue.
            EnCAddedMethod = 0x07,
#endif // EnC_SUPPORTED

            Unrestored = 0x08,

#if FEATURE_COMINTEROP
            HasComPlusCallInfo = 0x10, // this IMD contains an optional ComPlusCallInfo
#endif // FEATURE_COMINTEROP
        };

        public InstantiatedMethodDesc(IntPtr ptr) : base(ptr)
        {
        }

        public bool IMD_IsWrapperStubWithInstantiations() {
            return ((Fields.m_wFlags2 & (ushort)Kind.KindMask) == (ushort)Kind.WrapperStubWithInstantiations);
        }

        public bool IMD_IsGenericMethodDefinition() {
 
            return ((Fields.m_wFlags2 & (ushort)Kind.KindMask) == (ushort)Kind.GenericMethodDefinition);
        }

        public bool IMD_IsEnCAddedMethod() {

#if EnC_SUPPORTED
            return ((Fields.m_wFlags2 & (ushort)Kind.KindMask) == (ushort)Kind.EnCAddedMethod);
#else
            return false;
#endif
        }

#if FEATURE_COMINTEROP
        public bool IMD_HasComPlusCallInfo() {
            return ((Fields.m_wFlags2 & (ushort)Kind.HasComPlusCallInfo) != 0);
        }
#endif
    }



    [StructLayout(LayoutKind.Sequential)]
    struct FCallMethodDescFields : IMethodDescFieldsProvider {
        public MethodDescFields BaseFields;
        public uint m_dwECallID;
#if TARGET_64BIT
        public uint m_padding;

        public MethodDescFields GetBaseFields()
        {
            return BaseFields;
        }
#endif
    }

    [StructLayout(LayoutKind.Sequential)]
    struct NDirectMethodDescFields : IMethodDescFieldsProvider {
        public MethodDescFields BaseFields;

        public struct temp1_s
        {
            // If we are hosted, stack imbalance MDA is active, or alignment thunks are needed,
            // we will intercept m_pNDirectTarget. The true target is saved here.
            public IntPtr m_pNativeNDirectTarget;

            // Information about the entrypoint
            public IntPtr m_pszEntrypointName;

            public RelativePointer m_pszLibName; //union with DWORD m_dwECallID; // ECallID for QCalls


            // The writeable part of the methoddesc.
            #if FEATURE_NGEN_RELOCS_OPTIMIZATIONS
                RelativePointer<PTR_NDirectWriteableData>    m_pWriteableData;
            #else
                public IntPtr m_pWriteableData;
            #endif


#if HAS_NDIRECT_IMPORT_PRECODE
            public RelativePointer m_pImportThunkGlue;
#else 
            NDirectImportThunkGlue m_ImportThunkGlue;
#endif

            public uint m_DefaultDllImportSearchPathsAttributeValue; // DefaultDllImportSearchPathsAttribute is saved.

            // Various attributes needed at runtime.
            public ushort m_wFlags;

#if TARGET_X86
        // Size of outgoing arguments (on stack). Note that in order to get the @n stdcall name decoration,
        // it may be necessary to subtract 4 as the hidden large structure pointer parameter does not count.
        // See code:kStdCallWithRetBuf
        public ushort        m_cbStackArgumentSize;
#endif // defined(TARGET_X86)

            // This field gets set only when this MethodDesc is marked as PreImplemented
            public RelativePointer m_pStubMD;
        }

        public temp1_s ndirect;
        public MethodDescFields GetBaseFields()
        {
            return BaseFields;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct StoredSigMethodDescFields : IMethodDescFieldsProvider {
        public MethodDescFields BaseFields;

        public RelativePointer m_pSig;
        public uint m_cSig;
#if TARGET_64BIT
        // m_dwExtendedFlags is not used by StoredSigMethodDesc itself.
        // It is used by child classes. We allocate the space here to get
        // optimal layout.
        public uint m_dwExtendedFlags;
#endif
        public MethodDescFields GetBaseFields()
        {
            return BaseFields;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct EEImplMethodDescFields :  IMethodDescFieldsProvider {
        public StoredSigMethodDescFields BaseFields;
        public MethodDescFields GetBaseFields()
        {
            return BaseFields.BaseFields;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct ArrayMethodDescFields : IMethodDescFieldsProvider {
        public StoredSigMethodDescFields BaseFields;

        public MethodDescFields GetBaseFields()
        {
            return BaseFields.BaseFields;
        }
    }

    class ArrayMethodDesc : MethodDescGeneric<ArrayMethodDescFields>
    {
        public ArrayMethodDesc(IntPtr ptr) : base(ptr)
        {
        }

        enum Func {
            ARRAY_FUNC_GET = 0,
            ARRAY_FUNC_SET = 1,
            ARRAY_FUNC_ADDRESS = 2,
            ARRAY_FUNC_CTOR = 3, // Anything >= ARRAY_FUNC_CTOR is .ctor
        };

        public uint GetArrayFuncIndex() {
            // The ru
            var dwSlot = GetSlot();
            var dwVirtuals = GetMethodTable().GetNumVirtuals();
            if (!(dwSlot >= dwVirtuals))
            {
                throw new Exception("WTF");
            }
            //_ASSERTE(dwSlot >= dwVirtuals);
            return dwSlot - dwVirtuals;
        }

        public new uint GetAttrs() {
            return (GetArrayFuncIndex() >= (uint)Func.ARRAY_FUNC_CTOR) ? (uint)(CorMethodAttr.mdPublic | CorMethodAttr.mdRTSpecialName) : (uint)CorMethodAttr.mdPublic;
        }
    }


   [StructLayout(LayoutKind.Sequential)]
   public struct InstantiatedMethodDescFields : IMethodDescFieldsProvider {
        public MethodDescFields BaseFields;
        public RelativePointer m_pDictLayout; //SharedMethodInstantiation  //union with RelativeFixupPointer<PTR_MethodDesc> m_pWrappedMethodDesc; // For WrapperStubWithInstantiations

#if FEATURE_NGEN_RELOCS_OPTIMIZATIONS
        public RelativePointer m_pPerInstInfo;  //SHARED
#else
        public IntPtr m_pPerInstInfo;  //SHARED
#endif
        public ushort m_wFlags2;
        public ushort m_wNumGenericArgs;
        public MethodDescFields GetBaseFields()
        {
            return BaseFields;
        }
    }



    [StructLayout(LayoutKind.Sequential)]
    struct ComPlusCallMethodDescFields : IMethodDescFieldsProvider {
        public MethodDescFields BaseFields;
        public IntPtr m_pComPlusCallInfo;
        public MethodDescFields GetBaseFields()
        {
            return BaseFields;
        }
    }




    [StructLayout(LayoutKind.Sequential)]
    struct DynamicMethodDescFields : IMethodDescFieldsProvider {
        public StoredSigMethodDescFields BaseFields;
        public RelativePointer m_pszMethodName;
        public IntPtr m_pResolver;

#if TARGET_64BIT
        // We use m_dwExtendedFlags from StoredSigMethodDesc on WIN64
        public uint m_dwExtendedFlags;   // see DynamicMethodDesc::ExtendedFlags enum
#endif
        public MethodDescFields GetBaseFields()
        {
            return BaseFields.BaseFields;
        }

       
    }

    class DynamicMethodDesc : MethodDescGeneric<DynamicMethodDescFields> {
        public DynamicMethodDesc(IntPtr ptr) : base(ptr) {
        }

        enum ExtendedFlags : uint {
            nomdAttrs = 0x0000FFFF, // method attributes (LCG)
            nomdILStubAttrs =  (uint)CorMethodAttr.mdMemberAccessMask | (uint)CorMethodAttr.mdStatic, //  method attributes (IL stubs)

            // attributes (except mdStatic and mdMemberAccessMask) have different meaning for IL stubs
            // mdMemberAccessMask     = 0x0007,
            nomdReverseStub = 0x0008,
            // mdStatic               = 0x0010,
            nomdCALLIStub = 0x0020,
            nomdDelegateStub = 0x0040,
            // unused                 = 0x0080
            nomdUnbreakable = 0x0100,
            nomdDelegateCOMStub = 0x0200,  // CLR->COM or COM->CLR call via a delegate (WinRT specific)
            nomdSignatureNeedsRestore = 0x0400,
            nomdStubNeedsCOMStarted = 0x0800,  // EnsureComStarted must be called before executing the method
            nomdMulticastStub = 0x1000,
            nomdUnboxingILStub = 0x2000,
            nomdSecureDelegateStub = 0x4000,

            nomdILStub = 0x00010000,
            nomdLCGMethod = 0x00020000,
            nomdStackArgSize = 0xFFFC0000, // native stack arg size for IL stubs
        };

        public bool IsILStub()
        {
            return  (Fields.m_dwExtendedFlags & (uint)ExtendedFlags.nomdILStub) != 0;
        }

        public new uint GetAttrs() {
            return (IsILStub() ? (Fields.m_dwExtendedFlags & (uint)ExtendedFlags.nomdILStubAttrs) : (Fields.m_dwExtendedFlags & (uint)ExtendedFlags.nomdAttrs));
        }
    }
}
