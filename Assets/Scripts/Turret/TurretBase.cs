using UnityEngine;

namespace MyGame
{
    /// <summary>
    /// 全砲台共通の基底クラス
    /// 新しい砲台を作るときはこのクラスを継承して作成する
    /// ITurretを実装しているためOnEquip・OnUnequip・OnFireの実装が必須
    /// </summary>
    public abstract class TurretBase : ITurret
    {
        //********************************************************
        // 変数の定義
        //********************************************************

        /// <summary>砲台の一意な識別子</summary>
        private string turretId;

        /// <summary>砲台の表示名　UI等に使用する</summary>
        private string displayName;

        //********************************************************
        // プロパティ
        //********************************************************

        public string TurretId => turretId;
        public string DisplayName => displayName;

        //********************************************************
        // コンストラクタ
        //********************************************************

        /// <summary>
        /// 砲台の基本情報を設定するコンストラクタ
        /// 継承先のコンストラクタからbase()で呼び出す
        /// </summary>
        /// <param name="turretId">砲台の一意な識別子</param>
        /// <param name="displayName">砲台の表示名</param>
        protected TurretBase(string turretId, string displayName)
        {
            this.turretId = turretId;
            this.displayName = displayName;
        }

        //********************************************************
        // ITurretの実装（継承先で必ず実装する）
        //********************************************************

        public abstract void OnEquip(PlayerEntity owner);
        public abstract void OnUnequip(PlayerEntity owner);
        public abstract void OnFire(PlayerEntity owner);
    }
}