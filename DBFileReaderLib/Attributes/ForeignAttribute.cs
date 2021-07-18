using System;
using System.Collections.Generic;
using System.Text;

namespace DBFileReaderLib.Attributes
{
    public class ForeignAttribute : Attribute
    {
        public readonly bool IsForeign;

        public ForeignAttribute(bool isForeign) => IsForeign = isForeign;
    }
}
