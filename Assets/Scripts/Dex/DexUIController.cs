using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace InsectGame.Dex
{
    public class DexUIController : MonoBehaviour
    {
        [SerializeField] private DexController dexController;
        [SerializeField] private Text discoveredText;
        [SerializeField] private Text capturedText;
        [SerializeField] private TMP_Text discoveredTextTmp;
        [SerializeField] private TMP_Text capturedTextTmp;

        private void OnEnable()
        {
            if (dexController != null)
            {
                dexController.DexUpdated += HandleDexUpdated;
            }
        }

        private void OnDisable()
        {
            if (dexController != null)
            {
                dexController.DexUpdated -= HandleDexUpdated;
            }
        }

        private void HandleDexUpdated(DexSaveData data)
        {
            if (data == null)
            {
                return;
            }

            int discovered = data.records.Count;
            int captured = data.records.Sum(record => record.capturedCount);

            if (discoveredText != null)
            {
                discoveredText.text = $"발견: {discovered}";
            }

            if (capturedText != null)
            {
                capturedText.text = $"포획: {captured}";
            }

            if (discoveredTextTmp != null)
            {
                discoveredTextTmp.text = $"발견: {discovered}";
            }

            if (capturedTextTmp != null)
            {
                capturedTextTmp.text = $"포획: {captured}";
            }
        }

        public void AutoWire(DexController dex)
        {
            if (dexController == null || dexController != dex)
            {
                if (dexController != null)
                    dexController.DexUpdated -= HandleDexUpdated;
                dexController = dex;
                if (dexController != null)
                    dexController.DexUpdated += HandleDexUpdated;
            }
        }
    }
}
