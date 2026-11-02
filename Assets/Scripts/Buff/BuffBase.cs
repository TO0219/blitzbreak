namespace MyGame
{
    /// <summary>
    /// 全バフ共通の基底クラス
    /// 新しいバフを作るときはこのクラスを継承して作成する
    /// IBuffを実装しているため、OnApply・OnTick・OnRemoveの実装が必須
    /// </summary>
    public abstract class BuffBase : IBuff
    {
        //********************************************************
        // 変数の定義
        //********************************************************

        /// <summary>バフの一意な識別子　重複しないように命名する</summary>
        private string buffId;

        /// <summary>バフの表示名　UI等に使用する</summary>
        private string displayName;

        /// <summary>バフの持続時間（秒）　0以下で永続バフになる</summary>
        private float duration;

        /// <summary>バフの最大スタック数　同じバフを重ねがけできる上限</summary>
        private int maxStack;

        //********************************************************
        // プロパティ
        //********************************************************

        public string BuffId => buffId;
        public string DisplayName => displayName;
        public float Duration => duration;
        public int MaxStack => maxStack;

        /// <summary>durationが0以下の場合は永続バフとして扱う</summary>
        public bool IsPermanent => duration <= 0.0f;

        //********************************************************
        // コンストラクタ
        //********************************************************

        /// <summary>
        /// バフの基本情報を設定するコンストラクタ
        /// 継承先のコンストラクタからbase()で呼び出す
        /// </summary>
        /// <param name="buffId">バフの一意な識別子</param>
        /// <param name="displayName">バフの表示名</param>
        /// <param name="duration">持続時間（秒）　0以下で永続</param>
        /// <param name="maxStack">最大スタック数　デフォルトは1</param>
        protected BuffBase(string buffId, string displayName, float duration, int maxStack = 1)
        {
            this.buffId = buffId;
            this.displayName = displayName;
            this.duration = duration;
            this.maxStack = maxStack;
        }

        //********************************************************
        // IBuffの実装（継承先で必ず実装する）
        //********************************************************

        public abstract void OnApply(Entity target);
        public abstract void OnTick(Entity target, float deltaTime);
        public abstract void OnRemove(Entity target);
    }
}