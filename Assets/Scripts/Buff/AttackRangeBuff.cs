using System.Reflection;
using UnityEngine;

namespace MyGame
{
    /// <summary>
    /// 攻撃範囲変更バフ
    /// PlayerEntityが発射する攻撃の有効範囲へ反映する倍率を変更する
    /// 取得するたびに効果を重ね掛けし、解除時は1スタック分だけ効果を戻す
    /// PlayerEntityのprivateフィールドへリフレクションで直接反映する
    /// </summary>
    public class AttackRangeBuff : BuffBase
    {
        //********************************************************
        // 変数の定義
        //********************************************************

        /// <summary>
        /// 攻撃範囲に掛ける倍率
        /// 1.5なら範囲拡大、0.8なら範囲縮小になる
        /// </summary>
        private float attackRangeMultiplier;

        /// <summary>リフレクションで取得したattackRangeMultiplierフィールド</summary>
        //private static readonly FieldInfo attackRangeMultiplierField = typeof(PlayerEntity).GetField(
        //    "attackRangeMultiplier",
        //    BindingFlags.NonPublic | BindingFlags.Instance
        //);

        //********************************************************
        // コンストラクタ
        //********************************************************

        /// <summary>
        /// 攻撃範囲変更バフのコンストラクタ
        /// </summary>
        // <param name="attackRangeMultiplier">
        /// 攻撃範囲に掛ける倍率
        /// 1より大きい値で強化、1より小さい値で弱体化になる
        /// </param>
        public AttackRangeBuff(float attackRangeMultiplier)
            : base("attackRange", "攻撃範囲", 0f, 5)
        {
            this.attackRangeMultiplier = Mathf.Max(0.01f, attackRangeMultiplier);
        }

        //********************************************************
        // IBuffの実装
        //********************************************************

        /// <summary>
        /// バフ付与時の処理
        /// 現在の攻撃範囲倍率に1スタック分の倍率を掛ける
        /// 実際の射程や爆発範囲への反映処理は、武器または弾側で行う
        /// </summary>
        // <param name="target">バフを受けるEntity</param>
        public override void OnApply(Entity target)
        {
            target.Stats.MultiplyStat("attackRange", attackRangeMultiplier);
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
        /// 現在の攻撃範囲倍率から、解除された1スタック分の倍率だけを取り除く
        /// </summary>
        // <param name="target">バフを受けるEntity</param>
        public override void OnRemove(Entity target)
        {
            target.Stats.MultiplyStat("attackRange", 1f / attackRangeMultiplier);
        }
    }
}
