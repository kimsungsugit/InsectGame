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

            // 옛 CreateDiamondMesh()는 픽업이 뜰 때마다 new Mesh()를 만들었고 파괴 경로에
            // 회수가 없었다 — 수명 120초 × 반복 스폰만큼 실제로 샜다. 이제 프로세스당 1개다.
            mf.sharedMesh = ProcMeshLibrary.Diamond(0.5f, 1f, 0.6f);
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

        /// <summary>
        /// 머티리얼은 픽업마다 <c>themeColor</c>가 달라 공유할 수 없다 — 인스턴스를 만든 만큼
        /// 회수한다. 이 오브젝트는 수명 120초 만료(<see cref="Update"/>)와 획득
        /// (<see cref="OnTriggerEnter"/>) 두 경로로 파괴되는데, 어느 쪽에도 회수가 없었다.
        ///
        /// <b>메시는 건드리지 않는다</b> — <see cref="ProcMeshLibrary"/>의 프로세스 수명 캐시라
        /// 다른 픽업이 같은 것을 쓰고 있다. 여기서 파괴하면 다음 픽업이 빈 메시로 뜬다.
        /// </summary>
        private void OnDestroy()
        {
            MeshRenderer mr = GetComponent<MeshRenderer>();
            if (mr != null && mr.sharedMaterial != null) Destroy(mr.sharedMaterial);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other == null || itemData == null || inventory == null) return;
            if (!other.CompareTag("Player")) return;

            inventory.AddItem(itemData.itemId, 1);
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(SfxType.ItemPickup);
            Destroy(gameObject);
        }
    }
}
