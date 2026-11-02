using UnityEngine;

namespace MyGame
{
    /// <summary>
    /// 大砲型砲台
    /// 現在のPlayerEntityの攻撃処理を移植した通常砲台
    /// </summary>
    public class CannonTurret : TurretBase
    {
        //********************************************************
        // 変数の定義
        //********************************************************

        /// <summary>弾のPrefab</summary>
        private GameObject bulletPrefab;

        /// <summary>弾の発射位置</summary>
        private Transform muzzleTransform;

        /// <summary>発射クールタイム（秒）</summary>
        private float fireCooldown;

        /// <summary>発射クールタイムのタイマー</summary>
        private float fireTimer;

        /// <summary>発射入力フラグ</summary>
        private bool fireQueued;

        /// <summary>発射時に表示するマズルフラッシュ</summary>
        private GameObject muzzleFlashPrefab;

        /// <summary>発射時に表示する煙エフェクト</summary>
        private GameObject smokePrefab;

        //********************************************************
        // コンストラクタ
        //********************************************************

        /// <summary>
        /// 大砲型砲台のコンストラクタ
        /// </summary>
        /// <param name="bulletPrefab">弾のPrefab</param>
        /// <param name="muzzleTransform">弾の発射位置</param>
        /// <param name="fireCooldown">発射クールタイム（秒）</param>
        public CannonTurret(
            GameObject bulletPrefab,
            Transform muzzleTransform,
            float fireCooldown,
            GameObject muzzleFlashPrefab = null,
            GameObject smokePrefab = null)
            : base("cannon", "大砲型")
        {
            this.bulletPrefab = bulletPrefab;
            this.muzzleTransform = muzzleTransform;
            this.fireCooldown = fireCooldown;
            this.fireTimer = fireCooldown;
            this.muzzleFlashPrefab = muzzleFlashPrefab;
            this.smokePrefab = smokePrefab;
        }

        //********************************************************
        // ITurretの実装
        //********************************************************

        /// <summary>
        /// 有効化時の処理
        /// 大砲型は特別な初期化処理なし
        /// </summary>
        public override void OnEquip(PlayerEntity owner) { }

        /// <summary>
        /// 無効化時の処理
        /// 大砲型は特別な後処理なし
        /// </summary>
        public override void OnUnequip(PlayerEntity owner) { }

        /// <summary>
        /// 攻撃処理　毎フレーム呼ばれる
        /// クールタイムを管理しながら弾を発射する
        /// </summary>
        /// <param name="owner">砲台を所有するPlayerEntity</param>
        public override void OnFire(PlayerEntity owner)
        {
            fireTimer += Time.deltaTime;

            if (!fireQueued) return;
            if (fireTimer < GetEffectiveFireCooldown(owner)) return;
            if (bulletPrefab == null || muzzleTransform == null) return;

            if (owner != null &&
                owner.TryRequestNetworkProjectile(
                    NetworkProjectileType.Cannon,
                    muzzleTransform.position,
                    muzzleTransform.rotation))
            {
                SpawnFireEffect(muzzleFlashPrefab);
                SpawnFireEffect(smokePrefab);
                PlayFireSound();
                owner.NotifyWeaponFired(0.2f, 0.1f);
                fireTimer = 0f;
                fireQueued = false;
                return;
            }

            // 弾を生成して発射
            GameObject bulletObject = RuntimeObjectPool.Spawn(
                bulletPrefab,
                muzzleTransform.position,
                muzzleTransform.rotation
            );

            PlayerBulletEntity bullet = bulletObject.GetComponent<PlayerBulletEntity>();
            if (bullet != null)
                bullet.Setup(owner);

            SpawnFireEffect(muzzleFlashPrefab);
            SpawnFireEffect(smokePrefab);
            PlayFireSound();

            if (owner != null)
                owner.NotifyWeaponFired(0.2f, 0.1f);

            fireTimer = 0f;
            fireQueued = false;
        }

        /// <summary>
        /// 発射入力を受け付ける
        /// PlayerEntityの入力イベントから呼び出す
        /// </summary>
        public void QueueFire()
        {
            fireQueued = true;
        }

        public void ReleaseFire()
        {
            fireQueued = false;
        }

        private float GetEffectiveFireCooldown(PlayerEntity owner)
        {
            float attackSpeed = owner != null
                ? Mathf.Max(0.01f, owner.Stats.GetStat(StatKey.FireInterval))
                : 1f;

            return fireCooldown / attackSpeed;
        }

        private void SpawnFireEffect(GameObject effectPrefab)
        {
            if (effectPrefab == null || muzzleTransform == null)
                return;

            RuntimeObjectPool.Spawn(
                effectPrefab,
                muzzleTransform.position,
                muzzleTransform.rotation
            );
        }

        private void PlayFireSound()
        {
            if (muzzleTransform == null)
                return;

            global::AudioManager.Instance?.PlayCueAtPosition(
                global::AudioCue.CannonMedium,
                muzzleTransform.position,
                0.85f);
        }
    }
}
