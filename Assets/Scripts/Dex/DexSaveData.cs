using System;
using System.Collections.Generic;

namespace InsectGame.Dex
{
    [Serializable]
    public class DexSaveData
    {
        public List<DexRecord> records = new List<DexRecord>();
    }
}
