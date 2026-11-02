using System.Collections.Generic;
using UnityEngine;

namespace MyGame
{
    /// <summary>
    /// プレイヤーにアタッチして使用するバフ管理クラス
    /// バフの付与・解除・所持数確認・被弾時の消失判定を一括管理する
    /// 同じ種類のバフを複数取得した場合は、取得した個数分だけ効果を重ね掛けする
    /// Entityのライフサイクルに乗って動作する
    /// </summary>
    public class BuffManagerEntity : Entity
    {
        //********************************************************
        // 内部クラス
        //********************************************************

        /// <summary>
        /// 実行中のバフ1スタック分のデータ
        /// 同じ種類のバフを複数取得した場合も、取得1回につき1件ずつ保持する
        /// これにより、被弾時に1スタック単位で解除できる
        /// </summary>
        private class ActiveBuff
        {
            /// <summary>この1スタック分のバフ効果定義</summary>
            public BuffBase Definition;

            /// <summary>
            /// この1スタック分の残り時間
            /// 新仕様の永続バフでは使用しないが、時間制バフとの互換用に保持する
            /// </summary>
            public float RemainingTime;

            /// <summary>
            /// バフ定義から実行中データを生成する
            /// </summary>
            /// <param name="definition">取得したバフ効果</param>
            public ActiveBuff(BuffBase definition)
            {
                Definition = definition;
                RemainingTime = definition.Duration;
            }
        }

        //********************************************************
        // 変数の定義
        //********************************************************

        /// <summary>
        /// バフの効果を反映する対象プレイヤー
        /// Inspectorで未設定の場合はInitEntityで同じGameObjectから取得する
        /// </summary>
        [SerializeField] private Entity owner;

        [Header("被弾時のバフ消失設定")]

        /// <summary>
        /// 被弾したときにバフが失われる確率
        /// 0.5なら被弾ごとに50%の確率で消失処理を実行する
        /// </summary>
        [Tooltip("被弾したときにバフ消失処理が発生する確率。0.5は50%を表します。")]
        [SerializeField, Range(0f, 1f)] private float removeBuffChanceOnDamage = 0.5f;

        /// <summary>
        /// バフ消失処理が発生した場合に失う最小スタック数
        /// </summary>
        [Tooltip("バフ消失が発生した場合に失う最小個数です。")]
        [SerializeField, Min(1)] private int minRemoveBuffCount = 1;

        /// <summary>
        /// バフ消失処理が発生した場合に失う最大スタック数
        /// </summary>
        [Tooltip("バフ消失が発生した場合に失う最大個数です。")]
        [SerializeField, Min(1)] private int maxRemoveBuffCount = 2;

        /// <summary>
        /// 現在所持している全バフのリスト
        /// 同じ種類のバフも、取得した個数分だけ別要素として保持する
        /// </summary>
        private readonly List<ActiveBuff> activeBuffs = new();

        //********************************************************
        // Entityライフサイクル
        //********************************************************

        /// <summary>
        /// 初期化処理
        /// ownerが未設定の場合は同じGameObjectにあるPlayerEntityを取得する
        /// </summary>
        public override void InitEntity()
        {
            if (owner == null)
            {
                owner = GetComponent<Entity>();
            }

            if (owner == null)
            {
                Debug.LogWarning("[BuffManagerEntity] PlayerEntityが見つかりません。");
            }
        }

        /// <summary>
        /// 毎フレームの更新処理
        /// 時間制限を持つバフが追加された場合に備えて、残り時間の更新を行う
        /// 新仕様のバフは永続設定のため、被弾で解除されるまで残り続ける
        /// </summary>
        public override void UpdateEntity()
        {
            TickBuffs(Time.deltaTime);
        }

        //********************************************************
        // バフ付与・解除処理
        //********************************************************

        /// <summary>
        /// バフを1スタック分付与する
        /// 同じ種類のバフを既に所持していても、新しく取得した分の効果を重ね掛けする
        /// </summary>
        /// <param name="buff">新しく取得したバフ効果</param>
        public void ApplyBuff(BuffBase buff)
        {
            if (buff == null) return;
            if (owner == null) return;

            int currentStackCount = GetStack(buff.BuffId);

            // BuffBase側で上限を設定している場合は、その上限を超えて付与しない。
            // バフごとに設定された最大スタック数を超えて付与しない。
            if (currentStackCount >= buff.MaxStack)
            {
                return;
            }

            ActiveBuff active = new ActiveBuff(buff);
            activeBuffs.Add(active);

            // 取得した1スタック分の効果をPlayerEntityへ反映する。
            buff.OnApply(owner);

        }

