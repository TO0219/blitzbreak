using UnityEngine;

namespace MyGame
{
    /// <summary>
    /// ガトリング型砲台
    /// 連射速度が速い弾を連続発射する
    /// クールタイム短め・ダメージ小
    /// </summary>
    public class GatlingTurret : TurretBase
    {
        private const float FireLoopRestartPadding = 0.5f;

        //********************************************************
        // 変数の定義
        //********************************************************

        /// <summary>ガトリングの弾のPrefab</summary>
        private GameObject bulletPrefab;

        /// <summary>発射位置リスト</summary>
        private Transform[] muzzleTransforms;

        /// <summary>発射クールタイム（秒）</summary>
        private float fireCooldown;

        /// <summary>発射クールタイムのタイマー</summary>
        private float fireTimer;

        /// <summary>発射入力フラグ</summary>
        private bool fireQueued;

        /// <summary>発射時に表示するマズルフラッシュ</summary>
        private GameObject muzzleFlashPrefab;
        private GameObject fireLoopSoundObject;
        private AudioSource fireLoopAudioSource;
        private Transform fireLoopTarget;

        //********************************************************
        // コンストラクタ
        //********************************************************

        /// <summary>
        /// ガトリング型砲台のコンストラクタ
        /// </summary>
        /// <param name="bulletPrefab">弾のPrefab</param>
        /// <param name="muzzleTransform">発射位置</param>
        /// <param name="fireCooldown">発射クールタイム（秒）</param>
        public GatlingTurret(
            GameObject bulletPrefab,
            Transform[] muzzleTransforms,
            float fireCooldown,
            GameObject muzzleFlashPrefab = null)
            : base("gatling", "ガトリング型")
        {
            this.bulletPrefab = bulletPrefab;
            this.muzzleTransforms = muzzleTransforms;
            this.fireCooldown = fireCooldown;
            this.fireTimer = fireCooldown;
            this.muzzleFlashPrefab = muzzleFlashPrefab;
        }

        //********************************************************
        // ITurretの実装
        //********************************************************

        /// <summary>有効化時の処理</summary>
        public override void OnEquip(PlayerEntity owner)
        {
        }

        /// <summary>無効化時の処理</summary>
        public override void OnUnequip(PlayerEntity owner)
        {
            fireQueued = false;
            StopFireSound();
        }

        /// <summary>
        /// 攻撃処理　毎フレーム呼ばれる
        /// クールタイムを管理しながら連射する
        /// </summary>
        /// <param name="owner">砲台を所有するPlayerEntity</param>
        public override void OnFire(PlayerEntity owner)
        {
            fireTimer += Time.deltaTime;

            if (!fireQueued) return;
            if (bulletPrefab == null) return;
            if (muzzleTransforms == null || muzzleTransforms.Length == 0) return;

            Transform primaryMuzzle = GetPrimaryMuzzle();
            StartFireSound(primaryMuzzle);
            UpdateFireSoundLoop();

            if (fireTimer < GetEffectiveFireCooldown(owner)) return;
            foreach (Transform muzzle in muzzleTransforms)
            {
                if (muzzle == null) continue;

                Quaternion fireRotation = muzzle.rotation;
                if (owner != null &&
                    owner.IsAirMode &&
                    owner.TryGetAirAimRotation(muzzle.position, out Quaternion airAimRotation))
                {
                    fireRotation = airAimRotation;
                }

                if (owner != null &&
                    owner.TryRequestNetworkProjectile(
                        NetworkProjectileType.Gatling,
                        muzzle.position,
                        fireRotation))
                {
                    SpawnMuzzleFlash(muzzle, fireRotation);
                    continue;
                }


                // 弾を生成
                GameObject bulletObject = RuntimeObjectPool.Spawn(
                    bulletPrefab,
                    muzzle.position,
                    fireRotation
                );

                GatlingBulletEntity bullet = bulletObject.GetComponent<GatlingBulletEntity>();
                if (bullet != null)
                {
                    bullet.Setup(owner);
                    bullet.SetLaunchDirection(fireRotation * Vector3.forward);
                }

                SpawnMuzzleFlash(muzzle, fireRotation);
            }

            if (owner != null)
                owner.NotifyWeaponFired(0.08f, 0.06f);

            fireTimer = 0f;
        }

        /// <summary>
        /// 発射入力を受け付ける
        /// </summary>
        public void QueueFire()
        {
            fireQueued = true;
        }

        /// <summary>
        /// 発射入力を解除する
        /// ボタンを離したときに呼び出す
        /// </summary>
        public void ReleaseFire()
        {
            fireQueued = false;
            StopFireSound();
        }

        /// <summary>
        /// 発射位置を設定する
        /// </summary>
        public void SetMuzzleTransforms(Transform[] muzzleTransforms)
        {
            this.muzzleTransforms = muzzleTransforms;
        }

        private float GetEffectiveFireCooldown(PlayerEntity owner)
        {
            float attackSpeed = owner != null
                ? Mathf.Max(0.01f, owner.Stats.GetStat(StatKey.FireInterval))
                : 1f;

            return fireCooldown / attackSpeed;
        }

        private void SpawnMuzzleFlash(Transform muzzle, Quaternion fireRotation)
        {
            if (muzzleFlashPrefab == null || muzzle == null)
                return;

            RuntimeObjectPool.Spawn(
                muzzleFlashPrefab,
                muzzle.position,
                fireRotation
            );
        }

        private Transform GetPrimaryMuzzle()
        {
            if (muzzleTransforms == null)
                return null;

            for (int i = 0; i < muzzleTransforms.Length; i++)
            {
                if (muzzleTransforms[i] != null)
                    return muzzleTransforms[i];
            }

            return null;
        }

        private void StartFireSound(Transform muzzle)
        {
            if (muzzle == null || global::AudioManager.Instance == null)
                return;

            if (fireLoopAudioSource == null)
            {
                fireLoopSoundObject = new GameObject("GatlingFireLoopSE");
                fireLoopAudioSource = fireLoopSoundObject.AddComponent<AudioSource>();
                global::AudioManager.Instance.ConfigureLoopSource(
                    fireLoopAudioSource,
                    global::AudioCue.Gatling,
                    0.55f);
            }

            if (fireLoopAudioSource.clip == null)
                return;

            if (fireLoopTarget != muzzle)
            {
                fireLoopTarget = muzzle;
                fireLoopSoundObject.transform.SetParent(muzzle, false);
                fireLoopSoundObject.transform.localPosition = Vector3.zero;
                fireLoopSoundObject.transform.localRotation = Quaternion.identity;
            }

            if (!fireLoopAudioSource.isPlaying)
                fireLoopAudioSource.Play();
        }

        private void UpdateFireSoundLoop()
        {
            if (fireLoopAudioSource == null ||
                !fireLoopAudioSource.isPlaying ||
                fireLoopAudioSource.clip == null)
            {
                return;
            }

            float restartTime =
                fireLoopAudioSource.clip.length - FireLoopRestartPadding;
            if (restartTime <= 0f)
                return;

            if (fireLoopAudioSource.time >= restartTime)
                fireLoopAudioSource.time = 0f;
        }

        private void StopFireSound()
        {
            if (fireLoopAudioSource != null && fireLoopAudioSource.isPlaying)
                fireLoopAudioSource.Stop();
        }
    }
}
