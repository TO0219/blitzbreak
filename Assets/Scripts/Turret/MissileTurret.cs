using UnityEngine;

namespace MyGame
{
    /// <summary>
    /// ミサイル型砲台
    /// 追尾ミサイルを発射する
    /// クールタイム長め・ダメージ大
    /// </summary>
    public class MissileTurret : TurretBase
    {
        //********************************************************
        // 変数の定義
        //********************************************************

        /// <summary>ミサイルのPrefab</summary>
        private GameObject missilePrefab;

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

        /// <summary>発射時に表示するミサイル発射煙</summary>
        private GameObject missileLaunchSmokePrefab;

        /// <summary>次に発射する発射位置の番号</summary>
        private int muzzleIndex;

        //********************************************************
        // コンストラクタ
        //********************************************************

        /// <summary>
        /// ミサイル型砲台のコンストラクタ
        /// </summary>
        public MissileTurret(
            GameObject missilePrefab,
            Transform muzzleTransform,
            float fireCooldown,
            GameObject muzzleFlashPrefab = null,
            GameObject missileLaunchSmokePrefab = null)
            : this(missilePrefab, new Transform[] { muzzleTransform }, fireCooldown, muzzleFlashPrefab, missileLaunchSmokePrefab)
        {
        }

        /// <summary>
        /// ミサイル型砲台のコンストラクタ
        /// </summary>
        public MissileTurret(
            GameObject missilePrefab,
            Transform[] muzzleTransforms,
            float fireCooldown,
            GameObject muzzleFlashPrefab = null,
            GameObject missileLaunchSmokePrefab = null)
            : base("missile", "ミサイル型")
        {
            this.missilePrefab = missilePrefab;
            this.muzzleTransforms = muzzleTransforms;
            this.fireCooldown = fireCooldown;
            this.fireTimer = fireCooldown;
            this.muzzleIndex = 0;
            this.muzzleFlashPrefab = muzzleFlashPrefab;
            this.missileLaunchSmokePrefab = missileLaunchSmokePrefab;
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
        }

        /// <summary>
        /// 攻撃処理　毎フレーム呼ばれる
        /// クールタイムを管理しながらミサイルを発射する
        /// </summary>
        public override void OnFire(PlayerEntity owner)
        {
            fireTimer += Time.deltaTime;

            if (!fireQueued) return;
            if (fireTimer < GetEffectiveFireCooldown(owner))
            {
                return;
            }
            if (missilePrefab == null)
            {
                fireQueued = false;
                return;
            }
            if (muzzleTransforms == null || muzzleTransforms.Length == 0)
            {
                fireQueued = false;
                return;
            }

            Transform muzzle = muzzleTransforms[muzzleIndex];

            // 次回は反対側の発射位置を使う
            muzzleIndex++;
            if (muzzleIndex >= muzzleTransforms.Length)
                muzzleIndex = 0;

            if (muzzle == null)
            {
                fireQueued = false;
                return;
            }

            Quaternion fireRotation = muzzle.rotation;
            if (owner != null &&
                owner.TryGetAirAimRotation(muzzle.position, out Quaternion airAimRotation))
            {
                fireRotation = airAimRotation;
            }

            TurretManagerEntity turretManager =
                owner != null ? owner.GetComponent<TurretManagerEntity>() : null;
            ulong targetNetworkObjectId = turretManager != null
                ? turretManager.GetMissileLockTargetNetworkObjectId()
                : ulong.MaxValue;

            if (owner != null &&
                owner.TryRequestNetworkProjectile(
                    NetworkProjectileType.Missile,
                    muzzle.position,
                    fireRotation,
                    targetNetworkObjectId))
            {
                SpawnMuzzleFlash(muzzle, fireRotation);
                SpawnLaunchSmoke(muzzle, fireRotation);
                PlayFireSound(muzzle);
                owner.NotifyWeaponFired(0.42f, 0.18f);
                fireTimer = 0f;
                fireQueued = false;
                return;
            }

            // ミサイルを生成
            GameObject missileObject = RuntimeObjectPool.Spawn(
                missilePrefab,
                muzzle.position,
                fireRotation
            );

            MissileBulletEntity missile = missileObject.GetComponent<MissileBulletEntity>();
            if (missile != null)
            {
                Transform lockTarget = null;
                if (turretManager != null)
                    turretManager.TryGetMissileLockTarget(out lockTarget);

                missile.Setup(owner, lockTarget);
            }

            SpawnMuzzleFlash(muzzle, fireRotation);
            SpawnLaunchSmoke(muzzle, fireRotation);
            PlayFireSound(muzzle);

            if (owner != null)
                owner.NotifyWeaponFired(0.42f, 0.18f);

            fireTimer = 0f;
            fireQueued = false;
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
        /// </summary>
        public void ReleaseFire()
        {
            fireQueued = false;
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

        private void SpawnLaunchSmoke(Transform muzzle, Quaternion fireRotation)
        {
            if (missileLaunchSmokePrefab == null || muzzle == null)
                return;

            RuntimeObjectPool.Spawn(
                missileLaunchSmokePrefab,
                muzzle.position,
                fireRotation
            );
        }

        private void PlayFireSound(Transform muzzle)
        {
            if (muzzle == null)
                return;

            global::AudioManager.Instance?.PlayCueAtPosition(
                global::AudioCue.Missile,
                muzzle.position);
        }
    }
}
