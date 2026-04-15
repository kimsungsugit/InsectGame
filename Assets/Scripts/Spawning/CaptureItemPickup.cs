using InsectGame.Core;
using InsectGame.Data;
using UnityEngine;

namespace InsectGame.Spawning
{
    public class CaptureItemPickup : MonoBehaviour
    {
        private CaptureItemData itemData;
        private PlayerItemInventory inventory;
        private float bobOffset;
        private float lifetime;

        public void Initialize(CaptureItemData data, PlayerItemInventory inv)
        {
            itemData = data;
            inventory = inv;
            bobOffset = Random.value * Mathf.PI * 2f;
            lifetime = 0f;

            MeshFilter mf = gameObject.GetComponent<MeshFilter>();
            if (mf == null) mf = gameObject.AddComponent<MeshFilter>();
            MeshRenderer mr = gameObject.GetComponent<MeshRenderer>();
            if (mr == null) mr = gameObject.AddComponent<MeshRenderer>();

            mf.sharedMesh = CreateDiamondMesh();
            Shader shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            Material mat = new Material(shader);
            mat.color = data.themeColor;
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", data.themeColor * 0.4f);
            }
            mr.sharedMaterial = mat;

            SphereCollider col = gameObject.GetComponent<SphereCollider>();
            if (col == null) col = gameObject.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 1.5f;

            transform.localScale = Vector3.one * 0.6f;
        }

        private void Update()
        {
            lifetime += Time.deltaTime;
            float bob = Mathf.Sin(Time.time * 2f + bobOffset) * 0.15f;
            Vector3 pos = transform.position;
            pos.y = 0.8f + bob;
            transform.position = pos;
            transform.Rotate(Vector3.up, 90f * Time.deltaTime, Space.World);

            if (lifetime > 120f)
                Destroy(gameObject);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other == null || itemData == null || inventory == null) return;
            if (!other.CompareTag("Player")) return;

            inventory.AddItem(itemData.itemId, 1);
            Destroy(gameObject);
        }

        private Mesh CreateDiamondMesh()
        {
            Mesh mesh = new Mesh();
            Vector3[] verts = new Vector3[]
            {
                new Vector3(0, 1, 0),
                new Vector3(0.5f, 0, 0.5f),
                new Vector3(0.5f, 0, -0.5f),
                new Vector3(-0.5f, 0, -0.5f),
                new Vector3(-0.5f, 0, 0.5f),
                new Vector3(0, -0.6f, 0),
            };
            int[] tris = new int[]
            {
                0,1,2, 0,2,3, 0,3,4, 0,4,1,
                5,2,1, 5,3,2, 5,4,3, 5,1,4,
            };
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            return mesh;
        }
    }
}
