using System.Runtime.InteropServices;

namespace DotNetPatcher {

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct InvalidPrecode {
        // int3
        public const int Type = 0xCC;
    }
}
