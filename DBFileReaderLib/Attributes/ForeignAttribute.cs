using System;

namespace DBFileReaderLib.Attributes
{
    public class ForeignAttribute : Attribute
    {
        public readonly bool IsForeign;

        public ForeignAttribute(bool isForeign) => IsForeign = isForeign;
    }
}
