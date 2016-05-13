using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace cs
{
    class CSPersistent
    {
        internal object Value { get; private set; }
        internal LinkedListNode<GCHandle> Node;

        internal CSPersistent(object value)
        {
            Value = value;
        }
    }
}
