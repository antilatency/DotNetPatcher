using System;
using System.Collections.Generic;
using System.Text;

namespace DotNetPatcher {

    [Flags]
    public enum CORINFO_ACCESS_FLAGS : ushort {
        CORINFO_ACCESS_ANY = 0x0000, // Normal access
        CORINFO_ACCESS_THIS = 0x0001, // Accessed via the this reference
        CORINFO_ACCESS_UNWRAP = 0x0002, // Accessed via an unwrap reference

        CORINFO_ACCESS_NONNULL = 0x0004, // Instance is guaranteed non-null

        CORINFO_ACCESS_LDFTN = 0x0010, // Accessed via ldftn

        // Field access flags
        CORINFO_ACCESS_GET = 0x0100, // Field get (ldfld)
        CORINFO_ACCESS_SET = 0x0200, // Field set (stfld)
        CORINFO_ACCESS_ADDRESS = 0x0400, // Field address (ldflda)
        CORINFO_ACCESS_INIT_ARRAY = 0x0800, // Field use for InitializeArray
        CORINFO_ACCESS_ATYPICAL_CALLSITE = 0x4000, // Atypical callsite that cannot be disassembled by delay loading helper
        CORINFO_ACCESS_INLINECHECK = 0x8000, // Return fieldFlags and fieldAccessor only. Used by JIT64 during inlining.
    };

    // MethodDef attr bits, Used by DefineMethod.
   [Flags]
   public enum CorMethodAttr {
        // member access mask - Use this mask to retrieve accessibility information.
        mdMemberAccessMask = 0x0007,
        mdPrivateScope = 0x0000,     // Member not referenceable.
        mdPrivate = 0x0001,     // Accessible only by the parent type.
        mdFamANDAssem = 0x0002,     // Accessible by sub-types only in this Assembly.
        mdAssem = 0x0003,     // Accessibly by anyone in the Assembly.
        mdFamily = 0x0004,     // Accessible only by type and sub-types.
        mdFamORAssem = 0x0005,     // Accessibly by sub-types anywhere, plus anyone in assembly.
        mdPublic = 0x0006,     // Accessibly by anyone who has visibility to this scope.
                               // end member access mask

        // method contract attributes.
        mdStatic = 0x0010,     // Defined on type, else per instance.
        mdFinal = 0x0020,     // Method may not be overridden.
        mdVirtual = 0x0040,     // Method virtual.
        mdHideBySig = 0x0080,     // Method hides by name+sig, else just by name.

        // vtable layout mask - Use this mask to retrieve vtable attributes.
        mdVtableLayoutMask = 0x0100,
        mdReuseSlot = 0x0000,     // The default.
        mdNewSlot = 0x0100,     // Method always gets a new slot in the vtable.
                                // end vtable layout mask

        // method implementation attributes.
        mdCheckAccessOnOverride = 0x0200,     // Overridability is the same as the visibility.
        mdAbstract = 0x0400,     // Method does not provide an implementation.
        mdSpecialName = 0x0800,     // Method is special.  Name describes how.

        // interop attributes
        mdPinvokeImpl = 0x2000,     // Implementation is forwarded through pinvoke.
        mdUnmanagedExport = 0x0008,     // Managed method exported via thunk to unmanaged code.

        // Reserved flags for runtime use only.
        mdReservedMask = 0xd000,
        mdRTSpecialName = 0x1000,     // Runtime should check name encoding.
        mdHasSecurity = 0x4000,     // Method has security associate with it.
        mdRequireSecObject = 0x8000,     // Method calls another method containing security code.

    }


   public enum MethodClassification {
        mcIL = 0, // IL
        mcFCall = 1, // FCall (also includes tlbimped ctor, Delegate ctor)
        mcNDirect = 2, // N/Direct
        mcEEImpl = 3, // special method; implementation provided by EE (like Delegate Invoke)
        mcArray = 4, // Array ECall
        mcInstantiated = 5, // Instantiated generic methods, including descriptors
        // for both shared and unshared code (see InstantiatedMethodDesc)

#if FEATURE_COMINTEROP
        mcComInterop = 6,
#endif // FEATURE_COMINTEROP
        mcDynamic = 7, // for method desc with no metadata behind
        mcCount,
    };


