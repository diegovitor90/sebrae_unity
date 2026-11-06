using UnityEngine;

namespace SebraeLab.Missions
{
    [RequireComponent(typeof(BoxCollider))]
    public class MissionRoomZone : MonoBehaviour
    {
        public bool Contains(Vector3 point)
        {
            var box = GetComponent<BoxCollider>();
            var local = transform.InverseTransformPoint(point) - box.center;
            var half = box.size * .5f;
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
        }
        void OnDrawGizmosSelected()
        {
            var box = GetComponent<BoxCollider>();
            Gizmos.color = new Color(.1f, .75f, 1f, .3f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.center, box.size);
        }
    }
}
