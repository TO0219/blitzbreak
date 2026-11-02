using System;
using UnityEngine;

namespace MyGame
{
    public class RailgunTurret : TurretBase
    {
        private readonly Transform[] m_muzzleTransforms;
        private readonly float m_fireCooldown;
        private readonly float m_damage;
        private readonly float m_range;
        private readonly GameObject m_muzzleFlashPrefab;
        private float m_fireTimer;
        private bool m_fireQueued;

        public RailgunTurret(
            Transform[] muzzleTransforms,
            float fireCooldown,
            float damage,
            float range,
            GameObject muzzleFlashPrefab = null)
            : base("railgun", "レイルガン")
        {
            m_muzzleTransforms = muzzleTransforms;
            m_fireCooldown = Mathf.Max(0.1f, fireCooldown);
            m_damage = Mathf.Max(1f, damage);
            m_range = Mathf.Max(1f, range);
            m_muzzleFlashPrefab = muzzleFlashPrefab;
            m_fireTimer = m_fireCooldown;
        }

        public override void OnEquip(PlayerEntity owner)
        {
        }

        public override void OnUnequip(PlayerEntity owner)
        {
            m_fireQueued = false;
        }

        public override void OnFire(PlayerEntity owner)
        {
            m_fireTimer += Time.deltaTime;

            if (!m_fireQueued) return;
            if (m_fireTimer < GetEffectiveFireCooldown(owner)) return;

            Transform muzzle = GetPrimaryMuzzle();
            if (muzzle == null) return;

            Vector3 direction = muzzle.forward;
            if (owner != null &&
                owner.IsAirMode &&
                owner.TryGetAirAimRotation(muzzle.position, out Quaternion airAimRotation))
            {
                direction = airAimRotation * Vector3.forward;
            }

            if (owner != null &&
                owner.TryRequestRailgunFire(muzzle.position, direction))
            {
                SpawnMuzzleFlash(muzzle, Quaternion.LookRotation(direction));
                PlayFireSound(muzzle);
                owner.NotifyWeaponFired(0.22f, 0.12f);
                m_fireTimer = 0f;
                m_fireQueued = false;
                return;
            }

            if (TryFireAuthoritative(owner, muzzle.position, direction, out Vector3 endPoint))
            {
                RailgunBeamEffect.Play(muzzle.position, endPoint);
                SpawnMuzzleFlash(muzzle, Quaternion.LookRotation(direction));
                PlayFireSound(muzzle);
                owner?.NotifyWeaponFired(0.22f, 0.12f);
                m_fireTimer = 0f;
                m_fireQueued = false;
            }
        }

        public void QueueFire()
        {
            m_fireQueued = true;
        }

        public void ReleaseFire()
        {
            m_fireQueued = false;
        }

        public bool TryFireAuthoritative(
            PlayerEntity owner,
            Vector3 origin,
            Vector3 direction,
            out Vector3 endPoint)
        {
            return TryFireAuthoritative(
                owner,
                null,
                origin,
                direction,
                out endPoint);
        }

        public bool TryFireAuthoritative(
            EnemyCPUEntity cpuOwner,
            Vector3 origin,
            Vector3 direction,
            out Vector3 endPoint)
        {
            return TryFireAuthoritative(
                null,
                cpuOwner,
                origin,
                direction,
                out endPoint);
        }

        private bool TryFireAuthoritative(
            PlayerEntity owner,
            EnemyCPUEntity cpuOwner,
            Vector3 origin,
            Vector3 direction,
            out Vector3 endPoint)
        {
            if (direction.sqrMagnitude < 0.001f)
            {
                endPoint = origin;
                return false;
            }

            direction.Normalize();
            endPoint = origin + direction * m_range;

            RaycastHit[] hits = Physics.RaycastAll(
                origin,
                direction,
                m_range,
                ~0,
                QueryTriggerInteraction.Collide);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            float damage = GetEffectiveDamage(owner, cpuOwner);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider hitCollider = hits[i].collider;
                if (hitCollider == null)
                    continue;

                if (ShouldIgnoreHit(hitCollider, owner, cpuOwner))
                    continue;

                endPoint = hits[i].point;
                ApplyDamage(hitCollider, damage, owner, cpuOwner, endPoint);
                return true;
            }

            return true;
        }