        /// <summary>
        /// 指定したIDのバフを1スタック分だけ解除する
        /// 同じ種類を複数所持している場合は、最後に取得した1件を解除する
        /// </summary>
        /// <param name="buffId">解除するバフのID</param>
        /// <returns>解除できた場合はtrue、対象が存在しない場合はfalse</returns>
        public bool RemoveOneStack(string buffId)
        {
            for (int i = activeBuffs.Count - 1; i >= 0; i--)
            {
                ActiveBuff active = activeBuffs[i];

                if (active.Definition.BuffId != buffId)
                {
                    continue;
                }

                RemoveActiveBuffAt(i);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 指定したIDのバフを全スタック解除する
        /// メニュー遷移やリセット処理などで、種類単位で効果を完全に消す場合に使用する
        /// </summary>
        /// <param name="buffId">全解除するバフのID</param>
        public void RemoveBuff(string buffId)
        {
            for (int i = activeBuffs.Count - 1; i >= 0; i--)
            {
                ActiveBuff active = activeBuffs[i];

                if (active.Definition.BuffId == buffId)
                {
                    RemoveActiveBuffAt(i);
                }
            }
        }

        /// <summary>
        /// 現在所持している全バフを解除する
        /// プレイヤーの初期化や戦闘終了時のリセット処理で使用する
        /// </summary>
        public void ClearAllBuffs()
        {
            for (int i = activeBuffs.Count - 1; i >= 0; i--)
            {
                RemoveActiveBuffAt(i);
            }
        }

        public int RemoveRandomStacks(int removeCount)
        {
            if (removeCount <= 0 || activeBuffs.Count <= 0)
                return 0;

            int removedCount = 0;
            int targetCount = Mathf.Min(removeCount, activeBuffs.Count);

            for (int i = 0; i < targetCount; i++)
            {
                if (activeBuffs.Count <= 0)
                    break;

                int randomIndex = Random.Range(0, activeBuffs.Count);
                RemoveActiveBuffAt(randomIndex);
                removedCount++;
            }

            return removedCount;
        }

        //********************************************************
        // 被弾時のバフ消失処理
        //********************************************************

        /// <summary>
        /// プレイヤーがダメージを受けたときに呼び出すバフ消失判定
        /// Inspectorで設定した確率に成功した場合、所持バフからランダムに1～2個程度を解除する
        /// </summary>
        public void TryRemoveBuffsOnDamage()
        {
            if (activeBuffs.Count <= 0)
            {
                return;
            }

            // Random.valueは0以上1未満の値を返す。
            // 設定確率より大きい場合は、今回の被弾ではバフを失わない。
            if (Random.value > removeBuffChanceOnDamage)
            {
                return;
            }

            int minimum = Mathf.Clamp(minRemoveBuffCount, 1, activeBuffs.Count);
            int maximum = Mathf.Clamp(maxRemoveBuffCount, minimum, activeBuffs.Count);
            int removeCount = Random.Range(minimum, maximum + 1);

            for (int i = 0; i < removeCount; i++)
            {
                if (activeBuffs.Count <= 0)
                {
                    break;
                }

                // 所持中の全スタックからランダムに1個を選んで解除する。
                // 同じ種類を複数持っている場合も、そのうち1個だけが消える。
                int randomIndex = Random.Range(0, activeBuffs.Count);
                ActiveBuff removedBuff = activeBuffs[randomIndex];

                RemoveActiveBuffAt(randomIndex);

            }
        }

        //********************************************************
        // 所持情報取得処理
        //********************************************************

        /// <summary>
        /// 指定したIDのバフを現在何スタック所持しているか返す
        /// UIで種類ごとの所持個数を表示する場合に使用する
        /// </summary>
        /// <param name="buffId">確認するバフのID</param>
        /// <returns>指定したバフの現在所持数</returns>
        public int GetStack(string buffId)
        {
            int stackCount = 0;

            for (int i = 0; i < activeBuffs.Count; i++)
            {
                if (activeBuffs[i].Definition.BuffId == buffId)
                {
                    stackCount++;
                }
            }

            return stackCount;
        }

        /// <summary>
        /// 指定したIDのバフを1個以上所持しているか返す
        /// </summary>
        /// <param name="buffId">確認するバフのID</param>
        /// <returns>1個以上所持している場合はtrue</returns>
        public bool HasBuff(string buffId)
        {
            return GetStack(buffId) > 0;
        }

        /// <summary>
        /// 現在所持しているバフの合計個数を返す
        /// 種類に関係なく、全スタック数を合算した値となる
        /// </summary>
        /// <returns>現在所持している全バフの合計個数</returns>
        public int GetTotalStackCount()
        {
            return activeBuffs.Count;
        }

        /// <summary>
        /// 現在所持しているバフを、IDごとの個数にまとめて返す
        /// UI側で全バフ一覧を表示する場合に使用する
        /// </summary>
        /// <returns>バフIDと所持個数の組み合わせ</returns>
        public Dictionary<string, int> GetAllBuffStackCounts()
        {
            Dictionary<string, int> result = new Dictionary<string, int>();
            GetAllBuffStackCounts(result);
            return result;
        }

        public void GetAllBuffStackCounts(Dictionary<string, int> result)
        {
            if (result == null) return;

            result.Clear();
            for (int i = 0; i < activeBuffs.Count; i++)
            {
                string buffId = activeBuffs[i].Definition.BuffId;

                if (result.ContainsKey(buffId))
                {
                    result[buffId]++;
                }
                else
                {
                    result[buffId] = 1;
                }
            }
        }

        public void ApplyNetworkStackCounts(
            int moveSpeed,
            int attackPower,
            int attackRange,
            int attackSpeed,
            int durability)
        {
            SetKnownBuffStack("moveSpeed", moveSpeed);
            SetKnownBuffStack("attackPower", attackPower);
            SetKnownBuffStack("attackRange", attackRange);
            SetKnownBuffStack("attackSpeed", attackSpeed);
            SetKnownBuffStack("durability", durability);
        }

        private void SetKnownBuffStack(string buffId, int targetCount)
        {
            targetCount = Mathf.Max(0, targetCount);

            while (GetStack(buffId) > targetCount)
                RemoveOneStack(buffId);

            while (GetStack(buffId) < targetCount)
            {
                BuffBase buff = CreateKnownBuff(buffId);
                if (buff == null || GetStack(buffId) >= buff.MaxStack)
                    break;

                ApplyBuff(buff);
            }
        }

        private static BuffBase CreateKnownBuff(string buffId)
        {
            const float multiplier = 1.15f;

            return buffId switch
            {
                "moveSpeed" => new SpeedUpBuff(multiplier, 0f),
                "attackPower" => new AttackPowerBuff(multiplier),
                "attackRange" => new AttackRangeBuff(multiplier),
                "attackSpeed" => new AttackSpeedBuff(multiplier),
                "durability" => new DurabilityBuff(multiplier),
                _ => null
            };
        }

        //********************************************************
        // 内部処理
        //********************************************************

        /// <summary>
        /// 有効中の全バフについて、毎フレーム処理と残り時間の更新を行う
        /// 新仕様の5種類のバフは永続設定のため、時間による解除は発生しない
        /// </summary>
        /// <param name="deltaTime">前フレームからの経過時間</param>
        private void TickBuffs(float deltaTime)
        {
            for (int i = activeBuffs.Count - 1; i >= 0; i--)
            {
                ActiveBuff active = activeBuffs[i];

                active.Definition.OnTick(owner, deltaTime);

                // 永続バフは被弾時の解除処理だけで消えるため、時間計算を行わない。
                if (active.Definition.IsPermanent)
                {
                    continue;
                }

                active.RemainingTime -= deltaTime;

                if (active.RemainingTime <= 0f)
                {
                    RemoveActiveBuffAt(i);
                }
            }
        }

        /// <summary>
        /// リスト内の指定位置にあるバフを1スタック分解除する内部処理
        /// バフ効果のOnRemoveを呼び出してから、管理リストから削除する
        /// </summary>
        /// <param name="index">解除するバフのリスト位置</param>
        private void RemoveActiveBuffAt(int index)
        {
            if (index < 0 || index >= activeBuffs.Count)
            {
                return;
            }

            ActiveBuff active = activeBuffs[index];

            if (owner != null)
            {
                // バフスクリプト側で、該当するprivateフィールドから
                // 解除対象の1スタック分だけ効果を取り除く。
                active.Definition.OnRemove(owner);
            }

            activeBuffs.RemoveAt(index);
        }
    }
}
