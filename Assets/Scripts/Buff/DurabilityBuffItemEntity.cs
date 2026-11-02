using UnityEngine;

namespace MyGame
{
    /// <summary>
    /// 耐久バフアイテムの取得処理を担当するEntity
    /// プレイヤーが取得した際に、被ダメージ軽減バフの効果定義を生成して付与する
    /// </summary>
    public class DurabilityBuffItemEntity : BuffItemEntity
    {
        //********************************************************
        // 変数の定義
        //********************************************************

        /// <summary>被ダメージ量に掛ける倍率</summary>
        [SerializeField] private float durabilityMultiplier = 1.15f;

        //********************************************************
        // Entityライフサイクル
        //********************************************************

        /// <summary>
        /// 初期化処理
        /// このPrefabが付与する耐久バフを設定する
        /// </summary>
        public override void InitEntity()
        {
            base.InitEntity();
            Setup(new DurabilityBuff(durabilityMultiplier));
        }
    }
}
