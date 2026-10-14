using UnityEngine;
using UnityEngine.InputSystem;

namespace BomberRally
{
    /// <summary>
    /// 人間プレイヤーの操作役。キーボードのキー、または指定番号のゲームパッドの南ボタンで打ち返す。
    /// 方向キー／スティックで「狙い」（前・左・右）を決めておくと、打ち返したときにその方向へ飛ぶ。
    /// 狙いは一度入れれば覚えておき、打つ瞬間までいつでも変えられる。
    /// ローカル対戦で他の人に読まれないよう、狙いは画面に表示しない。
    /// キーをプレイヤーごとに変えれば 1 台のキーボードを複数人で共有できる。
    /// </summary>
    public class HumanHitController : MonoBehaviour, IHitController
    {
        [Header("キーボード")]
        [SerializeField] private Key keyboardKey = Key.Space;
        [SerializeField] private Key forwardKey = Key.UpArrow;
        [SerializeField] private Key leftKey = Key.LeftArrow;
        [SerializeField] private Key rightKey = Key.RightArrow;

        [Header("ゲームパッド")]
        [Tooltip("使うゲームパッドの番号（接続順）。-1 でゲームパッドを使わない")]
        [SerializeField] private int gamepadIndex = -1;
        [Tooltip("スティックをこれ以上倒したら方向入力とみなす")]
        [Range(0.1f, 1f)] [SerializeField] private float stickDeadZone = 0.5f;

        private RallyCharacter owner;

        /// <summary>今の狙い。None は未設定（ランダム）。</summary>
        public HitDirection CurrentAim { get; private set; }

        public void Initialize(RallyCharacter owner) => this.owner = owner;

        public void OnBombIncoming(float arrivalTime, HitWindow window) { }

        public void ResetState() => CurrentAim = HitDirection.None;

        private void Update()
        {
            if (owner == null) return;

            var keyboard = Keyboard.current;
            var gamepad = GetGamepad();

            // 狙いの更新を先に行い、同じフレームで押した方向キーも反映する
            var aim = HitDirection.None;
            if (keyboard != null) aim = ReadKeyboardAim(keyboard);
            if (aim == HitDirection.None && gamepad != null) aim = ReadStickAim(gamepad.leftStick.ReadValue());
            if (aim != HitDirection.None) CurrentAim = aim;

            bool hitPressed =
                (keyboard != null && keyboardKey != Key.None && keyboard[keyboardKey].wasPressedThisFrame) ||
                (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame);
            if (hitPressed) owner.RequestHit(CurrentAim);
        }

        private Gamepad GetGamepad()
        {
            if (gamepadIndex < 0 || gamepadIndex >= Gamepad.all.Count) return null;
            return Gamepad.all[gamepadIndex];
        }

        private HitDirection ReadKeyboardAim(Keyboard keyboard)
        {
            bool Pressed(Key key) => key != Key.None && keyboard[key].wasPressedThisFrame;

            if (Pressed(leftKey)) return HitDirection.Left;
            if (Pressed(rightKey)) return HitDirection.Right;
            if (Pressed(forwardKey)) return HitDirection.Forward;
            return HitDirection.None;
        }

        private HitDirection ReadStickAim(Vector2 stick)
        {
            if (stick.magnitude < stickDeadZone) return HitDirection.None;

            // 一番大きく倒している軸で判定。手前に倒したときは変更しない
            if (Mathf.Abs(stick.x) >= Mathf.Abs(stick.y))
            {
                return stick.x < 0f ? HitDirection.Left : HitDirection.Right;
            }
            return stick.y > 0f ? HitDirection.Forward : HitDirection.None;
        }
    }
}
