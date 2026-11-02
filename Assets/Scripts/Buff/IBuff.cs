namespace MyGame
{
    public interface IBuff
    {
        void OnApply(Entity target);    // 付与時
        void OnTick(Entity target, float deltaTime);  // 毎フレーム
        void OnRemove(Entity target);   // 除去時
    }
}