#if UNITY_EDITOR
using System.Threading.Tasks;
using InsectGame.Battle;
using InsectGame.Data;
using NUnit.Framework;
using UnityEngine;

namespace InsectGame.Tests
{
    /// <summary>
    /// 버프·약화 연출이 <b>맞는 쪽 곤충 위에</b> 뜨는가 — 진짜 <see cref="BattleArenaController"/>로 기술 연출을 돌려
    /// 이펙트 오브젝트가 생긴 자리를 잰다.
    ///
    /// 예전엔 버프 자리를 진영 bool로 다시 골랐는데 상대 쪽 분기도 내 곤충이라, 상대가 자기에게 건 버프의 고리가
    /// 내 곤충 위에 떴다(2026-10-02 기기 보고). 효과 자체는 옳게 상대에게 걸려 있어 숫자로는 안 보이던 결함이다.
    /// </summary>
    [TestFixture]
    public class BattleSupportEffectSideTests
    {
        [TestCase(false, SkillEffectType.BuffAttack, false, "BuffRing_0")]    // 상대의 자기 버프 → 상대 위
        [TestCase(false, SkillEffectType.DefenseBuff, false, "BuffRing_0")]
        [TestCase(true, SkillEffectType.BuffAttack, true, "BuffRing_0")]      // 내 버프 → 내 곤충 위
        [TestCase(false, SkillEffectType.DebuffAttack, true, "DebuffOrb")]    // 상대의 약화 → 내 곤충 위
        [TestCase(true, SkillEffectType.DebuffAttack, false, "DebuffOrb")]    // 내 약화 → 상대 위
        [Timeout(60000)]
        public async Task SupportEffect_AppearsOverTheInsectItAffects(
            bool playerActs, SkillEffectType effectType, bool expectOnPlayer, string effectName)
        {
            var data = ScriptableObject.CreateInstance<InsectData>();
            data.insectId = "beetle_basic";
            data.displayName = "검사용";
            var root = new GameObject("SupportEffectSideTest");
            try
            {
                var arena = root.AddComponent<BattleArenaController>();
                arena.SetupNormalBattle(data, 5, data, 5, false, Vector3.zero, Vector3.right);
                Assert.IsNotNull(arena.PlayerModel);
                Assert.IsNotNull(arena.EnemyModel);
                Vector3 playerPos = arena.PlayerModel.transform.position;
                Vector3 enemyPos = arena.EnemyModel.transform.position;
                Assert.Greater(Vector3.Distance(playerPos, enemyPos), 2f, "두 곤충이 한 자리에 섰다");

                arena.PlaySkillEffect(playerActs, InsectElement.None, effectType, null, false, 0.6f);

                // 프레임 수가 아니라 시간으로 기다린다 — 이펙트는 연출 시간이 타격 시점에 닿아야 생기는데, 배치 러너는 렌더가 없어
                // 600프레임이 0.23초에 끝나 그 직전에 포기하곤 했다(통과 0.25초 · 실패 0.23초, 2026-10-03).
                GameObject effect = null;
                float waitUntil = Time.realtimeSinceStartup + 10f;
                while (effect == null && Time.realtimeSinceStartup < waitUntil)
                {
                    await Task.Yield();
                    effect = GameObject.Find(effectName);
                }
                Assert.IsNotNull(effect, effectName + " 이펙트가 생기지 않았다");

                // 이펙트는 대상의 수직선 위에서 오르내린다 — 수평 거리로 어느 쪽인지 가린다.
                Vector3 at = effect.transform.position;
                float toPlayer = Vector2.Distance(new Vector2(at.x, at.z), new Vector2(playerPos.x, playerPos.z));
                float toEnemy = Vector2.Distance(new Vector2(at.x, at.z), new Vector2(enemyPos.x, enemyPos.z));
                if (expectOnPlayer)
                    Assert.Less(toPlayer, toEnemy, $"이펙트가 상대 쪽에 떴다 (내 곤충까지 {toPlayer:F2}m, 상대까지 {toEnemy:F2}m)");
                else
                    Assert.Less(toEnemy, toPlayer, $"이펙트가 내 곤충 쪽에 떴다 (내 곤충까지 {toPlayer:F2}m, 상대까지 {toEnemy:F2}m)");
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(data);
                // 이펙트·아레나는 아레나 루트 밑이라 CleanupArena(OnDisable)가 함께 치운다. 남았으면 다음 케이스가 그걸 찾는다.
                GameObject leftover = GameObject.Find(effectName);
                if (leftover != null) Object.DestroyImmediate(leftover);
            }
        }
    }
}
#endif
