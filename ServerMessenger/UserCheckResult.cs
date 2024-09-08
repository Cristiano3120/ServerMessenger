using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServerMessenger
{
    internal enum UserCheckResult
    {
        None = 0,
        EmailExists = 1,
        UsernameExists = 2,
        BothExists = 3,
    }
}
