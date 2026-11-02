using UnityEngine;

namespace MyGame
{
    /// <summary>
    /// 移動速度バフアイテムの取得処理を担当するEntity
    /// プレイヤーが取得した際に、移動速度バフの効果定義を生成して付与する
    /// </summary>
    public class SpeedUpBuffItemEntity : BuffItemEntity
    {
        //********************************************************
        // 変数の定義
        //********************************************************

        /// <summary>移動速度に掛ける倍率</summary>
        [SerializeField] private float speedMultiplier = 1.15f;

        //********************************************************
        // Entityライフサイクル
        //********************************************************

        /// <summary>
        /// 初期化処理
        /// このPrefabが付与する移動速度バフを設定する
        /// </summary>
        public override void InitEntity()
        {
            base.InitEntity();
            Setup(new SpeedUpBuff(speedMultiplier: speedMultiplier, duration: 0f));
        }
    }
}
