using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace BomberRally.EditorTools
{
    /// <summary>
    /// ラリー用の Animator Controller（Idle / Hit / Smash / Die / Victory）をアニメーション FBX から作る。
    /// アセット付属のデモ用 Controller は状態が自動で切り替わり続けるため、別に用意する。
    /// </summary>
    public static class RallyAnimatorBuilder
    {
        private const string OutputFolder = "Assets/Animations/Rally";

        public class Spec
        {
            public string ControllerName;
            public string Idle;
            public string Hit;
            public string Smash;
            public string Die;
            public string Victory;
        }

        public static readonly Spec Hero = new Spec
        {
            ControllerName = "HeroRally",
            Idle = "Assets/Animations/Hero/Idle_Battle_SwordAndShiled.fbx",
            Hit = "Assets/Animations/Hero/Attack01_SwordAndShiled.fbx",
            Smash = "Assets/Animations/Hero/Attack04_SwordAndShiled.fbx",
            Die = "Assets/Animations/Hero/Die01_SwordAndShield.fbx",
            Victory = "Assets/Animations/Hero/Victory_Battle_SwordAndShield.fbx",
        };

        public static readonly Spec Slime = new Spec
        {
            ControllerName = "SlimeRally",
            Idle = "Assets/Animations/Slime/IdleBattle_Slime_Anim.fbx",
            Hit = "Assets/Animations/Slime/Attack01_Slime_Anim.fbx",
            Smash = "Assets/Animations/Slime/Attack02_Slime_Anim.fbx",
            Die = "Assets/Animations/Slime/Die_Slime_Anim.fbx",
            Victory = "Assets/Animations/Slime/Victory_Slime_Anim.fbx",
        };

        /// <summary>Controller を作り直して返す。クリップが見つからなければ null。</summary>
        public static AnimatorController Build(Spec spec)
        {
            var clips = new[] { spec.Idle, spec.Hit, spec.Smash, spec.Die, spec.Victory }.Select(LoadClip).ToArray();
            if (clips.Any(c => c == null)) return null;

            if (!AssetDatabase.IsValidFolder(OutputFolder))
            {
                AssetDatabase.CreateFolder("Assets/Animations", "Rally");
            }

            string path = $"{OutputFolder}/{spec.ControllerName}.controller";
            AssetDatabase.DeleteAsset(path);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            var sm = controller.layers[0].stateMachine;

            var idle = sm.AddState("Idle");
            idle.motion = clips[0];
            sm.defaultState = idle;

            AddReturnToIdle(sm.AddState("Hit"), clips[1], idle);
            AddReturnToIdle(sm.AddState("Smash"), clips[2], idle);
            sm.AddState("Die").motion = clips[3];
            sm.AddState("Victory").motion = clips[4];

            AssetDatabase.SaveAssets();
            return controller;
        }

        private static void AddReturnToIdle(AnimatorState state, AnimationClip clip, AnimatorState idle)
        {
            state.motion = clip;
            var t = state.AddTransition(idle);
            t.hasExitTime = true;
            t.exitTime = 0.9f;
            t.duration = 0.1f;
        }

        private static AnimationClip LoadClip(string fbxPath)
        {
            var clip = AssetDatabase.LoadAllAssetsAtPath(fbxPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
            if (clip == null) Debug.LogError($"アニメーションが見つかりません: {fbxPath}");
            return clip;
        }
    }
}
