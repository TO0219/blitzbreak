using UnityEngine;
using System.Collections.Generic;
using Component = MyGame.Component;

namespace MyGame
{
    /// <summary>
    /// ゲーム内のエンティティを表す抽象クラス
    /// </summary>
    public abstract class Entity : MonoBehaviour
    {
        //********************************************************
        // 変数の定義

        //********************************************************
        //このオブジェクトが有効状態かどうか
        protected bool m_isActive = true;

        public bool IsActive {
            get { return m_isActive; } 
        }

        protected bool m_isDestroy = false;
        public bool IsDestroy {
            get { return m_isDestroy; } 
        }

        private bool m_changeActive = false;

        protected StatData m_stats = new StatData();

        public StatData Stats => m_stats;

        //コンポーネント 後で必要なら
        //protected List<Component> m_components = new List<Component>();

        //********************************************************
        // ライフサイクル
        //********************************************************
        public virtual void InitEntity() { }
        public virtual void UninitEntity() { }
        public virtual void UpdateEntity() { }
        public virtual void FixedUpdateEntity() { }
        public virtual void LateUpdateEntity() { }
        protected virtual void OnEnableEntity() { }
        protected virtual void OnDisableEntity() { }


        public virtual void UpdateActiveState()
        {
            if (m_changeActive) // 有効状態の変更があったか確認
            {
                if (m_isActive) // 有効状態が変化した場合の処理
                {
                    //有効化されたときの処理
                    OnEnableEntity();
                }
                else
                {
                    //無効化されたときの処理
                    OnDisableEntity();
                }
                m_changeActive = false; //変更フラグをリセット
            }
        }

        //********************************************************
        // 寿命管理
        //********************************************************
        public void SetActiveEntity(bool isActive)
        {
            if(m_isActive == isActive) return;  //フラグが同じなら何もしない

            m_isActive = isActive;  //状態を変更

            m_changeActive = true;  //変更フラグを立てる
        }

        public void OnDestroyEntity()
        {
            m_isDestroy = true;
            SetActiveEntity(false);
        }

        public void PrepareForReuse()
        {
            m_isDestroy = false;
            m_isActive = true;
            m_changeActive = false;
        }

        //********************************************************
        // エンティティの登録・登録解除
        //********************************************************
        protected virtual void OnEnable()
        {
            SetActiveEntity(true);
            if (EntityManager.Instance != null)
                EntityManager.Instance.AddEntity(this);
        }

        protected virtual void OnDisable()
        {
            SetActiveEntity(false);
        }
    }
    
 }



