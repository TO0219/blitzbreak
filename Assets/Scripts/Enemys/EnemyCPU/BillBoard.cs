using UnityEngine;

namespace MyGame
{
    public class Billboard : MonoBehaviour
    {
        private Camera m_camera;

        private void LateUpdate()
        {
            Camera targetCamera = ResolveCamera();
            if (targetCamera == null) return;

            transform.LookAt(
                transform.position + targetCamera.transform.rotation * Vector3.forward,
                targetCamera.transform.rotation * Vector3.up);
        }

        private Camera ResolveCamera()
        {
            if (m_camera != null && m_camera.isActiveAndEnabled)
                return m_camera;

            Camera mainCamera = Camera.main;
            if (mainCamera != null && mainCamera.isActiveAndEnabled)
            {
                m_camera = mainCamera;
                return m_camera;
            }

            Camera[] cameras = Camera.allCameras;
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera candidate = cameras[i];
                if (candidate == null ||
                    !candidate.isActiveAndEnabled ||
                    candidate.targetTexture != null)
                {
                    continue;
                }

                m_camera = candidate;
                return m_camera;
            }

            return null;
        }
    }
}
