using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BoneTCP.Data
{
    /// <summary>
    /// Represents status of sliding window
    /// </summary>
    internal enum windowStatus
    {
        INIT, // BEGINNING STATE

        TRANSMIT, // ACTIVELY SENDING
        RECEIVE, // ACTIVELY RECEIVING

        WAIT_NEG,
        WAIT_FLUSH,
        
    }
}
