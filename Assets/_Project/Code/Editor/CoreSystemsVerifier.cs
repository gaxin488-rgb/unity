#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using EchoesOfTheLost.Characters.Player;
using EchoesOfTheLost.Core;
using EchoesOfTheLost.Systems.Quest;
using EchoesOfTheLost.World;

// ============================================================
//  CoreSystemsVerifier — Automated EditMode & Scene integrity
//  verification suite for Echoes of the Lost (Phase 5 QA)
// ============================================================
namespace EchoesOfTheLost.EditorTools
{
    public static class CoreSystemsVerifier
    {
        private class TestPingEvent : IGameEvent
        {
            public int Payload;
        }

        private class DummyState : IState
        {
            public int EnterCount;
            public int UpdateCount;
            public int ExitCount;
            public void Enter() => EnterCount++;
            public void Update() => UpdateCount++;
            public void FixedUpdate() { }
            public void Exit() => ExitCount++;
        }

        [MenuItem("EotL/QA/Run Core Systems Self-Test")]
        public static void RunAllChecks()
        {
            int passed = 0;
            int failed = 0;

            // 1. EventBus pub/sub test
            int receivedPayload = 0;
            System.Action<TestPingEvent> handler = e => receivedPayload = e.Payload;
            EventBus.Subscribe(handler);
            EventBus.Publish(new TestPingEvent { Payload = 42 });
            EventBus.Unsubscribe(handler);
            if (receivedPayload == 42) passed++; else { failed++; Debug.LogError("[QA] EventBus test failed."); }

            // 2. StateMachine transition test
            var sm = new StateMachine();
            var s1 = new DummyState();
            var s2 = new DummyState();
            sm.SetState(s1);
            sm.Update();
            sm.SetState(s2);
            if (s1.EnterCount == 1 && s1.UpdateCount == 1 && s1.ExitCount == 1 && s2.EnterCount == 1)
                passed++;
            else
            {
                failed++;
                Debug.LogError("[QA] StateMachine test failed.");
            }

            // 3. StatValue modifier math test
            var stat = new StatValue { baseValue = 100f };
            stat.AddModifier(25f);
            stat.AddModifier(-10f);
            if (Mathf.Approximately(stat.FinalValue, 115f)) passed++;
            else { failed++; Debug.LogError("[QA] StatValue modifier test failed."); }

            // 4. ScriptableObject assets check
            var quest = AssetDatabase.LoadAssetAtPath<QuestData>("Assets/_Project/Data/ScriptableObjects/Quests/Quest_FirstEcho.asset");
            var biome = AssetDatabase.LoadAssetAtPath<BiomeData>("Assets/_Project/Data/ScriptableObjects/Biomes/Biome_VerdaWilds.asset");
            if (quest != null && biome != null) passed++;
            else { failed++; Debug.LogWarning("[QA] ScriptableObject assets not yet generated — run EotL/Setup/Build Sandbox Scene."); }

            // 5. Active scene objects check
            var player = GameObject.FindWithTag("Player");
            var hud = Object.FindAnyObjectByType<UI.HUDController>();
            var uiMgr = Object.FindAnyObjectByType<UI.UIManager>();
            if (player != null && hud != null && uiMgr != null) passed++;
            else { failed++; Debug.LogWarning("[QA] Scene objects check incomplete."); }

            Debug.Log($"[CoreSystemsVerifier] QA Self-Test Complete: {passed} passed, {failed} failed.");
        }
    }
}
#endif
