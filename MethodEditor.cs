using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace DotNetPatcher {
    public static class MethodEditor {
        public static IntPtr GetNativeCode(MethodInfo method) {
            var methodDesc = new MethodDesc(method.MethodHandle.Value);
            if (methodDesc.HasPrecode()) {
                var precode = methodDesc.GetPrecode();
                if (precode.GetType() == PrecodeType.PRECODE_FIXUP) {
                    var fixupPrecode = precode.AsFixupPrecode();
                    var fixupType = fixupPrecode.Fields.m_type;
                    if (fixupType == FixupPrecodeFields.TypePrestub) {
                        throw new Exception("Failed to get native code pointer. Fixup precode is pointing to PreStub! Metod is not JIT-ed!");
                    } else {
                        return fixupPrecode.GetTarget();
                    }
                } else {
                    throw new Exception("Only Fixup precode is supported for now");
                }
            }
            return methodDesc.GetMethodEntryPoint();
        }

        public static void SetNativeCode(MethodInfo method, IntPtr nativeCodeAbsoluteAddress) {
            var methodDesc = new MethodDesc(method.MethodHandle.Value);
            if (methodDesc.HasPrecode()) {
                var precode = methodDesc.GetPrecode();
                if (precode.GetType() == PrecodeType.PRECODE_FIXUP) {
                    var fixupPrecode = precode.AsFixupPrecode();
                    var fixupType = fixupPrecode.Fields.m_type;
                    if (fixupType == FixupPrecodeFields.TypePrestub) {
                        throw new Exception("Failed to set native code pointer. Fixup precode is pointing to PreStub! Metod is not JIT-ed!");
                    } else {
                        fixupPrecode.SetTargetAbsoluteUnsafe(nativeCodeAbsoluteAddress);
                    }
                } else {
                    throw new Exception("Only Fixup precode is supported for now");
                }
            } else {
                if (methodDesc.HasNonVtableSlot()) {
                    int size = methodDesc.GetBaseSize();
                    var pSlot = methodDesc.NativePtr + size;
                    Marshal.WriteIntPtr(pSlot, nativeCodeAbsoluteAddress);
                } else {
                    var slotPtr = methodDesc.GetMethodTable().GetSlotPtrRaw(methodDesc.GetSlot());
                    Marshal.WriteIntPtr(slotPtr, nativeCodeAbsoluteAddress);
                    //GetMethodTable().GetSlot(GetSlot());

                    ///throw new Exception("SetNativeCode: current pointer location is not supported");
                }
            }
        }

        public static void SwapMethods(MethodInfo methodA, MethodInfo methodB) {

            Console.WriteLine($"SwapMethods: {methodA.DeclaringType.ToString()}::{methodA.ToString()}, {methodB.DeclaringType.FullName}::{methodB.Name}");
            RuntimeHelpers.PrepareMethod(methodA.MethodHandle);
            RuntimeHelpers.PrepareMethod(methodB.MethodHandle);

            var aNativeCode = GetNativeCode(methodA);
            Console.WriteLine($"SwapMethods aPtr: {aNativeCode.ToInt64():X16}");
            var bNativeCode = GetNativeCode(methodB);
            Console.WriteLine($"SwapMethods bPtr: {bNativeCode.ToInt64():X16}");

            Console.WriteLine($"SwapMethods: setting ptr A");
            SetNativeCode(methodA, bNativeCode);
            Console.WriteLine($"SwapMethods: setting ptr B");
            SetNativeCode(methodB, aNativeCode);

            Console.WriteLine($"SwapMethods end");
        }
    }
}
