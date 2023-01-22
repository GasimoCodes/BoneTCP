using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoneTCP.Data
{
    internal enum windowStatus
    {
        None,
        Negotiating,
        Ready,
        Transmit,
        Receive,
        CommitFlush
    }
}
