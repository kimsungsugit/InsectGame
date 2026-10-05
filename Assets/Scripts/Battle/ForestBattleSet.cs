using System;
using System.Collections.Generic;
using InsectGame.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace InsectGame.Battle
{
    /// <summary>Deterministic forest clearing, combined by material. No scene/global render settings are changed.</summary>
    public static class ForestBattleSet
    {
        public static void Build(Transform parent, Func<Color, Material> materialFactory, List<Mesh> ownedMeshes)
        {
            Mesh pebble = ProcMeshLibrary.LowSphere(1f, 1f, 1f, 5, 9);
            Mesh crown = ProcMeshLibrary.LowSphere(1f, 1f, 1f, 5, 10);
            Mesh trunk = ProcMeshLibrary.TaperedCapsule(0.13f, 0.23f, 1f, 3, 8);
            var ground = new List<CombineInstance>();
            var clearing = new List<CombineInstance>();
            var moss = new List<CombineInstance>();
            var bark = new List<CombineInstance>();
            var nearLeaves = new List<CombineInstance>();
            var farLeaves = new List<CombineInstance>();
            var stones = new List<CombineInstance>();
            Add(ground, crown, new Vector3(0f, -0.24f, 0f), new Vector3(38f, 0.2f, 38f));
            Add(moss, crown, new Vector3(0f, -0.12f, 0f), new Vector3(8.6f, 0.16f, 6.8f));
            Add(clearing, crown, new Vector3(0f, -0.065f, 0f), new Vector3(6.6f, 0.10f, 4.8f));
            var random = new System.Random(8173);
            for (int i = 0; i < 18; i++)
            {
                // Back and sides only: keep foreground center and the duel axis clear.
                float angle = Mathf.Lerp(-112f, 112f, i / 17f) * Mathf.Deg2Rad;
                float distance = 9f + (float)random.NextDouble() * 7f;
                Vector3 foot = new Vector3(Mathf.Sin(angle) * distance, 0f, Mathf.Cos(angle) * distance);
                float height = 4.4f + (float)random.NextDouble() * 3f;
                Add(bark, trunk, foot + Vector3.up * (height * 0.5f), new Vector3(1.8f, height, 1.8f), (float)random.NextDouble() * 20f);
                var leaves = i % 3 == 0 ? nearLeaves : farLeaves;
                Add(leaves, crown, foot + Vector3.up * height, new Vector3(2.4f, 1.8f, 2.2f));
                Add(leaves, crown, foot + new Vector3(1.1f, height - 0.6f, 0.2f), new Vector3(1.7f, 1.3f, 1.6f));
            }
            for (int side = -1; side <= 1; side += 2)
            for (int i = 0; i < 8; i++)
            {
                Vector3 p = new Vector3(side * (5.5f + (float)random.NextDouble() * 1.9f), 0f, -2.5f + i * 1.2f);
                float size = 0.22f + (float)random.NextDouble() * 0.35f;
                Add(stones, pebble, p + Vector3.up * size * 0.22f, new Vector3(size * 1.8f, size * 0.7f, size), i * 31f);
                Add(nearLeaves, crown, p + new Vector3(side * 0.6f, 0.18f, 0.3f), new Vector3(0.8f, 0.35f, 0.65f));
            }
            Emit(parent, "ForestGround", ground, materialFactory(new Color(0.095f, 0.15f, 0.135f)), ownedMeshes, false);
            Emit(parent, "ForestMoss", moss, materialFactory(new Color(0.19f, 0.27f, 0.19f)), ownedMeshes, false);
            Emit(parent, "ForestClearing", clearing, materialFactory(new Color(0.33f, 0.32f, 0.23f)), ownedMeshes, false);
            Emit(parent, "ForestTrunks", bark, materialFactory(new Color(0.18f, 0.23f, 0.21f)), ownedMeshes, false);
            Emit(parent, "ForestNearLeaves", nearLeaves, materialFactory(new Color(0.16f, 0.29f, 0.23f)), ownedMeshes, false);
            Emit(parent, "ForestFarLeaves", farLeaves, materialFactory(new Color(0.22f, 0.32f, 0.29f)), ownedMeshes, false);
            Emit(parent, "ForestStones", stones, materialFactory(new Color(0.29f, 0.34f, 0.31f)), ownedMeshes, true);
        }

        private static void Add(List<CombineInstance> batch, Mesh mesh, Vector3 position, Vector3 scale, float yaw = 0f)
        {
            batch.Add(new CombineInstance { mesh = mesh, transform = Matrix4x4.TRS(position, Quaternion.Euler(0f, yaw, 0f), scale) });
        }

        private static void Emit(Transform parent, string name, List<CombineInstance> batch, Material material,
            List<Mesh> ownedMeshes, bool castShadows)
        {
            var mesh = new Mesh { name = name };
            mesh.CombineMeshes(batch.ToArray(), true, true);
            ownedMeshes.Add(mesh);
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            // 광택은 여기서 따로 정하지 않는다 — materialFactory(BattleArenaController.CreateSafeMaterial)가
            // 월드 소품과 같은 무광 마감(SceneryMaterials.MatteGloss)을 이미 입혀 온다. 예전의 0.15 사본은 그 전의 흔적이다.
        }
    }
}
