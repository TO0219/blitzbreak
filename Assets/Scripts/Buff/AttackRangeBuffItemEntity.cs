using UnityEngine;

namespace MyGame
{
    /// <summary>
    /// 攻撃範囲バフアイテムの取得処理を担当するEntity
    /// プレイヤーが取得した際に、弾の到達距離拡大バフの効果定義を生成して付与する
    /// </summary>
    public class AttackRangeBuffItemEntity : BuffItemEntity
    {
        //********************************************************
        // 変数の定義
        //********************************************************

        /// <summary>攻撃範囲に掛ける倍率</summary>
        [SerializeField] private float attackRangeMultiplier = 1.15f;

        //********************************************************
        // Entityライフサイクル
        //********************************************************

        /// <summary>
        /// 初期化処理
        /// このPrefabが付与する攻撃範囲バフを設定する
        /// </summary>
        public override void InitEntity()
        {
            base.InitEntity();
            Setup(new AttackRangeBuff(attackRangeMultiplier));
        }
    }
}
