using UnityEngine;
using UnityEngine.InputSystem;

namespace MyGame
{
    public class TPSCameraEntity : Entity
    {
        private const float MouseReferenceDeltaTime = 1f / 60f;
        private const float MaxGamepadLookDeltaTime = 1f / 30f;
        private const float MaxLookRotationPerFrame = 20f;

        [Header("References")]
        [SerializeField] private Transform followTarget;
        [SerializeField] private Transform cameraTransform;

        [Header("Orbit")]
        [SerializeField] private float yawSpeed = 180f;
        [SerializeField] private float pitchSpeed = 140f;
        [SerializeField] private float pitchMin = -35f;
        [SerializeField] private float pitchMax = 70f;

        [Header("Distance")]
        [SerializeField] private float distance = 8f;
        [SerializeField] private float height = 3.2f;

        [Header("Machine Camera")]
        [SerializeField] private float heavyGroundHeightOffset = 1.2f;

        [Header("Ground Collision")]
        [SerializeField] private bool preventGroundPenetration = true;
        [SerializeField] private float minCameraWorldY = 0.35f;
        [SerializeField] private float groundClearance = 0.35f;
        [SerializeField] private float maxGroundSurfaceY = 1.2f;
        [SerializeField] private float groundProbeHeight = 6f;
        [SerializeField] private float groundProbeDistance = 40f;
        [SerializeField] private LayerMask groundCollisionMask = ~0;

        [Header("Air Mode Distance")]
        [SerializeField] private float airDistance = 6.0f;
        [SerializeField, Tooltip("飛行時のカメラ旋回中心を固定するワールドY座標")]
        private float airHeight = 14.0f;
        [SerializeField] private Vector3 airFollowOffset = new Vector3(0f, 10f, 0f);
        [SerializeField] private Vector3 airPivotOffset = Vector3.zero;

        [Header("Smoothing")]
        [SerializeField] private float followLerp = 14f;

        [Header("Option")]
        [SerializeField] private float defaultFov = 90f;

        [Header("Screen Shake")]
        [SerializeField] private float shakeFrequency = 24f;
        [SerializeField] private float maxShakePosition = 0.35f;
        [SerializeField] private float maxShakeRotation = 2.5f;

        private Vector2 lookInput;
        private float yaw;
        private float pitch;
        private bool m_canControl = true;
        private PlayerEntity m_playerEntity;
        private Camera m_camera;
        private float m_sensitivity = 1f;
        private float m_stickSensitivity = 1f;
        private bool m_invertY;
        private float m_shakeRemaining;
        private float m_shakeDuration;
        private float m_shakeIntensity;
        private float m_shakeSeed;
        private readonly RaycastHit[] m_groundHits = new RaycastHit[12];

        public override void InitEntity()
        {
            if (cameraTransform == null && Camera.main != null)
                cameraTransform = Camera.main.transform;

            CacheCamera();
            RefreshOptionSettings();

            if (followTarget != null)
            {
                CachePlayerEntity();

                Vector3 euler = transform.rotation.eulerAngles;
                yaw = euler.y;
                pitch = euler.x;
                transform.position = followTarget.position;
            }
        }

