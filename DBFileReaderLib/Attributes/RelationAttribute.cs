﻿using System;

namespace DBFileReaderLib.Attributes
{
    public class RelationAttribute : Attribute
    {
        public readonly Type FieldType;
        public readonly bool IsNonInline;
        public RelationAttribute(Type fieldType, bool isNonInline) => (this.FieldType, this.IsNonInline) = (fieldType, isNonInline);
    }
}
