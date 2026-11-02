using System.Reflection;
using UnityEngine;

namespace MyGame
{
    /// <summary>
    /// 移動速度変更バフ
    /// 地上移動速度・空中移動速度の両方に倍率をかけて移動速度を変更する
    /// 取得するたびに効果を重ね掛けし、解除時は1スタック分だけ効果を戻す
    /// PlayerEntityのprivateフィールドへリフレクションで直接反映する
    /// </summary>
    public class SpeedUpBuff : BuffBase
    {
        //********************************************************
        // 変数の定義
        //********************************************************

        /// <summary>
        /// 移動速度に掛ける倍率
        /// 1.25なら速度上昇、0.8なら速度低下になる
        /// </summary>
        private float speedMultiplier;

        /// <summary>リフレクションで取得したgroundMoveSpeedフィールド</summary>
        //private static readonly FieldInfo groundMoveSpeedField = typeof(PlayerEntity).GetField(
        //    "groundMoveSpeed",
        //    BindingFlags.NonPublic | BindingFlags.Instance
        //);

        ///// <summary>リフレクションで取得したairMoveSpeedフィールド</summary>
        //private static readonly FieldInfo airMoveSpeedField = typeof(PlayerEntity).GetField(
        //    "airMoveSpeed",
        //    BindingFlags.NonPublic | BindingFlags.Instance
        //);

        //********************************************************
        // コンストラクタ
        //********************************************************

        /// <summary>
        /// 移動速度変更バフのコンストラクタ
        /// 新仕様ではバフは時間切れでは消えず、被弾時の消失判定によって解除される
        /// durationは既存の呼び出し処理との互換性を保つために受け取るが、
        /// 効果の持続時間としては使用しない
        /// </summary>
        // <param name="speedMultiplier">
        /// 移動速度に掛ける倍率
        /// 1より大きい値で速度上昇、1より小さい値で速度低下になる
        /// </param>
        // <param name="duration">
        /// 旧仕様との互換用引数
        /// 新仕様では値にかかわらず被弾で解除されるまで継続する
        /// </param>
        public SpeedUpBuff(float speedMultiplier, float duration = 0f)
            : base("moveSpeed", "移動速度", 0f, 3)
        {
            this.speedMultiplier = Mathf.Max(0.01f, speedMultiplier);
        }

        //********************************************************
        // IBuffの実装
        //********************************************************

        /// <summary>
        /// バフ付与時の処理
        /// 現在の地上移動速度・空中移動速度に1スタック分の倍率を掛ける
        /// 同じバフを複数取得した場合は、現在値に対してさらに倍率を掛ける
        /// </summary>
        /// <param name="target">バフを受けるEntity</param>
        public override void OnApply(Entity target)
        {
            target.Stats.MultiplyStat("moveSpeed", speedMultiplier);
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
        /// 現在の移動速度から、解除された1スタック分の倍率だけを取り除く
        /// </summary>
        // <param name="target">バフを受けるEntity</param>
        public override void OnRemove(Entity target)
        {
            target.Stats.MultiplyStat("moveSpeed", 1f / speedMultiplier);
        }
    }
}