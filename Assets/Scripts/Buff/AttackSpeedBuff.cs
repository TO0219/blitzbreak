using System.Reflection;
using UnityEngine;

namespace MyGame
{
    /// <summary>
    /// 攻撃速度変更バフ
    /// PlayerEntityが使用する武器の発射間隔へ反映する倍率を変更する
    /// 取得するたびに効果を重ね掛けし、解除時は1スタック分だけ効果を戻す
    /// PlayerEntityのprivateフィールドへリフレクションで直接反映する
    /// </summary>
    public class AttackSpeedBuff : BuffBase
    {
        //********************************************************
        // 変数の定義
        //********************************************************

        /// <summary>
        /// 攻撃速度に掛ける倍率
        /// 1.25なら発射速度上昇、0.8なら発射速度低下になる
        /// </summary>
        private float attackSpeedMultiplier;

        /// <summary>リフレクションで取得したattackSpeedMultiplierフィールド</summary>
        //private static readonly FieldInfo attackSpeedMultiplierField = typeof(PlayerEntity).GetField(
        //    "attackSpeedMultiplier",
        //    BindingFlags.NonPublic | BindingFlags.Instance
        //);

        //********************************************************
        // コンストラクタ
        //********************************************************

        /// <summary>
        /// 攻撃速度変更バフのコンストラクタ
        /// </summary>
        // <param name="attackSpeedMultiplier">
        /// 攻撃速度に掛ける倍率
        /// 1より大きい値で強化、1より小さい値で弱体化になる
        /// </param>
        public AttackSpeedBuff(float attackSpeedMultiplier)
            : base("attackSpeed", "攻撃速度", 0f, 5)
        {
            this.attackSpeedMultiplier = Mathf.Max(0.01f, attackSpeedMultiplier);
        }

        //********************************************************
        // IBuffの実装
        //********************************************************

        /// <summary>
        /// バフ付与時の処理
        /// 現在の攻撃速度倍率に1スタック分の倍率を掛ける
        /// 実際の発射間隔への反映処理は、各武器の発射処理側で行う
        /// </summary>
        // <param name="target">バフを受けるEntity</param>
        public override void OnApply(Entity target)
        {
            target.Stats.MultiplyStat("attackSpeed", attackSpeedMultiplier);
        }

        /// <summary>
        /// 毎フレームの処理
        /// このバフは被弾によって解除されるまで継続するため、時間更新は行わない
        /// </summary>
        // <param name="target">バフを受けるEntity</param>
        // <param name="deltaTime">前フレームからの経過時間</param>
        public override void OnTick(Entity target, float deltaTime) { }

        /// <summary>
        /// バフ解除時の処理
        /// 現在の攻撃速度倍率から、解除された1スタック分の倍率だけを取り除く
        /// </summary>
        // <param name="target">バフを受けるEntity</param>
        public override void OnRemove(Entity target)
        {
            target.Stats.MultiplyStat("attackSpeed", 1f / attackSpeedMultiplier);
        }
    }
}