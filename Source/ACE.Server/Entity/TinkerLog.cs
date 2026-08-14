using System;
using System.Collections.Generic;
using System.Linq;

using ACE.Entity.Enum;

namespace ACE.Server.Entity
{
    public class TinkerLog
    {
        public List<MaterialType> Tinkers;

        public TinkerLog(string csv)
        {
            Tinkers = new List<MaterialType>();

            if (csv == null) return;

            var vals = csv.Split(',');

            foreach (var val in vals)
            {
                if (!Enum.TryParse(val, true, out MaterialType materialType))
                {
                    Console.WriteLine($"Couldn't parse {val}");
                    continue;
                }
                Tinkers.Add(materialType);
            }
        }

        public int NumTinkers(MaterialType type)
        {
            return Tinkers.Count(i => i == type);
        }

        /// <summary>
        /// Removes the last occurrence of materialType from a TinkerLog CSV string.
        /// Returns null if the log becomes empty.
        /// </summary>
        public static string RemoveLast(string csv, MaterialType materialType)
        {
            if (csv == null)
                return null;

            var log = new TinkerLog(csv);

            for (var i = log.Tinkers.Count - 1; i >= 0; i--)
            {
                if (log.Tinkers[i] == materialType)
                {
                    log.Tinkers.RemoveAt(i);
                    break;
                }
            }

            if (log.Tinkers.Count == 0)
                return null;

            return string.Join(",", log.Tinkers.Select(t => ((uint)t).ToString()));
        }
    }
}
