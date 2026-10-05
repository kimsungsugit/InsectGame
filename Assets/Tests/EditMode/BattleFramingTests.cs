#if UNITY_EDITOR
using InsectGame.Battle;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    [TestFixture]
    public class BattleFramingTests
    {
        [TestCase(1f, 1f)]
        [TestCase(5f, 3f)]
        [TestCase(0.1f, 6f)]
        public void DuelSpacing_PreservesSilhouetteGap(float allyWidth, float opponentWidth)
        {
            Bounds ally = new Bounds(Vector3.zero, new Vector3(allyWidth, 1f, 1f));
            Bounds enemy = new Bounds(Vector3.zero, new Vector3(opponentWidth, 1f, 1f));
            float half = BattleFraming.DuelHalfSeparation(ally, enemy);
            Assert.GreaterOrEqual(half, 1.55f);
            Assert.GreaterOrEqual(half * 2f - ally.extents.x - enemy.extents.x, 0.899f);
        }
        [TestCase(1.33333f)]
        [TestCase(2.33333f)]
        public void Framing_AsymmetricNotch_KeepsAllCornersInSafeBattleArea(float aspect)
        {
            var go = new GameObject("Safe battle framing");
            try
            {
                Camera cam = go.AddComponent<Camera>();
                cam.aspect = aspect;
                cam.fieldOfView = 40f;
                Rect safe = new Rect(0.08f, 0.035f, 0.9f, 0.94f);
                Bounds bounds = new Bounds(new Vector3(1000f, 1.2f, 1000f), new Vector3(8f, 4f, 5f));
                BattleFraming.Compute(bounds, aspect, cam.fieldOfView, Quaternion.Euler(12f, -12f, 0f), safe,
                    out Vector3 pos, out Vector3 target);
                cam.transform.position = pos;
                cam.transform.LookAt(target);
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3(
                        (i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 v = cam.WorldToViewportPoint(corner);
                    Assert.That(v.x, Is.InRange(Mathf.Lerp(safe.xMin, safe.xMax, 0.079f), Mathf.Lerp(safe.xMin, safe.xMax, 0.921f)));
                    Assert.That(v.y, Is.InRange(Mathf.Lerp(safe.yMin, safe.yMax, 0.399f), Mathf.Lerp(safe.yMin, safe.yMax, 0.841f)));
                }
            }
            finally { Object.DestroyImmediate(go); }
        }

        [TestCase(1.33333f, 1f)]
        [TestCase(1.77778f, 1f)]
        [TestCase(2.33333f, 1f)]
        [TestCase(1.33333f, 2.5f)]
        [TestCase(2.33333f, 2.5f)]
        public void Framing_AllCornersAboveControlsAndBelowHud(float aspect, float size)
        {
            GameObject go = new GameObject("FramingTest");
            try
            {
                Camera cam = go.AddComponent<Camera>();
                cam.aspect = aspect;
                cam.fieldOfView = 60f;
                Bounds bounds = new Bounds(new Vector3(1000f, 0.8f, 1000f), new Vector3(6f, 2f, 3f) * size);
                BattleFraming.Compute(bounds, aspect, cam.fieldOfView, out Vector3 pos, out Vector3 target);
                cam.transform.position = pos;
                cam.transform.LookAt(target);
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3(
                        (i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 viewport = cam.WorldToViewportPoint(corner);
                    Assert.That(viewport.x, Is.InRange(0.079f, 0.921f));
                    Assert.That(viewport.y, Is.InRange(0.339f, 0.771f));
                    Assert.Greater(viewport.z, 0f);
                }
                Assert.Less(cam.WorldToViewportPoint(bounds.center + Vector3.left * size).x,
                    cam.WorldToViewportPoint(bounds.center + Vector3.right * size).x);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
#endif
