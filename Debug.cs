using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace DotNetPatcher {
    public static class Debug {

        public static void Assert(bool condition)
        {
            if (!condition)
            {
                if (Debugger.IsAttached) {
                    Debugger.Break();
                }
                throw new Exception("Assert");
            }
          
        }
    }
}
