using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MyGame
{
    public class SceneTransition : MonoBehaviour
    {
        [SerializeField] private bool enableEscKey = true;
        [SerializeField, Range(0f, 1f)] private float submitSoundWaitTime = 0.18f;

        private bool m_isOpeningOption;

        private void Update()
        {
            if (enableEscKey && Input.GetKeyDown(KeyCode.Escape))
                OpenOption();
        }

        public void OpenOption()
        {
            if (m_isOpeningOption)
                return;

            string currentScene = SceneManager.GetActiveScene().name;
            if (currentScene == "OptionScene") return;

            StartCoroutine(OpenOptionAfterSubmitSound(currentScene));
        }

        private IEnumerator OpenOptionAfterSubmitSound(string currentScene)
        {
            m_isOpeningOption = true;
            global::AudioManager.Instance?.PlayCue(global::AudioCue.UiSubmit);

            OptionManager.PreviousScene = currentScene;
            SceneTransitionManager.PreviousScene = currentScene;

            if (submitSoundWaitTime > 0f)
                yield return new WaitForSecondsRealtime(submitSoundWaitTime);

            if (SceneTransitionManager.Instance != null)
                SceneTransitionManager.Instance.LoadByName("OptionScene");
            else
                SceneManager.LoadScene("OptionScene");

            m_isOpeningOption = false;
        }
    }
}
