using UnityEngine;

namespace ProjectUI
{
    /// <summary>
    /// Keeps a piece of UI that stands in the world (a world-space canvas, a label over a thing) turned to the camera,
    /// and, if asked, the same size on the screen whatever the distance. A camera made or changed later is found, and
    /// it works on a disabled-then-enabled object without losing its size.
    /// </summary>
    public class WorldUiRotator : MonoBehaviour
    {
        [SerializeField, Tooltip("The camera it turns to; empty = the main camera")] private Camera viewCamera;
        [SerializeField] private WorldUiFacing facing = WorldUiFacing.Screen;
        [SerializeField, Tooltip("Degrees added to the turn")] private Vector3 offset;
        [SerializeField, Tooltip("Off: it keeps the size it was given. On: it keeps its size on the screen, whatever the distance and the camera's field of view")] private bool constantScreenSize;
        [SerializeField, Min(0f), Tooltip("With the size kept on the screen: how much of the screen's height one unit of the scale it was given takes. A canvas given the scale 1 / 1080 and 1 here is drawn like a 1080-high HUD")] private float screenSize = 1f;

        private Vector3 baseScale;
        private bool measured;

        /// <summary>It is on and drawn.</summary>
        public bool Shown => gameObject.activeInHierarchy;

        private void Awake()
        {
            Measure();
        }

        private void Measure()
        {
            if (measured) return;

            measured = true;
            baseScale = transform.localScale;
        }

        /// <summary>Switches the object on or off.</summary>
        public void Show(bool show)
        {
            Measure();
            if (gameObject.activeSelf != show) gameObject.SetActive(show);
        }

        // After the camera has moved this frame.
        private void LateUpdate()
        {
            Camera view = viewCamera != null ? viewCamera : Camera.main;
            if (view == null) return;

            Transform eye = view.transform;
            transform.localScale = constantScreenSize ? baseScale * ScreenScale(view) : baseScale;

            Quaternion turn;
            if (facing == WorldUiFacing.Screen) turn = eye.rotation;
            else
            {
                Vector3 toCamera = eye.position - transform.position;
                if (toCamera.sqrMagnitude < 0.000001f) return;

                turn = Quaternion.LookRotation(facing == WorldUiFacing.Camera ? toCamera : -toCamera, eye.up);
            }

            transform.rotation = turn * Quaternion.Euler(offset);
        }

        // What the given scale is multiplied by for one unit of it to take screenSize of the screen's height: the
        // height the camera sees at this depth, over the scale of what it hangs on.
        private float ScreenScale(Camera view)
        {
            Transform eye = view.transform;
            float depth = Mathf.Max(Vector3.Dot(transform.position - eye.position, eye.forward), view.nearClipPlane);
            float seen = view.orthographic ? view.orthographicSize * 2f : 2f * depth * Mathf.Tan(view.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float parent = transform.parent != null ? Mathf.Max(Mathf.Abs(transform.parent.lossyScale.y), 0.0001f) : 1f;
            return seen * screenSize / parent;
        }
    }
}
