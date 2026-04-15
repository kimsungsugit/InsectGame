using System;
using UnityEngine;

namespace InsectGame.Dex
{
    [Serializable]
    public class DexRecord
    {
        public string insectId;
        public int discoveredCount;
        public int capturedCount;
        public long firstSeenUnix;

        public DexRecord(string id)
        {
            insectId = id;
            discoveredCount = 0;
            capturedCount = 0;
            firstSeenUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
    }
}
