using UnityEngine;
using UnityEngine.InputSystem;

namespace BomberRally
{
    /// <summary>
    /// 人間プレイヤーの操作役。キーボードのキー、または指定番号のゲームパッドの南ボタンで打ち返す。
    /// 押した瞬間に倒しているスティック・押している方向キーで打つ方向（前・左・右）が決まる。
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

        public void Initialize(RallyCharacter owner) => this.owner = owner;

        public void OnBombIncoming(float arrivalTime, HitWindow window) { }

        public void ResetState() { }

        private void Update()
        {
            if (owner == null) return;

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboardKey != Key.None && keyboard[keyboardKey].wasPressedThisFrame)
            {
                owner.RequestHit(ReadKeyboardDirection(keyboard));
                return;
            }

            var gamepad = GetGamepad();
            if (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame)
            {
                owner.RequestHit(ReadStickDirection(gamepad.leftStick.ReadValue()));
            }
        }

        private Gamepad GetGamepad()
        {
            if (gamepadIndex < 0 || gamepadIndex >= Gamepad.all.Count) return null;
            return Gamepad.all[gamepadIndex];
        }

        private HitDirection ReadKeyboardDirection(Keyboard keyboard)
        {
            bool IsHeld(Key key) => key != Key.None && keyboard[key].isPressed;

            // 同時押しのときは左右を優先
            bool left = IsHeld(leftKey);
            bool right = IsHeld(rightKey);
            if (left && !right) return HitDirection.Left;
            if (right && !left) return HitDirection.Right;
            if (IsHeld(forwardKey)) return HitDirection.Forward;
            return HitDirection.None;
        }

        private HitDirection ReadStickDirection(Vector2 stick)
        {
            if (stick.magnitude < stickDeadZone) return HitDirection.None;

            // 一番大きく倒している軸で判定。手前に倒したときは方向指定なし
            if (Mathf.Abs(stick.x) >= Mathf.Abs(stick.y))
            {
                return stick.x < 0f ? HitDirection.Left : HitDirection.Right;
            }
            return stick.y > 0f ? HitDirection.Forward : HitDirection.None;
        }
    }
}
