using UnityEngine;

namespace RepoSuperBallEnemy
{
    public sealed class SuperBallVisualMotion : MonoBehaviour
    {
        public Transform VisualRoot;
        public Vector3 BaseLocalPosition;
        public bool Enabled = true;
        public float BobAmplitude = 0.14f;
        public float BobFrequency = 2.4f;
        public float RollDegreesPerSecond = 210.0f;

        private float phase;

        private void Awake()
        {
            if (VisualRoot == null)
            {
                Transform found = transform.Find("SuperBallChromeSphere");
                if (found != null)
                {
                    VisualRoot = found;
                }
            }

            if (VisualRoot != null && BaseLocalPosition == Vector3.zero)
            {
                BaseLocalPosition = VisualRoot.localPosition;
            }

            phase = Random.value * 10.0f;
        }

        private void Update()
        {
            if (!Enabled || VisualRoot == null)
            {
                return;
            }

            float bounce = Mathf.Abs(Mathf.Sin((Time.time + phase) * BobFrequency)) * BobAmplitude;
            VisualRoot.localPosition = BaseLocalPosition + Vector3.up * bounce;
            VisualRoot.Rotate(Vector3.right, RollDegreesPerSecond * Time.deltaTime, Space.Self);
            VisualRoot.Rotate(Vector3.up, RollDegreesPerSecond * 0.35f * Time.deltaTime, Space.World);
        }
    }
}
