using System;
using System.Collections.Generic;
using System.Text;

namespace DotNetPatcher {
    public class NativeObject<T> where T : struct {
        public readonly IntPtr NativePtr;
        public NativeObject(IntPtr ptr) {
            NativePtr = ptr;
        }
        public T Fields {
            get => Memory.ReadStruct<T>(NativePtr);
            set => Memory.WriteStruct(NativePtr, value);
        }
    }
}