        private bool ShouldIgnoreHit(
            Collider hitCollider,
            PlayerEntity owner,
            EnemyCPUEntity cpuOwner)
        {
            if (ProjectileSweepUtility.IsWorldBorder(hitCollider))
                return true;

            if (owner != null && hitCollider.transform.root == owner.transform.root)
                return true;

            if (cpuOwner != null && hitCollider.transform.root == cpuOwner.transform.root)
                return true;

            if (hitCollider.GetComponentInParent<ItemEntity>() != null ||
                hitCollider.GetComponentInParent<BuffItemEntity>() != null)
            {
                return true;
            }

            if (hitCollider.isTrigger &&
                hitCollider.GetComponentInParent<PlayerEntity>() == null &&
                hitCollider.GetComponentInParent<EnemyEntity>() == null &&
                hitCollider.GetComponentInParent<EnemyCPUEntity>() == null &&
                hitCollider.GetComponentInParent<DestructibleBuilding>() == null)
            {
                return true;
            }

            return false;
        }

        private void ApplyDamage(
            Collider hitCollider,
            float damage,
            PlayerEntity owner,
            EnemyCPUEntity cpuOwner,
            Vector3 hitPoint)
        {
            PlayerEntity player = hitCollider.GetComponentInParent<PlayerEntity>();
            if (player != null && player != owner)
            {
                if (!player.IsActiveDamageCollider(hitCollider))
                    return;

                bool wasAlive = player.CurrentHealth > 0f && !player.IsRespawning;
                if (cpuOwner != null)
                    player.Damage(damage, cpuOwner);
                else
                    player.Damage(damage, owner);

                if (wasAlive && owner != null)
                    owner.NotifyHitConfirmed(player.CurrentHealth <= 0f, true);
                return;
            }

            EnemyEntity enemy = hitCollider.GetComponent<EnemyEntity>();
            if (enemy == null)
                enemy = hitCollider.GetComponentInParent<EnemyEntity>();
            if (enemy != null)
            {
                bool wasAlive = enemy.CurrentHealth > 0f;
                bool defeated = wasAlive && damage >= enemy.CurrentHealth;
                if (cpuOwner != null)
                    enemy.Damage(damage, cpuOwner);
                else
                    enemy.Damage(damage, owner);

                if (wasAlive && owner != null)
                    owner.NotifyHitConfirmed(defeated);
                return;
            }

            EnemyCPUEntity cpu = hitCollider.GetComponent<EnemyCPUEntity>();
            if (cpu == null)
                cpu = hitCollider.GetComponentInParent<EnemyCPUEntity>();
            if (cpu != null && cpu != cpuOwner)
            {
                bool wasAlive = cpu.CPUCurrentHealth > 0f;
                if (owner != null)
                    cpu.Damage(damage, owner);
                else
                    cpu.Damage(damage, cpuOwner);

                if (wasAlive && owner != null)
                    owner.NotifyHitConfirmed(cpu.CPUCurrentHealth <= 0f, true);
                return;
            }

            DestructibleBuilding building = hitCollider.GetComponentInParent<DestructibleBuilding>();
            if (building != null)
                building.Damage(damage, hitPoint);
        }

        private float GetEffectiveDamage(PlayerEntity owner, EnemyCPUEntity cpuOwner)
        {
            if (owner != null)
                return m_damage * Mathf.Max(0.01f, owner.Stats.GetStat(StatKey.AttackPower));

            if (cpuOwner != null)
                return m_damage * Mathf.Max(0.01f, cpuOwner.Stats.GetStat(StatKey.AttackPower));

            return m_damage;
        }

        private float GetEffectiveFireCooldown(PlayerEntity owner)
        {
            float attackSpeed = owner != null
                ? Mathf.Max(0.01f, owner.Stats.GetStat(StatKey.FireInterval))
                : 1f;

            return m_fireCooldown / attackSpeed;
        }

        private Transform GetPrimaryMuzzle()
        {
            if (m_muzzleTransforms != null)
            {
                for (int i = 0; i < m_muzzleTransforms.Length; i++)
                {
                    if (m_muzzleTransforms[i] != null)
                        return m_muzzleTransforms[i];
                }
            }

            return null;
        }

        private void SpawnMuzzleFlash(Transform muzzle, Quaternion rotation)
        {
            if (m_muzzleFlashPrefab == null || muzzle == null)
                return;

            RuntimeObjectPool.Spawn(m_muzzleFlashPrefab, muzzle.position, rotation);
        }

        private void PlayFireSound(Transform muzzle)
        {
            if (muzzle == null)
                return;

            global::AudioManager.Instance?.PlayCueAtPosition(
                global::AudioCue.Railgun,
                muzzle.position,
                0.9f);
        }
    }
}
