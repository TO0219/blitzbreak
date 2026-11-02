using UnityEngine;

namespace MyGame
{
    /// <summary>
    /// 攻撃力バフアイテムの取得処理を担当するEntity
    /// プレイヤーが取得した際に、攻撃ダメージ上昇バフの効果定義を生成して付与する
    /// </summary>
    public class AttackPowerBuffItemEntity : BuffItemEntity
    {
        //********************************************************
        // 変数の定義
        //********************************************************

        /// <summary>攻撃ダメージに掛ける倍率</summary>
        [SerializeField] private float attackPowerMultiplier = 1.15f;

        //********************************************************
        // Entityライフサイクル
        //********************************************************

        /// <summary>
        /// 初期化処理
        /// このPrefabが付与する攻撃力バフを設定する
        /// </summary>
        public override void InitEntity()
        {
            base.InitEntity();
            Setup(new AttackPowerBuff(attackPowerMultiplier));
        }
    }
}
