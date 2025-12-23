using System;

namespace ACE.Common
{
    public struct VariantCacheId : IEquatable<VariantCacheId>
    {
        public ushort Landblock;
        public int Variant;

        public VariantCacheId(ushort landblock, int variant)
        {
            Landblock = landblock;
            Variant = variant;
        }

        public override bool Equals(object obj)
        {
            return obj is VariantCacheId cacheKey && Equals(cacheKey);
        }

        public bool Equals(VariantCacheId other)
        {
            return Landblock == other.Landblock && Variant == other.Variant;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + Landblock.GetHashCode();
                hash = hash * 31 + Variant.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(VariantCacheId left, VariantCacheId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(VariantCacheId left, VariantCacheId right)
        {
            return !(left == right);
        }
    }
}