    [Flags]
    enum CorTypeAttr : uint {
        // Use this mask to retrieve the type visibility information.
        tdVisibilityMask = 0x00000007,
        tdNotPublic = 0x00000000,     // Class is not public scope.
        tdPublic = 0x00000001,     // Class is public scope.
        tdNestedPublic = 0x00000002,     // Class is nested with public visibility.
        tdNestedPrivate = 0x00000003,     // Class is nested with private visibility.
        tdNestedFamily = 0x00000004,     // Class is nested with family visibility.
        tdNestedAssembly = 0x00000005,     // Class is nested with assembly visibility.
        tdNestedFamANDAssem = 0x00000006,     // Class is nested with family and assembly visibility.
        tdNestedFamORAssem = 0x00000007,     // Class is nested with family or assembly visibility.

        // Use this mask to retrieve class layout information
        tdLayoutMask = 0x00000018,
        tdAutoLayout = 0x00000000,     // Class fields are auto-laid out
        tdSequentialLayout = 0x00000008,     // Class fields are laid out sequentially
        tdExplicitLayout = 0x00000010,     // Layout is supplied explicitly
                                           // end layout mask

        // Use this mask to retrieve class semantics information.
        tdClassSemanticsMask = 0x00000020,
        tdClass = 0x00000000,     // Type is a class.
        tdInterface = 0x00000020,     // Type is an interface.
                                      // end semantics mask

        // Special semantics in addition to class semantics.
        tdAbstract = 0x00000080,     // Class is abstract
        tdSealed = 0x00000100,     // Class is concrete and may not be extended
        tdSpecialName = 0x00000400,     // Class name is special.  Name describes how.

        // Implementation attributes.
        tdImport = 0x00001000,     // Class / interface is imported
        tdSerializable = 0x00002000,     // The class is Serializable.
        tdWindowsRuntime = 0x00004000,     // The type is a Windows Runtime type

        // Use tdStringFormatMask to retrieve string information for native interop
        tdStringFormatMask = 0x00030000,
        tdAnsiClass = 0x00000000,     // LPTSTR is interpreted as ANSI in this class
        tdUnicodeClass = 0x00010000,     // LPTSTR is interpreted as UNICODE
        tdAutoClass = 0x00020000,     // LPTSTR is interpreted automatically
        tdCustomFormatClass = 0x00030000,     // A non-standard encoding specified by CustomFormatMask
        tdCustomFormatMask = 0x00C00000,     // Use this mask to retrieve non-standard encoding information for native interop. The meaning of the values of these 2 bits is unspecified.

        // end string format mask

        tdBeforeFieldInit = 0x00100000,     // Initialize the class any time before first static field access.
        tdForwarder = 0x00200000,     // This ExportedType is a type forwarder.

        // Flags reserved for runtime use.
        tdReservedMask = 0x00040800,
        tdRTSpecialName = 0x00000800,     // Runtime should check name encoding.
        tdHasSecurity = 0x00040000,     // Class has security associate with it.
    }

    [Flags]
    public enum WFLAGS_HIGH_ENUM : uint {
        // DO NOT use flags that have bits set in the low 2 bytes.
        // These flags are DWORD sized so that our atomic masking
        // operations can operate on the entire 4-byte aligned DWORD
        // instead of the logical non-aligned WORD of flags.  The
        // low WORD of flags is reserved for the component size.

        // The following bits describe mutually exclusive locations of the type
        // in the type hiearchy.
        enum_flag_Category_Mask = 0x000F0000,

        enum_flag_Category_Class = 0x00000000,
        enum_flag_Category_Unused_1 = 0x00010000,
        enum_flag_Category_Unused_2 = 0x00020000,
        enum_flag_Category_Unused_3 = 0x00030000,

        enum_flag_Category_ValueType = 0x00040000,
        enum_flag_Category_ValueType_Mask = 0x000C0000,
        enum_flag_Category_Nullable = 0x00050000, // sub-category of ValueType
        enum_flag_Category_PrimitiveValueType = 0x00060000, // sub-category of ValueType, Enum or primitive value type
        enum_flag_Category_TruePrimitive = 0x00070000, // sub-category of ValueType, Primitive (ELEMENT_TYPE_I, etc.)

        enum_flag_Category_Array = 0x00080000,
        enum_flag_Category_Array_Mask = 0x000C0000,
        // enum_flag_Category_IfArrayThenUnused                 = 0x00010000, // sub-category of Array
        enum_flag_Category_IfArrayThenSzArray = 0x00020000, // sub-category of Array

