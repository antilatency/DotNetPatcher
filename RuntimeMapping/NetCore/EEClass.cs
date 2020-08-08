using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace DotNetPatcher {
    public enum EEClassFieldId {
        EEClass_Field_NumInstanceFields = 0,
        EEClass_Field_NumMethods,
        EEClass_Field_NumStaticFields,
        EEClass_Field_NumHandleStatics,
        EEClass_Field_NumBoxedStatics,
        EEClass_Field_NonGCStaticFieldBytes,
        EEClass_Field_NumThreadStaticFields,
        EEClass_Field_NumHandleThreadStatics,
        EEClass_Field_NumBoxedThreadStatics,
        EEClass_Field_NonGCThreadStaticFieldBytes,
        EEClass_Field_NumNonVirtualSlots,
        EEClass_Field_COUNT
    };

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct EEClassFields {
        public IntPtr m_pGuidInfo;
        public RelativePointer m_rpOptionalFields;
        public RelativePointer m_pMethodTable; //deprecated
        public IntPtr m_pFieldDescList;
        public IntPtr m_pChunks;
#if FEATURE_COMINTEROP
        public IntPtr m_ohDelegate;
        public IntPtr m_pccwTemplate; // points to interop data structures used when this type is exposed to COM
#endif
        public uint m_dwAttrClass;
        public uint m_VMFlags;
#if _DEBUG
        public ushort m_wAuxFlags;
#endif
        public byte m_NormType;
        public byte m_fFieldsArePacked; // TRUE iff fields pointed to by GetPackedFields() are in packed state
        public byte m_cbFixedEEClassFields; // Count of bytes of normal fields of this instance (EEClass,
        public byte m_cbBaseSizePadding; // How many bytes of padding are included in BaseSize
    }

    public class EEClass : NativeObject<EEClassFields> {

        public EEClass(IntPtr ptr) : base(ptr){
  
        }

        public MethodDescChunk GetChunks() {
            return new MethodDescChunk(Fields.m_pChunks);
        }

        public bool IsSealed() {
            return (Fields.m_dwAttrClass & (uint)CorTypeAttr.tdSealed) != 0;
        }

        public bool IsAbstract() {
            return (Fields.m_dwAttrClass & (uint)CorTypeAttr.tdAbstract) != 0;
        }

        public bool HasNonPublicFields() {
            return ((VmFlags)Fields.m_VMFlags & VmFlags.VMFLAG_HASNONPUBLICFIELDS) == VmFlags.VMFLAG_HASNONPUBLICFIELDS;
        }

        public bool ContainsMethodImpls() {
            return ((VmFlags)Fields.m_VMFlags & VmFlags.VMFLAG_CONTAINS_METHODIMPLS) == VmFlags.VMFLAG_CONTAINS_METHODIMPLS;
        }

        public PackedFieldsEEClass GetPackedFields() {
            return Marshal.PtrToStructure<PackedFieldsEEClass>(NativePtr + Fields.m_cbFixedEEClassFields);
        }

        public ushort GetNumNonVirtualSlots() {
            return (ushort)GetPackableField(EEClassFieldId.EEClass_Field_NumNonVirtualSlots);
        }

        public uint GetPackableField(EEClassFieldId eField) {
            var fields = GetPackedFields();
            return Fields.m_fFieldsArePacked != 0 ?
                PackedFields.GetPackedField(ref fields, (uint)eField) :
                PackedFields.GetUnpackedField(ref fields, (uint)eField);
        }

        public ushort GetNumMethods() {
            return (ushort)GetPackableField(EEClassFieldId.EEClass_Field_NumMethods);
        }
    }

    public struct PackedFieldsEEClass : IPackedFieldsData {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = (int)EEClassFieldId.EEClass_Field_COUNT)]
        public uint[] fields;
        public uint[] GetFields() {
            return fields;
        }
        public void Clear() {
            fields = new uint[(int)EEClassFieldId.EEClass_Field_COUNT];
        }
    }
}
