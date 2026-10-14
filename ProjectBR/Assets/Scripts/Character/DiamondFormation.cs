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
