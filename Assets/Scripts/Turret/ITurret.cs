namespace MyGame
{
    /// <summary>
    /// 砲台が必ず実装しなければならないメソッドを定義するインターフェース
    /// このインターフェースを実装することで、全砲台に統一した振る舞いを強制する
    /// </summary>
    public interface ITurret
    {
        /// <summary>
        /// 砲台が有効化されたときに呼ばれる
        /// モデルの切り替えなど、有効化時に一度だけ行う処理を書く
        /// </summary>
        /// <param name="owner">砲台を所有するPlayerEntity</param>
        void OnEquip(PlayerEntity owner);

        /// <summary>
        /// 砲台が無効化されたときに呼ばれる
        /// OnEquipで行った処理を元に戻す後処理を書く
        /// </summary>
        /// <param name="owner">砲台を所有するPlayerEntity</param>
        void OnUnequip(PlayerEntity owner);

        /// <summary>
        /// 攻撃処理　毎フレーム呼ばれる
        /// 攻撃入力の受付や発射処理を書く
        /// </summary>
        /// <param name="owner">砲台を所有するPlayerEntity</param>
        void OnFire(PlayerEntity owner);
    }
}