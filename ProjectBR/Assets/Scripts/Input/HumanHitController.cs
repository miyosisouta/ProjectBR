using UnityEngine;
using UnityEngine.InputSystem;

namespace BomberRally
{
    /// <summary>
    /// 人間プレイヤーの操作役。キーボードのキー、または指定番号のゲームパッドの南ボタンで打ち返す。
    /// キーをプレイヤーごとに変えれば 1 台のキーボードを複数人で共有できる。
    /// </summary>
    public class HumanHitController : MonoBehaviour, IHitController
    {
        [SerializeField] private Key keyboardKey = Key.Space;
        [Tooltip("使うゲームパッドの番号（接続順）。-1 でゲームパッドを使わない")]
        [SerializeField] private int gamepadIndex = -1;

        private RallyCharacter owner;

        public void Initialize(RallyCharacter owner) => this.owner = owner;

        public void OnBombIncoming(float arrivalTime, HitWindow window) { }

        public void ResetState() { }

        private void Update()
        {
            if (owner == null) return;
            if (WasHitPressed()) owner.RequestHit();
        }

        private bool WasHitPressed()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboardKey != Key.None && keyboard[keyboardKey].wasPressedThisFrame) return true;

            if (gamepadIndex >= 0 && gamepadIndex < Gamepad.all.Count)
            {
                if (Gamepad.all[gamepadIndex].buttonSouth.wasPressedThisFrame) return true;
            }
            return false;
        }
    }
}
