using System;
using System.Collections.Generic;

namespace CasusBelli
{
    public class WarRegistry
    {
        private readonly Dictionary<string, WarRecord> _wars
            = new Dictionary<string, WarRecord>();

        public IEnumerable<WarRecord> AllWars
        {
            get { return _wars.Values; }
        }

        public bool TryGetWar(string warId, out WarRecord war)
        {
            war = null;

            if (string.IsNullOrWhiteSpace(warId))
            {
                return false;
            }

            return _wars.TryGetValue(warId, out war);
        }

        public bool AddWar(WarRecord war)
        {
            if (war == null ||
                string.IsNullOrWhiteSpace(war.WarId))
            {
                return false;
            }

            if (_wars.ContainsKey(war.WarId))
            {
                return false;
            }

            _wars.Add(war.WarId, war);
            return true;
        }

        public void Clear()
        {
            _wars.Clear();
        }
    }
}
