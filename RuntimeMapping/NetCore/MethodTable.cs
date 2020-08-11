using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace DotNetPatcher {

    [StructLayout(LayoutKind.Sequential)]
    public struct MethodTableFields {
        public struct GenericsStaticsInfo {
            public IntPtr m_pFieldDescs;
            public UInt64 m_DynamicTypeID;
        }
        public UInt32 m_dwFlags;  //4
        public UInt32 m_BaseSize; //4
        public ushort m_wFlags2; //2
        public ushort m_wToken;  //2
        public ushort m_wNumVirtuals; //2
        public ushort m_wNumInterfaces; //2
#if _DEBUG_
        public IntPtr debug_m_szClassName;
#endif
        public IntPtr m_pParentMethodTable; 
        public IntPtr m_pLoaderModule;
        public IntPtr m_pWriteableData;
        public IntPtr m_pCanonMT; //union with m_pEEClass   
        public IntPtr m_pPerInstInfo; // union with m_ElementTypeHnd m_pMultipurposeSlot1
        public IntPtr m_pInterfaceMap; //union with m_pMultipurposeSlot2
    }

    public class MethodTable : NativeObject<MethodTableFields>
    {
        public const int VTABLE_SLOTS_PER_CHUNK = 8;
        public const int VTABLE_SLOTS_PER_CHUNK_LOG2 = 3;


        [Flags]
        public enum WFLAGS2_ENUM : ushort {
            enum_flag_MultipurposeSlotsMask = 0x001F,
            enum_flag_HasPerInstInfo = 0x0001,
            enum_flag_HasInterfaceMap = 0x0002,
            enum_flag_HasDispatchMapSlot = 0x0004,
            enum_flag_HasNonVirtualSlots = 0x0008,
            enum_flag_HasModuleOverride = 0x0010,

            enum_flag_IsZapped = 0x0020, // This could be fetched from m_pLoaderModule if we run out of flags

            enum_flag_IsPreRestored = 0x0040, // Class does not need restore
            // This flag is set only for NGENed classes (IsZapped is true)

            enum_flag_HasModuleDependencies = 0x0080,

            enum_flag_IsIntrinsicType = 0x0100,

            enum_flag_RequiresDispatchTokenFat = 0x0200,

            enum_flag_HasCctor = 0x0400,
            // enum_flag_unused                 = 0x0800,

#if FEATURE_64BIT_ALIGNMENT
            enum_flag_RequiresAlign8 = 0x1000, // Type requires 8-byte alignment (only set on platforms that require this and don't get it implicitly)
#endif

            enum_flag_HasBoxedRegularStatics = 0x2000, // GetNumBoxedRegularStatics() != 0

            enum_flag_HasSingleNonVirtualSlot = 0x4000,

            enum_flag_DependsOnEquivalentOrForwardedStructs = 0x8000, // Declares methods that have type equivalent or type forwarded structures in their signature

        }; 

        public MethodTable(IntPtr ptr) : base(ptr) { }

        public IntPtr GetLoaderModule()
        {
            return Fields.m_pLoaderModule;
        }

        public bool HasModuleOverride() {
            return GetFlag(WFLAGS2_ENUM.enum_flag_HasModuleOverride) == WFLAGS2_ENUM.enum_flag_HasModuleOverride;
        }

        public MethodTable GetCanonicalMethodTable()
        {

            var addr = Fields.m_pCanonMT;

            if ((addr.ToInt64() & 2) == 0)
                return this;

#if FEATURE_PREJIT
            if ((addr & 1) != 0)
                return PTR_MethodTable(*PTR_TADDR(addr - 3));
#else

            return new MethodTable(addr - 2);
#endif
        }

        public IntPtr GetModule() {


            // Fast path for non-generic non-array case
            if ((Fields.m_dwFlags & ((uint)WFLAGS_HIGH_ENUM.enum_flag_HasComponentSize | (uint)WFLAGS_LOW_ENUM.enum_flag_GenericsMask)) == 0)
                return GetLoaderModule();

            MethodTable pMTForModule = IsArray() ? this : GetCanonicalMethodTable();
            if (!pMTForModule.HasModuleOverride())
                return pMTForModule.GetLoaderModule();

            var pSlot = pMTForModule.GetMultipurposeSlotPtr(WFLAGS2_ENUM.enum_flag_HasModuleOverride, c_ModuleOverrideOffsets);
            return Marshal.ReadIntPtr(pSlot); // RelativeFixupPointer < PTR_Module >::GetValueAtPtr(pSlot);
        }

        public WFLAGS_HIGH_ENUM GetFlag(WFLAGS_HIGH_ENUM flag) {
            return (WFLAGS_HIGH_ENUM)(Fields.m_dwFlags & (uint)flag);
        }

        public bool HasComponentSize() {
            var v = GetFlag(WFLAGS_HIGH_ENUM.enum_flag_HasComponentSize);
            bool result = v == WFLAGS_HIGH_ENUM.enum_flag_HasComponentSize;
            return result;
        }

        public bool IsStringOrArray() {
            return HasComponentSize();
        }

        public IntPtr GetSlotPtr(uint slotNum) {
            Debug.Assert(!IsZapped());
            return GetSlotPtrRaw(slotNum);
        }

        public uint RawGetComponentSize() {
#if BIGENDIAN
        return *((WORD*)&m_dwFlags + 1);
#else 
            return (uint)(Fields.m_dwFlags & 0xffff);
#endif 
        }

        public bool IsString() {
            return HasComponentSize() && !IsArray() && RawGetComponentSize() == 2;
        }

        public bool IsArray() {
            return GetFlag(WFLAGS_HIGH_ENUM.enum_flag_Category_Array_Mask) == WFLAGS_HIGH_ENUM.enum_flag_Category_Array;
        }

        public bool IsInterface() {
            return GetFlag(WFLAGS_HIGH_ENUM.enum_flag_Category_Mask) == WFLAGS_HIGH_ENUM.enum_flag_Category_Interface;
        }

        public IntPtr GetClassPtr() {
            var ptr = Fields.m_pCanonMT;
            var flags = (LowBits)(ptr.ToInt64() & 0x3);

            if (flags == LowBits.UNION_EECLASS) {
                return ptr;
            } else if (flags == LowBits.UNION_METHODTABLE) {
                var parsedMt = new MethodTable(ptr);
                return parsedMt.Fields.m_pCanonMT;
            } else {
                throw new Exception("WTF");
            }
        }

        public EEClass GetClass() {
            return new EEClass(GetClassPtr());
        }

        public ushort GetNumVirtuals() {
            return Fields.m_wNumVirtuals;
        }

        public ushort GetNumNonVirtualSlots() {
            return HasNonVirtualSlots() ? GetClass().GetNumNonVirtualSlots() : (ushort)0;
        }

        public bool HasNonVirtualSlots() {
            return GetFlag(WFLAGS2_ENUM.enum_flag_HasNonVirtualSlots) == WFLAGS2_ENUM.enum_flag_HasNonVirtualSlots;
        }

        public WFLAGS2_ENUM GetFlag(WFLAGS2_ENUM flag) {
            return (WFLAGS2_ENUM)((uint)Fields.m_wFlags2 & (uint)flag);
        }

        public ushort GetNumVtableSlots() {
            return (ushort)(GetNumVirtuals() + GetNumNonVirtualSlots());
        }

        public ushort GetNumMethods() {
            return GetClass().GetNumMethods();
        }

        public bool HasSingleNonVirtualSlot() {
            return GetFlag(WFLAGS2_ENUM.enum_flag_HasSingleNonVirtualSlot) == WFLAGS2_ENUM.enum_flag_HasSingleNonVirtualSlot;
        }

        public bool IsZapped()
        {
#if FEATURE_PREJIT
            return GetFlag(enum_flag_IsZapped);
#else
            return false;
#endif
        }

        public IntPtr GetSlot(uint slotNumber) {
            Debug.Assert(slotNumber < GetNumVtableSlots());

            var pSlot = GetSlotPtrRaw(slotNumber);
            if (slotNumber < GetNumVirtuals())
            {
                return Marshal.ReadIntPtr(pSlot); // VTableIndir2_t::GetValueMaybeNullAtPtr(pSlot);
            } else if (IsZapped() && slotNumber >= GetNumVirtuals()) {
                // Non-virtual slots in NGened images are relative pointers
                return Marshal.ReadIntPtr(pSlot); //RelativePointer < PCODE >::GetValueAtPtr(pSlot);
            }
            return Marshal.ReadIntPtr(pSlot);
        }

        public uint GetIndexOfVtableIndirection(uint slotNum)
        {
            return slotNum >> VTABLE_SLOTS_PER_CHUNK_LOG2;
        }

        public uint GetNumVirtuals_NoLogging() {
            return Fields.m_wNumVirtuals;
        }

        public uint GetNumVtableIndirections(uint wNumVirtuals) {
            //_ASSERTE((1 << VTABLE_SLOTS_PER_CHUNK_LOG2) == VTABLE_SLOTS_PER_CHUNK);
            return (wNumVirtuals + (VTABLE_SLOTS_PER_CHUNK - 1)) >> VTABLE_SLOTS_PER_CHUNK_LOG2;
        }

        public uint GetNumVtableIndirections()
        {
            return GetNumVtableIndirections(GetNumVirtuals_NoLogging());
        }

        public IntPtr GetMultipurposeSlotPtr(WFLAGS2_ENUM flag, byte[] offsets)
        {
            //_ASSERTE(GetFlag(flag));
            int offset = offsets[(int)GetFlag((WFLAGS2_ENUM)((ushort)flag - 1))];
            if (offset >= Marshal.SizeOf<MethodTableFields>())
            {
                offset += (int)GetNumVtableIndirections() * IntPtr.Size; //sizeof(VTableIndir_t);
            }
            return NativePtr + offset;
        }


        struct MultipurposeSlotOffset
        {

            //// This is raw index of the slot assigned on first come first served basis
            //enum { raw = CountBitsAtCompileTime < mask >::value };
            //
            //// This is actual index of the slot. It is equal to raw index except for the case
            //// where the first fixed slot is not used, but the second one is. The first fixed
            //// slot has to be assigned instead of the second one in this case. This assumes that 
            //// there are exactly two fixed slots.
            //enum { index = (((mask & 3) == 2) && (raw == 1)) ? 0 : raw };
            //
            //// Offset of slot
            //enum {
            //    slotOffset = (index == 0) ? offsetof(MethodTable, m_pMultipurposeSlot1) :
            //        (index == 1) ? offsetof(MethodTable, m_pMultipurposeSlot2) :
            //        (sizeof(MethodTable) + index * sizeof(TADDR) - 2 * sizeof(TADDR))
            //};
            //
            //// Size of methodtable with overflow slots. It is used to compute start offset of optional members.
            //enum { totalSize = (slotOffset >= sizeof(MethodTable)) ? slotOffset : sizeof(MethodTable) };

            static int SparseBitcount(int n) {
                int count = 0;
                while (n != 0) {
                    count++;
                    n &= (n - 1);
                }
                return count;
            }

            public static int raw(int mask)
            {
                return SparseBitcount(mask);
            }

            public static int index(int mask)
            {
                return (((mask & 3) == 2) && (raw(mask) == 1)) ? 0 : raw(mask);
            }
            
            public static byte slotOffset(int mask)
            {
                return (byte)((index(mask) == 0) ? Marshal.OffsetOf<MethodTableFields>("m_pPerInstInfo").ToInt64() : //m_pMultipurposeSlot1
                    ((index(mask) == 1) ? Marshal.OffsetOf<MethodTableFields>("m_pInterfaceMap").ToInt64() : //m_pMultipurposeSlot2
                    (Marshal.SizeOf<MethodTableFields>() + index(mask) * IntPtr.Size - 2 * IntPtr.Size)));
            }
        }

        static byte MULTIPURPOSE_SLOT_OFFSET(int mask)
        {
            return MultipurposeSlotOffset.slotOffset(mask);
        }

        static byte[] MULTIPURPOSE_SLOT_OFFSET_1(int mask)
        {
            return new byte[]{ MULTIPURPOSE_SLOT_OFFSET(mask), MULTIPURPOSE_SLOT_OFFSET(mask | 0x01) };
        }

        static byte[] MULTIPURPOSE_SLOT_OFFSET_2(int mask) {
            List<byte> result = new List<byte>();
            result.AddRange(MULTIPURPOSE_SLOT_OFFSET_1(mask));
            result.AddRange(MULTIPURPOSE_SLOT_OFFSET_1(mask | 0x02));
            return result.ToArray();
        }

        static byte[] MULTIPURPOSE_SLOT_OFFSET_3(int mask) {
            List<byte> result = new List<byte>();
            result.AddRange(MULTIPURPOSE_SLOT_OFFSET_2(mask));
            result.AddRange(MULTIPURPOSE_SLOT_OFFSET_2(mask | 0x04));
            return result.ToArray();
        }

        static byte[] MULTIPURPOSE_SLOT_OFFSET_4(int mask) {
            List<byte> result = new List<byte>();
            result.AddRange(MULTIPURPOSE_SLOT_OFFSET_3(mask));
            result.AddRange(MULTIPURPOSE_SLOT_OFFSET_3(mask | 0x08));
            return result.ToArray();
        }

        static byte[] c_NonVirtualSlotsOffsets = MULTIPURPOSE_SLOT_OFFSET_3(0);

        static byte[] c_ModuleOverrideOffsets = MULTIPURPOSE_SLOT_OFFSET_4(0);

        public IntPtr GetNonVirtualSlotsPtr() {
            
            Debug.Assert(GetFlag(WFLAGS2_ENUM.enum_flag_HasNonVirtualSlots) == WFLAGS2_ENUM.enum_flag_HasNonVirtualSlots); 
            return GetMultipurposeSlotPtr(WFLAGS2_ENUM.enum_flag_HasNonVirtualSlots, c_NonVirtualSlotsOffsets);
        }

        public bool HasNonVirtualSlotsArray() {
            return HasNonVirtualSlots() && !HasSingleNonVirtualSlot();
        }

        public IntPtr GetNonVirtualSlotsArray() {
            if (!HasNonVirtualSlotsArray())
            {
                throw new Exception("WTF");
            }

            return Marshal.ReadIntPtr(GetNonVirtualSlotsPtr());
        }

        public IntPtr GetSlotPtrRaw(uint slotNum) {

             if (slotNum < GetNumVirtuals()) {
                 // Virtual slots live in chunks pointed to by vtable indirections
                 /*uint index = GetIndexOfVtableIndirection(slotNum);
                 IntPtr baseAddr = dac_cast<TADDR>(&(GetVtableIndirections()[index]));
                 DPTR(VTableIndir2_t) baseAfterInd = VTableIndir_t::GetValueMaybeNullAtPtr(baseAddr) + GetIndexAfterVtableIndirection(slotNum);
                 return dac_cast<TADDR>(baseAfterInd);*/
                 throw new NotImplementedException();
             } else if (HasSingleNonVirtualSlot()) {
                // Non-virtual slots < GetNumVtableSlots live in a single chunk pointed to by an optional member,
                // except when there is only one in which case it lives in the optional member itself
                //_ASSERTE(slotNum == GetNumVirtuals());
                //return GetNonVirtualSlotsPtr();
                throw new NotImplementedException();
            } else {
                 // Non-virtual slots < GetNumVtableSlots live in a single chunk pointed to by an optional member
                 Debug.Assert(HasNonVirtualSlotsArray());

                 var nvsa = GetNonVirtualSlotsArray();
                 return nvsa + ((((int)slotNum - GetNumVirtuals()) * IntPtr.Size) );
             }
        }
    }
}