        enum_flag_Category_Interface = 0x000C0000,
        enum_flag_Category_Unused_4 = 0x000D0000,
        enum_flag_Category_Unused_5 = 0x000E0000,
        enum_flag_Category_Unused_6 = 0x000F0000,

        enum_flag_Category_ElementTypeMask = 0x000E0000, // bits that matter for element type mask


        enum_flag_HasFinalizer = 0x00100000, // instances require finalization

        enum_flag_IDynamicInterfaceCastable = 0x00200000, // class implements IDynamicInterfaceCastable interface

        enum_flag_ICastable = 0x00400000, // class implements ICastable interface

        enum_flag_HasIndirectParent = 0x00800000, // m_pParentMethodTable has double indirection

        enum_flag_ContainsPointers = 0x01000000,

        enum_flag_HasTypeEquivalence = 0x02000000, // can be equivalent to another type

        // enum_flag_unused                   = 0x04000000,

        enum_flag_HasCriticalFinalizer = 0x08000000, // finalizer must be run on Appdomain Unload
        enum_flag_Collectible = 0x10000000,
        enum_flag_ContainsGenericVariables = 0x20000000,   // we cache this flag to help detect these efficiently and
                                                           // to detect this condition when restoring

        enum_flag_ComObject = 0x40000000, // class is a com object

        enum_flag_HasComponentSize = 0x80000000,   // This is set if component size is used for flags.

        // Types that require non-trivial interface cast have this bit set in the category
        enum_flag_NonTrivialInterfaceCast = enum_flag_Category_Array
                                             | enum_flag_ComObject
                                             | enum_flag_ICastable
                                             | enum_flag_IDynamicInterfaceCastable

    }  // enum WFLAGS_HIGH_ENUM

    [Flags]
    enum WFLAGS_LOW_ENUM : uint {
        // AS YOU ADD NEW FLAGS PLEASE CONSIDER WHETHER Generics::NewInstantiation NEEDS
        // TO BE UPDATED IN ORDER TO ENSURE THAT METHODTABLES DUPLICATED FOR GENERIC INSTANTIATIONS
        // CARRY THE CORECT FLAGS.
        //

        // We are overloading the low 2 bytes of m_dwFlags to be a component size for Strings
        // and Arrays and some set of flags which we can be assured are of a specified state
        // for Strings / Arrays, currently these will be a bunch of generics flags which don't
        // apply to Strings / Arrays.

        enum_flag_UNUSED_ComponentSize_1 = 0x00000001,

        enum_flag_StaticsMask = 0x00000006,
        enum_flag_StaticsMask_NonDynamic = 0x00000000,
        enum_flag_StaticsMask_Dynamic = 0x00000002,   // dynamic statics (EnC, reflection.emit)
        enum_flag_StaticsMask_Generics = 0x00000004,   // generics statics
        enum_flag_StaticsMask_CrossModuleGenerics = 0x00000006, // cross module generics statics (NGen)
        enum_flag_StaticsMask_IfGenericsThenCrossModule = 0x00000002, // helper constant to get rid of unnecessary check

        enum_flag_NotInPZM = 0x00000008,   // True if this type is not in its PreferredZapModule

        enum_flag_GenericsMask = 0x00000030,
        enum_flag_GenericsMask_NonGeneric = 0x00000000,   // no instantiation
        enum_flag_GenericsMask_GenericInst = 0x00000010,   // regular instantiation, e.g. List<String>
        enum_flag_GenericsMask_SharedInst = 0x00000020,   // shared instantiation, e.g. List<__Canon> or List<MyValueType<__Canon>>
        enum_flag_GenericsMask_TypicalInst = 0x00000030,   // the type instantiated at its formal parameters, e.g. List<T>

        enum_flag_HasVariance = 0x00000100,   // This is an instantiated type some of whose type parameters are co- or contra-variant

        enum_flag_HasDefaultCtor = 0x00000200,
        enum_flag_HasPreciseInitCctors = 0x00000400,   // Do we need to run class constructors at allocation time? (Not perf important, could be moved to EEClass

        enum_flag_IsByRefLike = 0x00001000,

        // In a perfect world we would fill these flags using other flags that we already have
        // which have a constant value for something which has a component size.
        enum_flag_UNUSED_ComponentSize_5 = 0x00002000,
        enum_flag_UNUSED_ComponentSize_6 = 0x00004000,
        enum_flag_UNUSED_ComponentSize_7 = 0x00008000,


