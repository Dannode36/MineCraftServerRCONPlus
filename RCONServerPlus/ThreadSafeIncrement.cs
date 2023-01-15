using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;

namespace RCONServerPlus
{
    internal static class ThreadSafeIncrement
    {
        private static int num = 0;

        internal static int Get()
        {
            unchecked
            {
                if (Interlocked.Increment(ref num) < 0)
                {
                    num = 0;
                }
                return num;
            }
        }
    }
}
