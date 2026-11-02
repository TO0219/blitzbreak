using System.Reflection;
using UnityEngine;

namespace MyGame
{
    /// <summary>
    /// 攻撃力変更バフ
    /// PlayerEntityが発射する攻撃のダメージ倍率を変更する
    /// 取得するたびに効果を重ね掛けし、解除時は1スタック分だけ効果を戻す
    /// PlayerEntityのprivateフィールドへリフレクションで直接反映する
    /// </summary>
    public class AttackPowerBuff : BuffBase
    {
        //********************************************************
        // 変数の定義
        //********************************************************

        /// <summary>
        /// 攻撃ダメージに掛ける倍率
        /// 1.5なら攻撃力上昇、0.8なら攻撃力低下になる
        /// </summary>
        private float attackPowerMultiplier;

        //********************************************************
        // コンストラクタ
        //********************************************************

        // <summary>
        // 攻撃力変更バフのコンストラクタ
        // </summary>
        // <param name="attackPowerMultiplier">
        // 攻撃ダメージに掛ける倍率
        // 1より大きい値で強化、1より小さい値で弱体化になる
        // </param>
        public AttackPowerBuff(float multiplier)
            : base("attackPower", "攻撃力", 0f, 5)
        {
            attackPowerMultiplier = Mathf.Max(0.01f, multiplier);
        }

        //********************************************************
        // IBuffの実装
        //********************************************************

        /// <summary>
        /// バフ付与時の処理
        /// 現在の攻撃力倍率に1スタック分の倍率を掛ける
        /// 実際の弾ダメージへの反映処理は、発射処理側でこの倍率を使用して行う
        /// </summary>
        // <param name="target">バフを受けるEntity</param>
        public override void OnApply(Entity target)
        {
            target.Stats.MultiplyStat("attackPower", attackPowerMultiplier);
        }

        // <summary>
        // 毎フレームの処理
        // このバフは被弾によって解除されるまで継続するため、時間更新は行わない
        // </summary>
        // <param name="target">バフを受けるEntity</param>
        // <param name="deltaTime">前フレームからの経過時間</param>
        public override void OnTick(Entity target, float deltaTime) { }

        // <summary>
        // バフ解除時の処理
        // 現在の攻撃力倍率から、解除された1スタック分の倍率だけを取り除く
        // </summary>
        // <param name="target">バフを受けるEntity</param>
        public override void OnRemove(Entity target)
        {
            target.Stats.MultiplyStat("attackPower", 1f / attackPowerMultiplier);
        }
    }
}