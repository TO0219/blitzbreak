using MyGame;
using UnityEngine;

public class UnLoad_UI : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    public void UnLoadUI()
    {
        MultiSceneManager.Instance.Unload("PlayerDetailScene");
    }

}