        public override void LateUpdateEntity()
        {
            if (BattlePauseUIController.IsPaused)
            {
                lookInput = Vector2.zero;
                ClearScreenShake();
                return;
            }

            if (!m_canControl) return;
            if (followTarget == null || cameraTransform == null) return;

            CachePlayerEntity();
            if (m_playerEntity != null && m_playerEntity.IsRespawning)
            {
                lookInput = Vector2.zero;
                return;
            }

            RefreshOptionSettings();

            float dt = Time.deltaTime;
            bool isGamepadLook = IsGamepadLookActive();
            Vector2 frameLookInput = isGamepadLook
                ? Gamepad.current.rightStick.ReadValue()
                : lookInput;
            float activeSensitivity = isGamepadLook
                ? m_stickSensitivity
                : m_sensitivity;
            float lookDeltaTime = isGamepadLook
                ? Mathf.Min(dt, MaxGamepadLookDeltaTime)
                : MouseReferenceDeltaTime;

            float pitchDirection = m_invertY ? 1f : -1f;
            float yawDelta =
                frameLookInput.x *
                yawSpeed *
                activeSensitivity *
                lookDeltaTime;
            float pitchDelta =
                frameLookInput.y *
                pitchSpeed *
                activeSensitivity *
                pitchDirection *
                lookDeltaTime;

            yaw += Mathf.Clamp(
                yawDelta,
                -MaxLookRotationPerFrame,
                MaxLookRotationPerFrame);
            pitch += Mathf.Clamp(
                pitchDelta,
                -MaxLookRotationPerFrame,
                MaxLookRotationPerFrame);
            pitch = Mathf.Clamp(pitch, pitchMin, pitchMax);

            if (!isGamepadLook)
                lookInput = Vector2.zero;

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            bool isAirMode = m_playerEntity != null && m_playerEntity.IsAirMode;
            float airTransition = isAirMode
                ? m_playerEntity.AirTakeoffVisualProgress
                : 0f;
            float currentDistance = Mathf.Lerp(distance, airDistance, airTransition);

            Vector3 targetPos = followTarget.position;
            transform.position = Vector3.Lerp(
                transform.position,
                targetPos,
                1f - Mathf.Exp(-followLerp * dt)
            );

            Vector3 groundPivot =
                transform.position + Vector3.up * GetGroundCameraHeight();
            Vector3 pivot = groundPivot;

            if (isAirMode)
            {
                Vector3 airPivot = transform.position;
                airPivot += airFollowOffset;
                airPivot += transform.TransformDirection(airPivotOffset);
                airPivot.y = airHeight;
                pivot = Vector3.Lerp(groundPivot, airPivot, airTransition);
            }

            Vector3 camPos = pivot - rotation * Vector3.forward * currentDistance;
            camPos = PreventGroundPenetration(pivot, camPos);

            ApplyScreenShake(camPos, rotation, dt);
        }

        public void SetLocalControl(bool canControl)
        {
            m_canControl = canControl;

            if (!m_canControl)
            {
                lookInput = Vector2.zero;
                ClearScreenShake();
            }
        }

        public void SetFollowTarget(Transform target)
        {
            followTarget = target;
            CachePlayerEntity();
        }

        public void SetCameraTransform(Transform cam)
        {
            cameraTransform = cam;
            CacheCamera();
            RefreshOptionSettings();
        }

        public void ApplyOptionSettings()
        {
            RefreshOptionSettings();
            if (!OptionManager.IsCameraShakeEnabled)
                ClearScreenShake();
        }

        public void RequestScreenShake(float intensity, float duration)
        {
            if (BattlePauseUIController.IsPaused ||
                !m_canControl ||
                !OptionManager.IsCameraShakeEnabled)
                return;

            float safeIntensity = Mathf.Clamp01(intensity);
            float safeDuration = Mathf.Max(0f, duration);
            if (safeIntensity <= 0f || safeDuration <= 0f)
                return;

            m_shakeIntensity = Mathf.Max(m_shakeIntensity, safeIntensity);
            m_shakeDuration = Mathf.Max(m_shakeDuration, safeDuration);
            m_shakeRemaining = Mathf.Max(m_shakeRemaining, safeDuration);
            m_shakeSeed = Random.value * 1000f;
        }

        private void CachePlayerEntity()
        {
            m_playerEntity = GetComponentInParent<PlayerEntity>();

            if (m_playerEntity == null && followTarget != null)
            {
                m_playerEntity = followTarget.GetComponentInParent<PlayerEntity>();
            }
        }

        private void CacheCamera()
        {
            m_camera = cameraTransform != null ? cameraTransform.GetComponent<Camera>() : null;
        }

        private void RefreshOptionSettings()
        {
            m_sensitivity = Mathf.Max(0.01f, OptionManager.GetSensitivity());
            m_stickSensitivity = Mathf.Max(0.01f, OptionManager.GetStickSensitivity());
            m_invertY = OptionManager.IsInvertYEnabled;

            if (m_camera != null)
                m_camera.fieldOfView = PlayerPrefs.GetFloat(OptionManager.FovKey, defaultFov);
        }

        private float GetGroundCameraHeight()
        {
            float result = height;
            if (m_playerEntity != null &&
                m_playerEntity.MachineProfile.Type == MachineType.Heavy)
            {
                result += Mathf.Max(0f, heavyGroundHeightOffset);
            }

            return result;
        }

        private static bool IsGamepadLookActive()
        {
            if (Gamepad.current == null)
                return false;

            return Gamepad.current.rightStick.ReadValue().sqrMagnitude > 0.0001f;
        }