        // IMPORTANT! IMPORTANT! IMPORTANT!
        //
        // As you change the flags in WFLAGS_LOW_ENUM you also need to change this
        // to be up to date to reflect the default values of those flags for the
        // case where this MethodTable is for a String or Array
        enum_flag_StringArrayValues = enum_flag_StaticsMask_NonDynamic |
                                      enum_flag_GenericsMask_NonGeneric


    }

    enum LowBits {
        UNION_EECLASS = 0,    //  0 - pointer to EEClass. This MethodTable is the canonical method table.
        UNION_INVALID = 1,    //  1 - not used
        UNION_METHODTABLE = 2,    //  2 - pointer to canonical MethodTable.
        UNION_INDIRECTION = 3     //  3 - pointer to indirection cell that points to canonical MethodTable.
    }

    [Flags]
    enum VmFlags : uint {
#if FEATURE_READYTORUN
        VMFLAG_LAYOUT_DEPENDS_ON_OTHER_MODULES = 0x00000001,
#endif
        VMFLAG_DELEGATE = 0x00000002,

        // VMFLAG_UNUSED                       = 0x0000001c,

        VMFLAG_FIXED_ADDRESS_VT_STATICS = 0x00000020, // Value type Statics in this class will be pinned
        VMFLAG_HASLAYOUT = 0x00000040,
        VMFLAG_ISNESTED = 0x00000080,

        VMFLAG_IS_EQUIVALENT_TYPE = 0x00000200,

        //   OVERLAYED is used to detect whether Equals can safely optimize to a bit-compare across the structure.
        VMFLAG_HASOVERLAYEDFIELDS = 0x00000400,

        // Set this if this class or its parent have instance fields which
        // must be explicitly inited in a constructor (e.g. pointers of any
        // kind, gc or native).
        //
        // Currently this is used by the verifier when verifying value classes
        // - it's ok to use uninitialised value classes if there are no
        // pointer fields in them.
        VMFLAG_HAS_FIELDS_WHICH_MUST_BE_INITED = 0x00000800,

        VMFLAG_UNSAFEVALUETYPE = 0x00001000,

        VMFLAG_BESTFITMAPPING_INITED = 0x00002000, // VMFLAG_BESTFITMAPPING and VMFLAG_THROWONUNMAPPABLECHAR are valid only if this is set
        VMFLAG_BESTFITMAPPING = 0x00004000, // BestFitMappingAttribute.Value
        VMFLAG_THROWONUNMAPPABLECHAR = 0x00008000, // BestFitMappingAttribute.ThrowOnUnmappableChar

        // unused                              = 0x00010000,
        VMFLAG_NO_GUID = 0x00020000,
        VMFLAG_HASNONPUBLICFIELDS = 0x00040000,
        // unused                              = 0x00080000,
        VMFLAG_CONTAINS_STACK_PTR = 0x00100000,
        VMFLAG_PREFER_ALIGN8 = 0x00200000, // Would like to have 8-byte alignment
        VMFLAG_ONLY_ABSTRACT_METHODS = 0x00400000, // Type only contains abstract methods

#if FEATURE_COMINTEROP
        VMFLAG_SPARSE_FOR_COMINTEROP = 0x00800000,
        // interfaces may have a coclass attribute
        VMFLAG_HASCOCLASSATTRIB = 0x01000000,
        VMFLAG_COMEVENTITFMASK = 0x02000000, // class is a special COM event interface
#endif // FEATURE_COMINTEROP
        VMFLAG_VTABLEMETHODIMPL = 0x04000000, // class uses MethodImpl to override virtual function defined on class
        VMFLAG_COVARIANTOVERRIDE = 0x08000000, // class has a covariant override

        // This one indicates that the fields of the valuetype are
        // not tightly packed and is used to check whether we can
        // do bit-equality on value types to implement ValueType::Equals.
        // It is not valid for classes, and only matters if ContainsPointer
        // is false.
        VMFLAG_NOT_TIGHTLY_PACKED = 0x10000000,

        // True if methoddesc on this class have any real (non-interface) methodimpls
        VMFLAG_CONTAINS_METHODIMPLS = 0x20000000,

#if FEATURE_COMINTEROP
        VMFLAG_MARSHALINGTYPE_MASK = 0xc0000000,

        VMFLAG_MARSHALINGTYPE_INHIBIT = 0x40000000,
        VMFLAG_MARSHALINGTYPE_FREETHREADED = 0x80000000,
        VMFLAG_MARSHALINGTYPE_STANDARD = 0xc0000000,
#endif
    }
}
