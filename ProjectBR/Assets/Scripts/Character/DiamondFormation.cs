using UnityEngine;

namespace BomberRally
{
    /// <summary>
    /// 4 体をひし形に配置する。0:手前 1:右 2:奥 3:左。全員中心を向く。
    /// </summary>
    public class DiamondFormation : MonoBehaviour
    {
        [SerializeField] private float radius = 4f;

        private static readonly Vector3[] Directions =
        {
            Vector3.back,
            Vector3.right,
            Vector3.forward,
            Vector3.left,
        };

        public Vector3 GetSlotPosition(int index)
        {
            return transform.position + transform.rotation * Directions[index % Directions.Length] * radius;
        }

        /// <summary>
        /// index の位置から見て direction 側にいる相手の位置番号。
        /// 全員が中央を向いているので、右隣は時計回りに 1 つ先・左隣は 1 つ前・前は向かい。
        /// </summary>
        public static int GetSlotInDirection(int index, HitDirection direction)
        {
            int count = Directions.Length;
            switch (direction)
            {
                case HitDirection.Right: return (index + 1) % count;
                case HitDirection.Forward: return (index + 2) % count;
                case HitDirection.Left: return (index + count - 1) % count;
                default: return -1;
            }
        }

        public void Arrange(RallyCharacter[] characters)
        {
            for (int i = 0; i < characters.Length; i++)
            {
                var c = characters[i];
                if (c == null) continue;
                c.transform.position = GetSlotPosition(i);
                c.SetHome(transform.position);
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.yellow;
            for (int i = 0; i < Directions.Length; i++)
            {
                Gizmos.DrawLine(GetSlotPosition(i), GetSlotPosition(i + 1));
                Gizmos.DrawWireSphere(GetSlotPosition(i), 0.4f);
            }
        }
    }
}