        private void ApplyScreenShake(Vector3 basePosition, Quaternion baseRotation, float deltaTime)
        {
            if (!OptionManager.IsCameraShakeEnabled)
            {
                ClearScreenShake();
                cameraTransform.SetPositionAndRotation(basePosition, baseRotation);
                return;
            }

            if (m_shakeRemaining <= 0f || m_shakeDuration <= 0f)
            {
                ClearScreenShake();
                cameraTransform.SetPositionAndRotation(basePosition, baseRotation);
                return;
            }

            m_shakeRemaining = Mathf.Max(0f, m_shakeRemaining - deltaTime);
            float fade = Mathf.Clamp01(m_shakeRemaining / m_shakeDuration);
            float strength = m_shakeIntensity * fade * fade;
            float sampleTime = Time.unscaledTime * Mathf.Max(1f, shakeFrequency);

            Vector3 positionNoise = new Vector3(
                SampleShakeNoise(sampleTime, 0f),
                SampleShakeNoise(sampleTime, 17f),
                SampleShakeNoise(sampleTime, 31f)
            );
            Vector3 rotationNoise = new Vector3(
                SampleShakeNoise(sampleTime, 47f),
                SampleShakeNoise(sampleTime, 59f),
                SampleShakeNoise(sampleTime, 71f)
            );

            Vector3 worldOffset =
                baseRotation *
                (positionNoise * maxShakePosition * strength);
            Quaternion rotationOffset = Quaternion.Euler(
                rotationNoise * maxShakeRotation * strength);

            cameraTransform.SetPositionAndRotation(
                basePosition + worldOffset,
                baseRotation * rotationOffset);
        }

        private float SampleShakeNoise(float sampleTime, float offset)
        {
            return Mathf.PerlinNoise(m_shakeSeed + offset, sampleTime) * 2f - 1f;
        }

        private void ClearScreenShake()
        {
            m_shakeRemaining = 0f;
            m_shakeDuration = 0f;
            m_shakeIntensity = 0f;
        }

        private Vector3 PreventGroundPenetration(Vector3 pivot, Vector3 desiredPosition)
        {
            if (!preventGroundPenetration)
                return desiredPosition;

            float minY = minCameraWorldY;
            if (TryFindGroundHeight(pivot, desiredPosition, out float groundY))
            {
                minY = Mathf.Max(minY, groundY + Mathf.Max(0f, groundClearance));
            }

            if (desiredPosition.y < minY)
            {
                desiredPosition.y = minY;
            }

            return desiredPosition;
        }

        private bool TryFindGroundHeight(
            Vector3 pivot,
            Vector3 desiredPosition,
            out float groundY)
        {
            float probeTopY =
                Mathf.Max(pivot.y, desiredPosition.y, minCameraWorldY) +
                Mathf.Max(0.1f, groundProbeHeight);
            Vector3 origin = new Vector3(desiredPosition.x, probeTopY, desiredPosition.z);
            float distanceToCheck =
                Mathf.Max(0.1f, groundProbeDistance) +
                Mathf.Max(0f, probeTopY - desiredPosition.y);

            int hitCount = Physics.RaycastNonAlloc(
                origin,
                Vector3.down,
                m_groundHits,
                distanceToCheck,
                groundCollisionMask,
                QueryTriggerInteraction.Ignore);

            bool found = false;
            groundY = float.NegativeInfinity;

            for (int i = 0; i < hitCount; i++)
            {
                Collider hitCollider = m_groundHits[i].collider;
                if (hitCollider == null)
                    continue;

                if (m_playerEntity != null &&
                    hitCollider.transform.IsChildOf(m_playerEntity.transform))
                    continue;

                float hitY = m_groundHits[i].point.y;
                if (hitY > maxGroundSurfaceY)
                    continue;

                if (!found || hitY > groundY)
                {
                    groundY = hitY;
                    found = true;
                }
            }

            return found;
        }

        public void OnLook(InputValue value)
        {
            if (BattlePauseUIController.IsPaused || !m_canControl) return;
            lookInput = value.Get<Vector2>();
        }

        public void OnLook(InputAction.CallbackContext context)
        {
            if (BattlePauseUIController.IsPaused || !m_canControl) return;
            lookInput = context.ReadValue<Vector2>();
        }
    }
}